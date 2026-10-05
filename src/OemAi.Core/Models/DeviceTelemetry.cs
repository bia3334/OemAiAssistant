namespace OemAi.Core.Models;

/// <summary>Immutable snapshot of OEM device health at a point in time.</summary>
public sealed record DeviceTelemetry(
    string Manufacturer,
    string DeviceModel,
    string OsVersion,
    double CpuUsagePercent,
    double RamUsedGb,
    double RamTotalGb,
    int BatteryPercent,
    bool IsCharging,
    DateTimeOffset CapturedAt)
{
    public double RamUsagePercent => RamTotalGb <= 0 ? 0 : RamUsedGb / RamTotalGb * 100;
}
