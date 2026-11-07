using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HRSystem.BLL.Devices;

// Emkan HR fingerprint agent: runs on a PC inside the company network, reads the ZKTeco devices
// over IP and sends their punches to the online site. Run it and leave it open, or schedule it
// with "--once" in Windows Task Scheduler.

Console.OutputEncoding = Encoding.UTF8;
var folder = AppContext.BaseDirectory;
var settingsPath = Path.Combine(folder, "agentsettings.json");
var statePath = Path.Combine(folder, "agentstate.json");
var logPath = Path.Combine(folder, "agent.log");

void Log(string message)
{
    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}";
    Console.WriteLine(line);
    try { File.AppendAllText(logPath, line + Environment.NewLine); } catch { /* the console still has it */ }
}

if (!File.Exists(settingsPath))
{
    Log($"مفيش ملف إعدادات: {settingsPath}");
    return 1;
}

var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
var settings = JsonSerializer.Deserialize<AgentSettings>(File.ReadAllText(settingsPath), json)!;
if (settings.Devices.Count == 0 || settings.Devices.Any(d => d.ApiKey.StartsWith("PASTE")))
{
    Log("كمّل agentsettings.json الأول: عنوان الموقع، والـ IP ومفتاح الربط لكل جهاز (من شاشة أجهزة البصمة في الموقع)");
    return 1;
}

var state = File.Exists(statePath)
    ? JsonSerializer.Deserialize<Dictionary<string, DateTime>>(File.ReadAllText(statePath)) ?? []
    : new Dictionary<string, DateTime>();
using var http = new HttpClient { BaseAddress = new Uri(settings.SiteUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromMinutes(2) };
var once = args.Contains("--once");

Log($"شغال: {settings.Devices.Count} جهاز، كل {settings.IntervalMinutes} دقيقة → {settings.SiteUrl}");
while (true)
{
    foreach (var device in settings.Devices)
        await SyncAsync(device);

    File.WriteAllText(statePath, JsonSerializer.Serialize(state, json));
    if (once)
        return 0;
    await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, settings.IntervalMinutes)));
}

async Task SyncAsync(DeviceSettings device)
{
    try
    {
        List<DevicePunch> punches;
        using (var client = new ZkClient())
        {
            await client.ConnectAsync(device.Ip, device.Port, device.CommKey, TimeSpan.FromSeconds(10));
            punches = await client.ReadAttendanceAsync();
            await client.DisconnectAsync();
        }

        // Only what came since the last send (one day back, in case of a late clock); the first run sends 31 days
        var since = state.TryGetValue(device.Name, out var last) ? last.AddDays(-1) : DateTime.Today.AddDays(-31);
        var batch = punches.Where(p => p.Time >= since).ToList();
        if (batch.Count == 0)
        {
            Log($"{device.Name}: مفيش بصمات جديدة ({punches.Count} في الجهاز)");
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/devices/punches")
        {
            Content = JsonContent.Create(new { punches = batch.Select(p => new { userId = p.UserId, time = p.Time.ToString("yyyy-MM-ddTHH:mm:ss") }) })
        };
        request.Headers.Add("X-Device-Key", device.ApiKey);
        var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            Log($"{device.Name}: الموقع رفض ({(int)response.StatusCode}) {body}");
            return;
        }

        state[device.Name] = batch.Max(p => p.Time);
        Log($"{device.Name}: اتبعت {batch.Count} بصمة ← {body}");
    }
    catch (Exception ex) when (ex is ZkException or HttpRequestException or IOException or TaskCanceledException or System.Net.Sockets.SocketException)
    {
        Log($"{device.Name}: {ex.Message}");
    }
}

internal class AgentSettings
{
    public string SiteUrl { get; set; } = "";
    public int IntervalMinutes { get; set; } = 10;
    public List<DeviceSettings> Devices { get; set; } = [];
}

internal class DeviceSettings
{
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
    public int Port { get; set; } = 4370;
    public int CommKey { get; set; }
    public string ApiKey { get; set; } = "";
}
