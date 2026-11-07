using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HRSystem.BLL.Devices;

namespace HRSystem.Tests;

public class ZkClientTests
{
    [Fact]
    public void Time_round_trips_through_the_device_format()
    {
        var time = new DateTime(2026, 11, 3, 8, 57, 41);
        Assert.Equal(time, ZkClient.DecodeTime(ZkClient.EncodeTime(time)));
    }

    [Fact]
    public void Checksum_of_a_connect_header_matches_the_device_algorithm()
    {
        // CMD_CONNECT, checksum 0, session 0, reply 65534: words 1000 + 0 + 0 + 65534 = 66534 → 999 → ~999
        var header = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(header, 1000);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6), 65534);
        Assert.Equal(64536 - 1, ZkClient.Checksum(header));
    }

    [Fact]
    public void Packet_carries_the_tcp_prefix_and_the_next_reply_id()
    {
        var packet = ZkClient.BuildPacket(1000, [], 0, 65534);
        Assert.Equal(16, packet.Length);
        Assert.Equal(0x5050, BinaryPrimitives.ReadUInt16LittleEndian(packet));
        Assert.Equal(0x7D82, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)));
        Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(4)));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(14))); // 65534 + 1 wraps to 0
    }

    [Fact]
    public void Comm_key_of_zero_still_scrambles_the_session()
        => Assert.Equal(4, ZkClient.MakeCommKey(0, 1234).Length);

    [Theory]
    [InlineData(40)]
    [InlineData(16)]
    [InlineData(8)]
    public void Attendance_records_of_every_size_are_read(int size)
    {
        var punches = new[] { new DevicePunch("7", new DateTime(2026, 11, 1, 8, 55, 0)), new DevicePunch("12", new DateTime(2026, 11, 1, 17, 3, 9)) };
        var data = FakeZkDevice.AttendanceBlob(punches, size);

        var read = ZkClient.ParseAttendance(data, punches.Length);

        Assert.Equal(punches, read);
    }

    [Fact]
    public async Task Reads_the_log_from_a_device_over_tcp()
    {
        var punches = Enumerable.Range(0, 3000)
            .Select(i => new DevicePunch((i % 40 + 1).ToString(), new DateTime(2026, 10, 1, 8, 0, 0).AddMinutes(i * 7)))
            .ToList();
        using var device = new FakeZkDevice(punches, commKey: 1234);

        using var client = new ZkClient();
        await client.ConnectAsync("127.0.0.1", device.Port, 1234, TimeSpan.FromSeconds(5));
        var read = await client.ReadAttendanceAsync();
        await client.DisconnectAsync();

        Assert.Equal(punches, read);
    }

    [Fact]
    public async Task A_wrong_comm_key_is_refused_with_a_clear_message()
    {
        using var device = new FakeZkDevice([], commKey: 1234);
        using var client = new ZkClient();
        var ex = await Assert.ThrowsAsync<ZkException>(() => client.ConnectAsync("127.0.0.1", device.Port, 9999, TimeSpan.FromSeconds(5)));
        Assert.Contains("Comm Key", ex.Message);
    }
}

// A stand-in ZKTeco device on localhost: answers connect / auth / sizes / buffered reads the way
// the firmware does, sending large logs as PREPARE_DATA, DATA frames, ACK_OK.
public sealed class FakeZkDevice : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly byte[] _log;
    private readonly int _records;
    private readonly int _commKey;
    private readonly CancellationTokenSource _stop = new();

    public FakeZkDevice(IReadOnlyList<DevicePunch> punches, int commKey = 0, int port = 0)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _log = AttendanceBlob(punches, 40);
        _records = punches.Count;
        _commKey = commKey;
        _listener.Start();
        _ = Task.Run(ServeAsync);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public static byte[] AttendanceBlob(IEnumerable<DevicePunch> punches, int size)
    {
        using var body = new MemoryStream();
        foreach (var p in punches)
        {
            var record = new byte[size];
            var time = ZkClient.EncodeTime(p.Time);
            switch (size)
            {
                case 8:
                    BinaryPrimitives.WriteUInt16LittleEndian(record, ushort.Parse(p.UserId));
                    BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(3), time);
                    break;
                case 16:
                    BinaryPrimitives.WriteUInt32LittleEndian(record, uint.Parse(p.UserId));
                    BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(4), time);
                    break;
                default:
                    Encoding.ASCII.GetBytes(p.UserId).CopyTo(record, 2);
                    BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(27), time);
                    break;
            }
            body.Write(record);
        }
        var blob = new byte[4 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(blob, (int)body.Length);
        body.ToArray().CopyTo(blob, 4);
        return blob;
    }

    private async Task ServeAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using var tcp = await _listener.AcceptTcpClientAsync(_stop.Token);
                await HandleAsync(tcp.GetStream());
            }
        }
        catch (Exception) when (_stop.IsCancellationRequested) { }
        catch (IOException) { }
    }

    private async Task HandleAsync(NetworkStream stream)
    {
        const ushort session = 4321;
        var authorized = _commKey == 0;
        while (true)
        {
            var top = new byte[8];
            if (await stream.ReadAtLeastAsync(top, 8, throwOnEndOfStream: false) < 8)
                return;
            var body = new byte[BinaryPrimitives.ReadInt32LittleEndian(top.AsSpan(4))];
            await stream.ReadExactlyAsync(body);
            var command = BinaryPrimitives.ReadUInt16LittleEndian(body);
            var reply = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(6));
            var payload = body[8..];

            Task Send(ushort code, byte[]? data = null) => stream.WriteAsync(Frame(code, session, reply, data ?? [])).AsTask();

            switch (command)
            {
                case 1000: await Send(authorized ? (ushort)2000 : (ushort)2005); break;
                case 1102:
                    authorized = payload.SequenceEqual(ZkClient.MakeCommKey(_commKey, session));
                    await Send(authorized ? (ushort)2000 : (ushort)2005);
                    break;
                case 50:
                    var sizes = new byte[80];
                    BinaryPrimitives.WriteInt32LittleEndian(sizes.AsSpan(32), _records);
                    await Send(2000, sizes);
                    break;
                case 1503:
                    var info = new byte[5];
                    BinaryPrimitives.WriteInt32LittleEndian(info.AsSpan(1), _log.Length);
                    await Send(2000, info);
                    break;
                case 1504:
                    var start = BinaryPrimitives.ReadInt32LittleEndian(payload);
                    var size = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4));
                    await Send(1500, BitConverter.GetBytes(size));
                    // the chunk split over several DATA frames, like the firmware does
                    for (var offset = 0; offset < size; offset += 1024)
                        await Send(1501, _log.AsSpan(start + offset, Math.Min(1024, size - offset)).ToArray());
                    await Send(2000);
                    break;
                case 1001: await Send(2000); return;
                default: await Send(2000); break;
            }
        }
    }

    private static byte[] Frame(ushort code, ushort session, ushort reply, byte[] data)
    {
        var frame = new byte[16 + data.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, 0x5050);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 0x7D82);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4), 8 + data.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(8), code);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(12), session);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(14), reply);
        data.CopyTo(frame, 16);
        return frame;
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
    }
}
