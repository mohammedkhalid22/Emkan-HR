using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace HRSystem.BLL.Devices;

// One punch as the device stored it: the user's number on the device and the time
public record DevicePunch(string UserId, DateTime Time);

// Reads the attendance log from a ZKTeco device over TCP (port 4370), the same protocol the
// vendor's own software uses. No SDK or COM install needed. Kept free of other project types
// so the company-side agent can compile this file as is.
public sealed class ZkClient : IDisposable
{
    // Command codes
    private const ushort CmdConnect = 1000;
    private const ushort CmdExit = 1001;
    private const ushort CmdAuth = 1102;
    private const ushort CmdGetFreeSizes = 50;
    private const ushort CmdAttLogRrq = 13;
    private const ushort CmdPrepareData = 1500;
    private const ushort CmdData = 1501;
    private const ushort CmdFreeData = 1502;
    private const ushort CmdPrepareBuffer = 1503;
    private const ushort CmdReadBuffer = 1504;
    private const ushort CmdAckOk = 2000;
    private const ushort CmdAckUnauth = 2005;

    // Every TCP frame starts with 50 50 82 7D and the payload length
    private const ushort TcpMagic1 = 0x5050;
    private const ushort TcpMagic2 = 0x7D82;
    private const int MaxChunk = 0xFFC0;

    private readonly TcpClient _tcp = new();
    private NetworkStream? _stream;
    private ushort _sessionId;
    private ushort _replyId = ushort.MaxValue - 1;

    public async Task ConnectAsync(string host, int port, int commKey, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            await _tcp.ConnectAsync(host, port, cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ZkException($"الجهاز مش بيرد على {host}:{port}. اتأكد إنه شغال وعلى نفس الشبكة");
        }
        _tcp.ReceiveTimeout = _tcp.SendTimeout = (int)timeout.TotalMilliseconds;
        _stream = _tcp.GetStream();

        var reply = await SendAsync(CmdConnect, [], cancellationToken);
        _sessionId = reply.SessionId;
        if (reply.Code == CmdAckUnauth)
            reply = await SendAsync(CmdAuth, MakeCommKey(commKey, _sessionId), cancellationToken);
        if (reply.Code != CmdAckOk)
            throw new ZkException(reply.Code == CmdAckUnauth
                ? "الجهاز رفض الاتصال: الـ Comm Key غلط"
                : $"الجهاز رفض الاتصال (رد {reply.Code})");
    }

    // The whole attendance log stored on the device
    public async Task<List<DevicePunch>> ReadAttendanceAsync(CancellationToken cancellationToken = default)
    {
        var records = await ReadRecordCountAsync(cancellationToken);
        if (records == 0)
            return [];

        var data = await ReadWithBufferAsync(CmdAttLogRrq, cancellationToken);
        return ParseAttendance(data, records);
    }

    public async ValueTask DisconnectAsync()
    {
        if (_stream is null)
            return;
        try { await SendAsync(CmdExit, [], CancellationToken.None); } catch { /* closing anyway */ }
        _stream = null;
    }

    public void Dispose() => _tcp.Dispose();

    // ---------- Protocol ----------

    private readonly record struct Reply(ushort Code, ushort SessionId, ushort ReplyId, byte[] Data);

    private async Task<int> ReadRecordCountAsync(CancellationToken cancellationToken)
    {
        var reply = await SendAsync(CmdGetFreeSizes, [], cancellationToken);
        if (reply.Code != CmdAckOk || reply.Data.Length < 80)
            throw new ZkException("الجهاز مارجّعش عدد السجلات");
        // 20 little-endian ints; the 9th is the number of attendance records
        return BinaryPrimitives.ReadInt32LittleEndian(reply.Data.AsSpan(8 * 4, 4));
    }

    private async Task<byte[]> ReadWithBufferAsync(ushort command, CancellationToken cancellationToken)
    {
        // <bhii: 1, command, 0, 0
        var request = new byte[11];
        request[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(request.AsSpan(1), (short)command);

        var reply = await SendAsync(CmdPrepareBuffer, request, cancellationToken);
        if (reply.Code == CmdData)
            return reply.Data; // small logs come back in one go
        if (reply.Code != CmdAckOk || reply.Data.Length < 5)
            throw new ZkException($"الجهاز رفض قراية السجلات (رد {reply.Code})");

        var size = BinaryPrimitives.ReadInt32LittleEndian(reply.Data.AsSpan(1, 4));
        using var all = new MemoryStream(size);
        for (var start = 0; start < size; start += MaxChunk)
            all.Write(await ReadChunkAsync(start, Math.Min(MaxChunk, size - start), cancellationToken));

        await SendAsync(CmdFreeData, [], cancellationToken);
        return all.ToArray();
    }

    private async Task<byte[]> ReadChunkAsync(int start, int size, CancellationToken cancellationToken)
    {
        var request = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(request, start);
        BinaryPrimitives.WriteInt32LittleEndian(request.AsSpan(4), size);

        var reply = await SendAsync(CmdReadBuffer, request, cancellationToken);
        if (reply.Code == CmdData)
            return reply.Data;
        if (reply.Code != CmdPrepareData)
            throw new ZkException($"رد غير متوقع من الجهاز أثناء القراية ({reply.Code})");

        // PREPARE_DATA, then one or more DATA frames, then ACK_OK
        using var chunk = new MemoryStream(size);
        while (true)
        {
            var next = await ReadFrameAsync(cancellationToken);
            if (next.Code == CmdData)
                chunk.Write(next.Data);
            else if (next.Code == CmdAckOk)
                return chunk.ToArray();
            else
                throw new ZkException($"رد غير متوقع من الجهاز أثناء القراية ({next.Code})");
        }
    }

    private async Task<Reply> SendAsync(ushort command, byte[] payload, CancellationToken cancellationToken)
    {
        if (_stream is null)
            throw new ZkException("مش متصل بالجهاز");

        var packet = BuildPacket(command, payload, _sessionId, _replyId);
        _replyId = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(14));

        await _stream.WriteAsync(packet, cancellationToken);
        var reply = await ReadFrameAsync(cancellationToken);
        _replyId = reply.ReplyId;
        return reply;
    }

    private async Task<Reply> ReadFrameAsync(CancellationToken cancellationToken)
    {
        var top = await ReadExactAsync(8, cancellationToken);
        if (BinaryPrimitives.ReadUInt16LittleEndian(top) != TcpMagic1 || BinaryPrimitives.ReadUInt16LittleEndian(top.AsSpan(2)) != TcpMagic2)
            throw new ZkException("الرد مش من جهاز ZKTeco (أو الجهاز شغال UDP بس)");
        var length = BinaryPrimitives.ReadInt32LittleEndian(top.AsSpan(4));
        if (length < 8 || length > 16 * 1024 * 1024)
            throw new ZkException("رد الجهاز بطول غير صحيح");

        var body = await ReadExactAsync(length, cancellationToken);
        return new Reply(
            BinaryPrimitives.ReadUInt16LittleEndian(body),
            BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(4)),
            BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(6)),
            body[8..]);
    }

    private async Task<byte[]> ReadExactAsync(int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        try
        {
            await _stream!.ReadExactlyAsync(buffer, cancellationToken);
        }
        catch (EndOfStreamException)
        {
            throw new ZkException("الجهاز قفل الاتصال");
        }
        return buffer;
    }

    // ---------- Pure helpers (tested directly) ----------

    // TCP frame: 50 50 82 7D, length, then header (command, checksum, session, reply id) and payload
    public static byte[] BuildPacket(ushort command, byte[] payload, ushort sessionId, ushort replyId)
    {
        var body = new byte[8 + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(body, command);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(4), sessionId);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(6), replyId);
        payload.CopyTo(body, 8);
        var checksum = Checksum(body);

        replyId = (ushort)((replyId + 1) % ushort.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(2), checksum);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(6), replyId);

        var frame = new byte[8 + body.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, TcpMagic1);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), TcpMagic2);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4), body.Length);
        body.CopyTo(frame, 8);
        return frame;
    }

    // Sum of 16-bit words, folded at 65535, then inverted (as the device firmware does it)
    public static ushort Checksum(ReadOnlySpan<byte> data)
    {
        long sum = 0;
        var i = 0;
        for (; i + 1 < data.Length; i += 2)
        {
            sum += data[i] | (data[i + 1] << 8);
            if (sum > ushort.MaxValue)
                sum -= ushort.MaxValue;
        }
        if (i < data.Length)
            sum += data[^1];
        while (sum > ushort.MaxValue)
            sum -= ushort.MaxValue;

        sum = ~sum;
        while (sum < 0)
            sum += ushort.MaxValue;
        return (ushort)sum;
    }

    // The scrambled password the device expects when a Comm Key is set
    public static byte[] MakeCommKey(int key, ushort sessionId, byte ticks = 50)
    {
        uint k = 0;
        for (var i = 0; i < 32; i++)
            k = (key & (1 << i)) != 0 ? (k << 1) | 1 : k << 1;
        k += sessionId;

        var b = BitConverter.GetBytes(k); // little-endian
        b[0] ^= (byte)'Z';
        b[1] ^= (byte)'K';
        b[2] ^= (byte)'S';
        b[3] ^= (byte)'O';
        // swap the two 16-bit halves
        b = [b[2], b[3], b[0], b[1]];
        return [(byte)(b[0] ^ ticks), (byte)(b[1] ^ ticks), ticks, (byte)(b[3] ^ ticks)];
    }

    // Seconds since 2000-01-01 counted with 31-day months, as the device stores them
    public static DateTime DecodeTime(uint t)
    {
        var second = (int)(t % 60); t /= 60;
        var minute = (int)(t % 60); t /= 60;
        var hour = (int)(t % 24); t /= 24;
        var day = (int)(t % 31) + 1; t /= 31;
        var month = (int)(t % 12) + 1; t /= 12;
        return new DateTime((int)t + 2000, month, day, hour, minute, second);
    }

    public static uint EncodeTime(DateTime d)
        => (uint)(((((d.Year % 100) * 12 * 31) + ((d.Month - 1) * 31) + d.Day - 1) * (24 * 60 * 60))
                  + ((d.Hour * 60 + d.Minute) * 60) + d.Second);

    // The log starts with its total size; records are 40, 16 or 8 bytes depending on the firmware
    public static List<DevicePunch> ParseAttendance(byte[] data, int records)
    {
        var punches = new List<DevicePunch>();
        if (data.Length < 4 || records <= 0)
            return punches;

        var total = BinaryPrimitives.ReadInt32LittleEndian(data);
        var recordSize = total / records;
        var body = data.AsSpan(4);

        switch (recordSize)
        {
            case 8: // uid(2) status(1) time(4) punch(1); the device number is the uid
                for (var i = 0; i + 8 <= body.Length; i += 8)
                    Add(BinaryPrimitives.ReadUInt16LittleEndian(body[i..]).ToString(), BinaryPrimitives.ReadUInt32LittleEndian(body[(i + 3)..]));
                break;
            case 16: // user id(4) time(4) status punch reserved(2) workcode(4)
                for (var i = 0; i + 16 <= body.Length; i += 16)
                    Add(BinaryPrimitives.ReadUInt32LittleEndian(body[i..]).ToString(), BinaryPrimitives.ReadUInt32LittleEndian(body[(i + 4)..]));
                break;
            default: // 40 bytes: uid(2) user id(24, text) status(1) time(4) punch(1) space(8)
                var size = recordSize > 0 ? recordSize : 40;
                for (var i = 0; i + 40 <= body.Length; i += size)
                {
                    var idBytes = body.Slice(i + 2, 24);
                    var end = idBytes.IndexOf((byte)0);
                    var userId = Encoding.ASCII.GetString(end < 0 ? idBytes : idBytes[..end]).Trim();
                    Add(userId, BinaryPrimitives.ReadUInt32LittleEndian(body[(i + 27)..]));
                }
                break;
        }
        return punches;

        void Add(string userId, uint time)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return;
            try { punches.Add(new DevicePunch(userId, DecodeTime(time))); }
            catch (ArgumentOutOfRangeException) { /* an empty or broken slot */ }
        }
    }
}

public class ZkException(string message) : Exception(message);
