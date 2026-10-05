using System.Diagnostics;
using System.Globalization;
using OemAi.Core.Abstractions;
using OemAi.Core.Models;

namespace OemAi.Core.UseCases;

/// <summary>
/// Application use case: "ask the assistant about this device".
/// Pure orchestration over two ports — no UI, no HTTP, no WPF — so the same class
/// backs the desktop ViewModel today and an ASP.NET Core endpoint tomorrow.
/// </summary>
public sealed class DeviceAssistant(IDeviceTelemetryService telemetry, IAIService ai)
{
    /// <summary>
    /// Last N messages sent back as memory. Small on-device models have short context windows
    /// (phi-4-mini ≈ 4K tokens), so older turns are dropped.
    /// </summary>
    public const int MaxHistoryMessages = 8;

    /// <param name="history">Prior turns, owned by the caller (ViewModel / HTTP client) — this use case stays stateless.</param>
    public async Task<AssistantReply> AskAsync(
        string question,
        IReadOnlyList<ChatTurn>? history = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        var stopwatch = Stopwatch.StartNew();
        var snapshot = await telemetry.GetSnapshotAsync(cancellationToken);
        var recent = (history ?? []).TakeLast(MaxHistoryMessages).ToList();
        // Telemetry is background context in the system message; the user turn is only the question,
        // so small models answer what was asked instead of always summarising the numbers.
        var prompt = new AiPrompt(BuildSystemPrompt(snapshot), question.Trim(), snapshot, recent);
        var answer = await ai.CompleteAsync(prompt, cancellationToken);

        return new AssistantReply(answer, snapshot, stopwatch.Elapsed);
    }

    /// <summary>Role + live telemetry as optional grounding (prompt augmentation).</summary>
    public static string BuildSystemPrompt(DeviceTelemetry t) =>
        string.Create(CultureInfo.InvariantCulture, $"""
            You are a helpful assistant built into an OEM Windows laptop.
            Answer the user's actual question directly. Only use the device telemetry below
            when the question is about this computer (health, performance, battery, optimisation);
            for anything else, ignore it and do not mention it.
            Use the earlier conversation to answer follow-up questions; you may repeat anything
            the user told you before (such as their name). In the user's messages, "I"/"me"/"tôi"
            refers to the user, not to you.
            Be concise (under 150 words). Always reply in the same language as the user's question
            (e.g. Vietnamese question → Vietnamese answer).

            [Device telemetry @ {t.CapturedAt:HH:mm:ss}]
            Manufacturer: {t.Manufacturer}
            Model: {t.DeviceModel}
            OS: {t.OsVersion}
            CPU usage: {t.CpuUsagePercent:F0}%
            RAM: {t.RamUsedGb:F1}/{t.RamTotalGb:F1} GB ({t.RamUsagePercent:F0}%)
            Battery: {t.BatteryPercent}% ({(t.IsCharging ? "plugged in" : "on battery")})
            """);
}
