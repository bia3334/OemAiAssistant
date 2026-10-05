using OemAi.Core.Abstractions;
using OemAi.Core.Models;

namespace OemAi.Api;

/// <summary>
/// Hybrid path: a cloud server can't read the user's hardware, so the device uploads its own
/// snapshot with the question and the use case grounds on that instead of the server machine.
/// </summary>
public sealed class SubmittedTelemetry(DeviceTelemetry snapshot) : IDeviceTelemetryService
{
    public const int MaxTextLength = 100;
    public const double MaxRamTotalGb = 1024;

    public Task<DeviceTelemetry> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(snapshot);

    /// <summary>
    /// Trust boundary: these values are interpolated into the system prompt, so text fields reject
    /// control characters (newline-based prompt injection) and numbers must be physically plausible.
    /// Ranges are written as "is inside" so NaN/Infinity (which compare false) are rejected too —
    /// they arrive as JSON strings and would otherwise crash response serialization.
    /// </summary>
    /// <returns>An error message, or null when the snapshot is acceptable.</returns>
    public static string? Validate(DeviceTelemetry t) =>
        CheckText(t.Manufacturer, "manufacturer")
        ?? CheckText(t.DeviceModel, "deviceModel")
        ?? CheckText(t.OsVersion, "osVersion")
        ?? (t.CpuUsagePercent is >= 0 and <= 100 ? null : "telemetry.cpuUsagePercent must be between 0 and 100.")
        ?? (t.BatteryPercent is >= 0 and <= 100 ? null : "telemetry.batteryPercent must be between 0 and 100.")
        ?? (t.RamTotalGb is > 0 and <= MaxRamTotalGb ? null : $"telemetry.ramTotalGb must be greater than 0 and at most {MaxRamTotalGb}.")
        ?? (t.RamUsedGb >= 0 && t.RamUsedGb <= t.RamTotalGb ? null : "telemetry.ramUsedGb must be between 0 and ramTotalGb.");

    private static string? CheckText(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? $"telemetry.{name} is required."
        : value.Length > MaxTextLength ? $"telemetry.{name} must be at most {MaxTextLength} characters."
        : value.Any(char.IsControl) ? $"telemetry.{name} must not contain control characters."
        : null;
}
