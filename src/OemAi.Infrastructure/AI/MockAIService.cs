using System.Text;
using OemAi.Core.Abstractions;
using OemAi.Core.Models;

namespace OemAi.Infrastructure.AI;

/// <summary>
/// Deterministic rule-based "AI" so the PoC runs offline with no keys or model download.
/// ponytail: rules, not a model — replace with an OpenAI-compatible HttpClient (Foundry Local,
/// Azure OpenAI) or Semantic Kernel by registering another IAIService; nothing else changes.
/// </summary>
public sealed class MockAIService : IAIService
{
    private static readonly TimeSpan SimulatedLatency = TimeSpan.FromMilliseconds(1500);

    public async Task<string> CompleteAsync(AiPrompt prompt, CancellationToken cancellationToken = default)
    {
        await Task.Delay(SimulatedLatency, cancellationToken); // model inference time; honours Cancel

        var t = prompt.Context;
        var findings = new List<string>();
        var actions = new List<string>();

        if (t.CpuUsagePercent >= 80)
        {
            findings.Add($"CPU đang tải cao ({t.CpuUsagePercent:F0}%).");
            actions.Add("Mở Task Manager → sắp xếp theo CPU, đóng tiến trình nền không cần thiết.");
        }
        if (t.RamUsagePercent >= 85)
        {
            findings.Add($"RAM gần đầy ({t.RamUsedGb:F1}/{t.RamTotalGb:F1} GB).");
            actions.Add("Đóng bớt tab trình duyệt, tắt ứng dụng khởi động cùng Windows (Startup apps).");
        }
        if (t.BatteryPercent <= 20 && !t.IsCharging)
        {
            findings.Add($"Pin thấp ({t.BatteryPercent}%) và không cắm sạc.");
            actions.Add("Bật Energy Saver và giảm độ sáng màn hình; cắm sạc sớm.");
        }

        var sb = new StringBuilder()
            .AppendLine($"Phân tích thiết bị {t.DeviceModel}:")
            .AppendLine($"• CPU {t.CpuUsagePercent:F0}% · RAM {t.RamUsagePercent:F0}% · Pin {t.BatteryPercent}%{(t.IsCharging ? " (đang sạc)" : "")}")
            .AppendLine();

        if (findings.Count == 0)
        {
            sb.AppendLine("✅ Hệ thống đang ở trạng thái tốt, không phát hiện điểm nghẽn.")
              .AppendLine("Gợi ý duy trì: cập nhật driver OEM và Windows Update định kỳ, chạy Storage Sense hàng tháng.");
        }
        else
        {
            sb.AppendLine("⚠️ Phát hiện:");
            findings.ForEach(f => sb.AppendLine($"  - {f}"));
            sb.AppendLine().AppendLine("🔧 Đề xuất tối ưu:");
            for (var i = 0; i < actions.Count; i++) sb.AppendLine($"  {i + 1}. {actions[i]}");
        }

        return sb.ToString().TrimEnd();
    }
}
