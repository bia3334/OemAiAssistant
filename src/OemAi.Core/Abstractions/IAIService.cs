using OemAi.Core.Models;

namespace OemAi.Core.Abstractions;

/// <summary>Port to any AI engine: local mock, on-device model (Foundry Local / Phi Silica), or cloud LLM.</summary>
public interface IAIService
{
    Task<string> CompleteAsync(AiPrompt prompt, CancellationToken cancellationToken = default);
}
