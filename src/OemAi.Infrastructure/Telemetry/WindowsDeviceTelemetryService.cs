using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using OemAi.Core.Abstractions;
using OemAi.Core.Models;

namespace OemAi.Infrastructure.Telemetry;

/// <summary>
/// Real telemetry from the local Windows machine via the BIOS registry key and Win32 APIs
/// (GetSystemTimes, GlobalMemoryStatusEx, GetSystemPowerStatus) — no WMI, no extra packages.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDeviceTelemetryService : IDeviceTelemetryService
{
    private const string BiosKey = @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\BIOS";
    private const double BytesPerGb = 1024d * 1024 * 1024;

    private readonly string _manufacturer = ReadBios("SystemManufacturer");
    private readonly string _model = ReadBios("SystemProductName");
    private readonly object _gate = new();
    private CpuTimes? _previous;

    public async Task<DeviceTelemetry> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        // CPU % is a delta between two samples; on the very first call take a short baseline.
        if (_previous is null)
        {
            lock (_gate) _previous ??= CpuTimes.Read();
            await Task.Delay(250, cancellationToken);
        }

        double cpu;
        lock (_gate)
        {
            var current = CpuTimes.Read();
            cpu = current.UsagePercentSince(_previous!.Value);
            _previous = current;
        }

        var memory = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref memory)) throw new InvalidOperationException("GlobalMemoryStatusEx failed.");

        GetSystemPowerStatus(out var power);
        // 255 = unknown / no battery (desktop): report full and on AC.
        var hasBattery = power.BatteryLifePercent <= 100;

        return new DeviceTelemetry(
            Manufacturer: _manufacturer,
            DeviceModel: _model,
            OsVersion: OsName,
            CpuUsagePercent: Math.Round(cpu, 1),
            RamUsedGb: Math.Round((memory.TotalPhys - memory.AvailPhys) / BytesPerGb, 1),
            RamTotalGb: Math.Round(memory.TotalPhys / BytesPerGb, 1),
            BatteryPercent: hasBattery ? power.BatteryLifePercent : 100,
            IsCharging: !hasBattery || power.ACLineStatus == 1,
            CapturedAt: DateTimeOffset.Now);
    }

    // Windows 11 still reports version 10.0; it is distinguished by build >= 22000.
    private static readonly string OsName =
        $"Windows {(Environment.OSVersion.Version.Build >= 22000 ? "11" : "10")} (build {Environment.OSVersion.Version.Build})";

    private static string ReadBios(string name) =>
        (Registry.GetValue(BiosKey, name, null) as string)?.Trim() is { Length: > 0 } value ? value : "Unknown";

    private readonly record struct CpuTimes(long Idle, long Kernel, long User)
    {
        public static CpuTimes Read() =>
            GetSystemTimes(out var idle, out var kernel, out var user)
                ? new(idle, kernel, user)
                : throw new InvalidOperationException("GetSystemTimes failed.");

        // Kernel time already includes idle time.
        public double UsagePercentSince(CpuTimes previous)
        {
            var total = Kernel - previous.Kernel + (User - previous.User);
            var busy = total - (Idle - previous.Idle);
            return total <= 0 ? 0 : Math.Clamp(busy * 100d / total, 0, 100);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
}
