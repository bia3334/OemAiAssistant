using OemAi.Core.Abstractions;
using OemAi.Core.Models;
using OemAi.Core.UseCases;

namespace OemAi.Core.Tests;

public class DeviceAssistantTests
{
    private static readonly DeviceTelemetry Sample = new(
        "Contoso OEM", "ProBook 14", "Windows 11", CpuUsagePercent: 92.4, RamUsedGb: 14.5, RamTotalGb: 16,
        BatteryPercent: 12, IsCharging: false, CapturedAt: DateTimeOffset.UnixEpoch);

    private sealed class FixedTelemetry : IDeviceTelemetryService
    {
        public Task<DeviceTelemetry> GetSnapshotAsync(CancellationToken ct = default) => Task.FromResult(Sample);
    }

    private sealed class RecordingAi : IAIService
    {
        public AiPrompt? Received;
        public Task<string> CompleteAsync(AiPrompt prompt, CancellationToken ct = default)
        {
            Received = prompt;
            return Task.FromResult("ok");
        }
    }

    [Fact]
    public async Task AskAsync_GroundsQuestionWithLiveTelemetry()
    {
        var ai = new RecordingAi();
        var reply = await new DeviceAssistant(new FixedTelemetry(), ai).AskAsync("  Máy có ổn không?  ");

        Assert.Equal("ok", reply.Answer);
        Assert.Same(Sample, reply.Snapshot);
        Assert.Same(Sample, ai.Received!.Context);
        Assert.Contains("CPU usage: 92%", ai.Received.SystemPrompt);
        Assert.Contains("RAM: 14.5/16.0 GB (91%)", ai.Received.SystemPrompt);
        Assert.Contains("Battery: 12% (on battery)", ai.Received.SystemPrompt);
        Assert.Equal("Máy có ổn không?", ai.Received.UserPrompt); // question only, no telemetry
    }

    [Fact]
    public async Task AskAsync_SendsOnlyMostRecentHistory()
    {
        var ai = new RecordingAi();
        var history = Enumerable.Range(1, 12)
            .Select(i => new ChatTurn(i % 2 == 1 ? ChatRole.User : ChatRole.Assistant, $"m{i}"))
            .ToList();

        await new DeviceAssistant(new FixedTelemetry(), ai).AskAsync("tiếp?", history);

        Assert.Equal(DeviceAssistant.MaxHistoryMessages, ai.Received!.History.Count);
        Assert.Equal("m5", ai.Received.History[0].Content);   // oldest kept
        Assert.Equal("m12", ai.Received.History[^1].Content); // newest, order preserved
        Assert.Equal("tiếp?", ai.Received.UserPrompt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AskAsync_RejectsBlankQuestion(string question)
    {
        var sut = new DeviceAssistant(new FixedTelemetry(), new RecordingAi());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.AskAsync(question));
    }
}
