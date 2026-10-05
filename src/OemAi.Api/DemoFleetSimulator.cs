using OemAi.Core.Models;

namespace OemAi.Api;

/// <summary>
/// `--Demo:Fleet=true`: a fake OEM fleet so the dashboard has devices without real agents installed.
/// Every tick = base profile + fresh small noise (not a random walk), so each device keeps its
/// story — healthy, CPU-bound, RAM pressure, low battery, offline — for the whole demo.
/// </summary>
public sealed class DemoFleetSimulator(DeviceStore store) : BackgroundService
{
    private const string OsVersion = "Windows 11 (build 26100)";
    private const double BatteryFloor = 5;
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(2);

    private sealed record Profile(
        string Id, string Manufacturer, string Model,
        double Cpu, double RamRatio, double RamTotalGb, double Battery, bool IsCharging,
        double BatteryDrainPerTick = 0);

    private static readonly Profile[] Live =
    [
        new("DELL-LAT7450-001", "Dell Inc.", "Latitude 7450", Cpu: 22, RamRatio: 0.45, RamTotalGb: 16, Battery: 82, IsCharging: true),
        new("HP-EB840-002", "HP", "HP EliteBook 840 G11", Cpu: 92, RamRatio: 0.60, RamTotalGb: 32, Battery: 64, IsCharging: false),
        new("LENOVO-X1C-003", "LENOVO", "ThinkPad X1 Carbon Gen 12", Cpu: 45, RamRatio: 0.94, RamTotalGb: 16, Battery: 71, IsCharging: true),
        new("ASUS-ZB14-004", "ASUSTeK COMPUTER INC.", "Zenbook 14 OLED UX3405", Cpu: 30, RamRatio: 0.55, RamTotalGb: 16, Battery: 16, IsCharging: false, BatteryDrainPerTick: 0.2),
        new("ACER-SG14-005", "Acer", "Swift Go 14", Cpu: 15, RamRatio: 0.40, RamTotalGb: 8, Battery: 95, IsCharging: true),
    ];

    private static readonly Profile Offline =
        new("LENOVO-T14-006", "LENOVO", "ThinkPad T14 Gen 5", Cpu: 10, RamRatio: 0.30, RamTotalGb: 16, Battery: 50, IsCharging: false);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var offlineSince = DateTimeOffset.UtcNow.AddHours(-3);
        store.TryUpsert(Offline.Id, Snapshot(Offline, Offline.Cpu, Offline.RamRatio, Offline.Battery, offlineSince),
            isSimulated: true, offlineSince);

        using var timer = new PeriodicTimer(Tick);
        for (var tick = 0; ; tick++)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var p in Live)
            {
                var battery = Math.Max(BatteryFloor, p.Battery - p.BatteryDrainPerTick * tick);
                store.TryUpsert(p.Id, Snapshot(p, p.Cpu + Noise(5), p.RamRatio + Noise(0.03), battery, now),
                    isSimulated: true, now);
            }
            if (!await timer.WaitForNextTickAsync(stoppingToken))
                return;
        }
    }

    private static double Noise(double amplitude) => (Random.Shared.NextDouble() * 2 - 1) * amplitude;

    private static DeviceTelemetry Snapshot(Profile p, double cpu, double ramRatio, double battery, DateTimeOffset capturedAt) =>
        new(p.Manufacturer, p.Model, OsVersion,
            CpuUsagePercent: Math.Round(Math.Clamp(cpu, 0, 99), 1),
            RamUsedGb: Math.Round(Math.Clamp(ramRatio, 0, 1) * p.RamTotalGb, 1),
            RamTotalGb: p.RamTotalGb,
            BatteryPercent: (int)Math.Round(Math.Clamp(battery, 0, 100)),
            IsCharging: p.IsCharging,
            CapturedAt: capturedAt);
}
