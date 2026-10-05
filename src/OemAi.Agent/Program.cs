using System.Globalization;
using System.Net.Http.Json;
using OemAi.Core.Abstractions;
using OemAi.Infrastructure.Telemetry;

// Device-side agent: uploads this machine's telemetry to the fleet API every N seconds.
// Usage: dotnet run --project src/OemAi.Agent -- [--server http://host:5000] [--device-id id] [--interval 2]
var server = Arg("--server") ?? "http://localhost:5000";
var deviceId = Arg("--device-id") ?? DefaultDeviceId();
var interval = Math.Max(1, int.Parse(Arg("--interval") ?? "2", CultureInfo.InvariantCulture));

IDeviceTelemetryService telemetry = OperatingSystem.IsWindows()
    ? new WindowsDeviceTelemetryService()
    : new MockDeviceTelemetryService();
using var http = new HttpClient { BaseAddress = new Uri(server.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(5) };
using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var path = $"api/devices/{Uri.EscapeDataString(deviceId)}/telemetry";
Console.WriteLine($"OemAi Agent - device {deviceId} -> {server} (every {interval}s). Ctrl+C to stop.");

try
{
    do
    {
        string result;
        try
        {
            var s = await telemetry.GetSnapshotAsync(cts.Token);
            using var response = await http.PostAsJsonAsync(path, s, cts.Token);
            result = response.IsSuccessStatusCode
                ? $"OK   CPU {s.CpuUsagePercent:0}% | RAM {s.RamUsagePercent:0}% | BAT {s.BatteryPercent}%"
                : $"FAIL {(int)response.StatusCode} {response.StatusCode}";
        }
        catch (Exception ex) when (!cts.IsCancellationRequested)
        {
            // Server down / unreachable / slow: report and retry on the next tick, never crash.
            result = ex switch
            {
                HttpRequestException h => $"FAIL {h.HttpRequestError}",
                TaskCanceledException => "FAIL timeout",
                _ => $"FAIL {ex.Message}",
            };
        }
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"[{DateTime.Now:HH:mm:ss}] {result}"));
    }
    while (await timer.WaitForNextTickAsync(cts.Token));
}
catch (OperationCanceledException) when (cts.IsCancellationRequested)
{
    Console.WriteLine("Agent stopped.");
}

string? Arg(string name) =>
    Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;

// API accepts ^[A-Za-z0-9._-]{1,64}$
static string DefaultDeviceId()
{
    var id = new string(Environment.MachineName
        .Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '-').ToArray());
    return id.Length > 64 ? id[..64] : id;
}
