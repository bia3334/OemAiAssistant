using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OemAi.Core.Abstractions;
using OemAi.Core.Models;
using OemAi.Core.UseCases;

namespace OemAi.Desktop.ViewModels;

public sealed record ChatMessage(string Author, string Text, TimeSpan? Elapsed = null)
{
    public bool IsUser => Author == MainViewModel.UserAuthor;
}

/// <summary>
/// Presentation logic only — no WPF types, so it is unit-testable and the
/// business flow lives in <see cref="DeviceAssistant"/> (Core), not here.
/// </summary>
public sealed partial class MainViewModel(DeviceAssistant assistant, IDeviceTelemetryService telemetryService)
    : ObservableObject, IDisposable
{
    public const string UserAuthor = "Bạn";
    private static readonly TimeSpan TelemetryInterval = TimeSpan.FromSeconds(2);
    private readonly CancellationTokenSource _lifetime = new();

    [ObservableProperty]
    private DeviceTelemetry? _telemetry;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AskCommand))]
    private string _question = "Kiểm tra tình trạng máy và tối ưu hóa hệ thống";

    private const string Greeting = "Xin chào! Tôi là trợ lý thiết bị. Hãy hỏi về tình trạng máy của bạn.";

    /// <summary>Conversation memory sent to the model (only successful Q/A pairs, not system notices).</summary>
    private readonly List<ChatTurn> _history = [];

    public ObservableCollection<ChatMessage> Messages { get; } = [new("AI", Greeting)];

    /// <summary>
    /// AsyncRelayCommand: disables itself while running (no double-submit), exposes
    /// IsRunning for the loading indicator, and generates AskCancelCommand for free.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAsk), IncludeCancelCommand = true)]
    private async Task AskAsync(CancellationToken cancellationToken)
    {
        var question = Question.Trim();
        Messages.Add(new(UserAuthor, question));
        Question = string.Empty;

        try
        {
            var reply = await assistant.AskAsync(question, _history, cancellationToken);
            Telemetry = reply.Snapshot; // show exactly what the AI reasoned over
            Messages.Add(new("AI", reply.Answer, reply.Elapsed));
            _history.Add(new(ChatRole.User, question));
            _history.Add(new(ChatRole.Assistant, reply.Answer));
        }
        catch (OperationCanceledException)
        {
            Messages.Add(new("Hệ thống", "Đã huỷ yêu cầu."));
        }
        catch (Exception ex)
        {
            Messages.Add(new("Hệ thống", $"Lỗi khi gọi AI: {ex.Message}"));
        }
    }

    private bool CanAsk() => !string.IsNullOrWhiteSpace(Question);

    [RelayCommand]
    private void NewChat()
    {
        AskCancelCommand.Execute(null);
        _history.Clear();
        Messages.Clear();
        Messages.Add(new("AI", Greeting));
    }

    /// <summary>Polls telemetry until the VM is disposed. Continuations resume on the UI thread.</summary>
    public async Task StartMonitoringAsync()
    {
        using var timer = new PeriodicTimer(TelemetryInterval);
        try
        {
            do
            {
                Telemetry = await telemetryService.GetSnapshotAsync(_lifetime.Token);
            }
            while (await timer.WaitForNextTickAsync(_lifetime.Token));
        }
        catch (OperationCanceledException)
        {
            // app shutting down
        }
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
