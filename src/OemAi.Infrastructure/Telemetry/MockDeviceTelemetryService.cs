using System.Runtime.InteropServices;
using OemAi.Core.Abstractions;
using OemAi.Core.Models;

namespace OemAi.Infrastructure.Telemetry;

/// <summary>
/// Simulated OEM telemetry. OS and total RAM are real; CPU, RAM pressure and battery
/// follow a bounded random walk so the dashboard looks alive.
/// ponytail: mock values — swap for an OEM SDK / WMI (Win32_Processor, Win32_Battery) implementation.
/// </summary>
public sealed class MockDeviceTelemetryService : IDeviceTelemetryService
{
    private readonly object _gate = new();
    private readonly Random _random = Random.Shared;
    private readonly double _ramTotalGb;
    private double _cpu = 35, _ramUsedRatio = 0.55;
    private double _battery = 78;
    private bool _charging;

    public MockDeviceTelemetryService()
    {
        var bytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        _ramTotalGb = bytes > 0 ? bytes / 1024d / 1024 / 1024 : 16;
    }

    public async Task<DeviceTelemetry> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken); // simulate driver / SDK I/O latency

        lock (_gate)
        {
            _cpu = Math.Clamp(_cpu + (_random.NextDouble() - 0.45) * 18, 3, 99);
            _ramUsedRatio = Math.Clamp(_ramUsedRatio + (_random.NextDouble() - 0.5) * 0.06, 0.25, 0.97);
            _battery = Math.Clamp(_battery + (_charging ? 0.8 : -0.3), 1, 100);
            if (_battery <= 15) _charging = true;
            if (_battery >= 100) _charging = false;

            return new DeviceTelemetry(
                Manufacturer: "Contoso OEM",
                DeviceModel: "Contoso ProBook 14 AI (Copilot+ PC)",
                OsVersion: RuntimeInformation.OSDescription,
                CpuUsagePercent: Math.Round(_cpu, 1),
                RamUsedGb: Math.Round(_ramTotalGb * _ramUsedRatio, 1),
                RamTotalGb: Math.Round(_ramTotalGb, 1),
                BatteryPercent: (int)Math.Round(_battery),
                IsCharging: _charging,
                CapturedAt: DateTimeOffset.Now);
        }
    }
}
