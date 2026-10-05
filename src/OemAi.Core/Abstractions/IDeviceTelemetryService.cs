using OemAi.Core.Models;

namespace OemAi.Core.Abstractions;

/// <summary>Port to the device's hardware data: OEM SDK, WMI, or telemetry uploaded by a remote device.</summary>
public interface IDeviceTelemetryService
{
    Task<DeviceTelemetry> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
