namespace OemAi.Core.Models;

public enum ChatRole { User, Assistant }

/// <summary>One prior message in the conversation, sent back to the model as memory.</summary>
public sealed record ChatTurn(ChatRole Role, string Content);

/// <summary>
/// Provider-agnostic request to an AI engine. <see cref="Context"/> carries the structured
/// telemetry so engines can ground on data (tool calling, rules) rather than re-parsing text.
/// </summary>
public sealed record AiPrompt(
    string SystemPrompt,
    string UserPrompt,
    DeviceTelemetry Context,
    IReadOnlyList<ChatTurn> History);

/// <summary>Result of one assistant turn, returned to any front end (WPF, Web API, CLI).</summary>
public sealed record AssistantReply(string Answer, DeviceTelemetry Snapshot, TimeSpan Elapsed);
