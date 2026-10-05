using OemAi.Core.Abstractions;
using OemAi.Core.Models;

namespace OemAi.Api;

/// <summary>
/// Spells out rule-based alerts in the system prompt before the model sees it. Small on-device models
/// misjudge raw numbers (e.g. call 94% RAM "normal"); stating the alerts keeps the AI's answer consistent
/// with the dashboard badges. Thresholds mirror getIssues() in wwwroot/index.html — keep them in sync.
/// </summary>
public sealed class AlertingAIService(IAIService inner) : IAIService
{
    public Task<string> CompleteAsync(AiPrompt prompt, CancellationToken cancellationToken = default) =>
        inner.CompleteAsync(prompt with { SystemPrompt = $"{prompt.SystemPrompt}\n\n{DescribeAlerts(prompt.Context)}" }, cancellationToken);

    public static string DescribeAlerts(DeviceTelemetry t)
    {
        var alerts = new List<string>();
        if (t.CpuUsagePercent >= 85) alerts.Add($"High CPU load: {t.CpuUsagePercent:F0}% (threshold 85%).");
        if (t.RamUsagePercent >= 90) alerts.Add($"Memory pressure: RAM {t.RamUsagePercent:F0}% used (threshold 90%).");
        if (t.BatteryPercent <= 20 && !t.IsCharging) alerts.Add($"Low battery: {t.BatteryPercent}% and not charging (threshold 20%).");

        return alerts.Count == 0
            ? "[Alerts] None: all metrics are within normal ranges."
            : "[Alerts: real problems, state them first when the user asks about this device]\n- " + string.Join("\n- ", alerts);
    }
}
