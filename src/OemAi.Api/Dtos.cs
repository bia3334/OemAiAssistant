using OemAi.Core.Models;

namespace OemAi.Api;

/// <param name="Telemetry">Optional snapshot uploaded by the device itself; absent = the server's own machine.</param>
/// <param name="DeviceId">Optional fleet device whose stored snapshot grounds the answer; wins over <paramref name="Telemetry"/>.</param>
public sealed record AskRequest(
    string Question,
    IReadOnlyList<ChatTurnDto>? History = null,
    DeviceTelemetry? Telemetry = null,
    string? DeviceId = null);
public sealed record ChatTurnDto(string Role, string Content);
public sealed record AskResponse(string Answer, DeviceTelemetry Snapshot, double ElapsedMs);
