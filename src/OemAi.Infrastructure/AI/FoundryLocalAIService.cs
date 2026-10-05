using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OemAi.Core.Abstractions;
using OemAi.Core.Models;

namespace OemAi.Infrastructure.AI;

/// <summary>
/// On-device LLM through Foundry Local's OpenAI-compatible endpoint (runs on NPU/GPU/CPU, fully offline).
/// The endpoint port is assigned at daemon start, so it is discovered via `foundry server status -o json`.
/// Pointing the same request at Azure OpenAI / OpenAI only needs a different base URL + auth header.
/// </summary>
public sealed class FoundryLocalAIService : IAIService
{
    /// <summary>Loaded when no model is in memory; Foundry picks the best variant (NPU/GPU/CPU) for the machine.</summary>
    public const string DefaultModelAlias = "phi-4-mini";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private readonly SemaphoreSlim _discoverLock = new(1, 1);
    private Endpoint? _endpoint;

    public async Task<string> CompleteAsync(AiPrompt prompt, CancellationToken cancellationToken = default)
    {
        var endpoint = await GetEndpointAsync(cancellationToken);
        // OpenAI chat format: system → prior turns (memory) → current question.
        ChatMessageDto[] messages =
        [
            new("system", prompt.SystemPrompt),
            .. prompt.History.Select(t => new ChatMessageDto(t.Role == ChatRole.User ? "user" : "assistant", t.Content)),
            new("user", prompt.UserPrompt),
        ];
        var request = new ChatRequest(
            endpoint.ModelId,
            messages,
            Temperature: 0.3,
            MaxTokens: 700);

        using var response = await _http.PostAsJsonAsync(new Uri(endpoint.BaseUrl, "v1/chat/completions"), request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _endpoint = null; // daemon may have restarted on a new port or unloaded the model
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Foundry Local trả về {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
        return result?.Choices.FirstOrDefault()?.Message.Content?.Trim()
               ?? throw new InvalidOperationException("Foundry Local không trả về nội dung.");
    }

    private async Task<Endpoint> GetEndpointAsync(CancellationToken cancellationToken)
    {
        if (_endpoint is not null) return _endpoint;

        await _discoverLock.WaitAsync(cancellationToken);
        try
        {
            if (_endpoint is not null) return _endpoint;

            var status = await ReadStatusAsync(cancellationToken);
            if (!status.Running)
            {
                await RunFoundryAsync("server start", cancellationToken);
                status = await ReadStatusAsync(cancellationToken);
            }

            var url = status.WebUrls.FirstOrDefault()
                      ?? throw new InvalidOperationException("Foundry Local đang không chạy. Hãy chạy: foundry server start");
            var baseUrl = new Uri(url.TrimEnd('/') + "/");

            // Use whatever model the user loaded (e.g. a GPU variant); otherwise load the default alias.
            // (/v1/models also lists cached-but-unloaded models, so ask the CLI for what is in memory.)
            var modelId = await GetLoadedModelAsync(cancellationToken);
            if (modelId is null)
            {
                await RunFoundryAsync($"model load {DefaultModelAlias}", cancellationToken);
                modelId = await GetLoadedModelAsync(cancellationToken)
                          ?? throw new InvalidOperationException($"Không load được model. Hãy chạy: foundry model load {DefaultModelAlias}");
            }

            return _endpoint = new Endpoint(baseUrl, modelId);
        }
        finally
        {
            _discoverLock.Release();
        }
    }

    private static async Task<string?> GetLoadedModelAsync(CancellationToken cancellationToken)
    {
        var json = await RunFoundryAsync("model list --loaded -o json", cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<ModelList>(json, WebJson)?.Models?.FirstOrDefault()?.Id;
        }
        catch (JsonException)
        {
            return null; // CLI prints a plain-text note when nothing is loaded
        }
    }

    private sealed record Endpoint(Uri BaseUrl, string ModelId);

    private sealed record ModelList(ModelInfo[] Models);

    private sealed record ModelInfo(string Id);

    private static async Task<ServerStatus> ReadStatusAsync(CancellationToken cancellationToken) =>
        JsonSerializer.Deserialize<ServerStatus>(await RunFoundryAsync("server status -o json", cancellationToken),
            WebJson) ?? new ServerStatus(false, []);

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static async Task<string> RunFoundryAsync(string arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo("foundry", arguments)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Process process;
        try
        {
            process = Process.Start(psi)!;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException(
                "Chưa cài Foundry Local. Cài bằng: winget install Microsoft.FoundryLocal");
        }

        using (process)
        {
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return output;
        }
    }

    private sealed record ServerStatus(bool Running, string[] WebUrls);

    private sealed record ChatRequest(
        string Model,
        ChatMessageDto[] Messages,
        double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record ChatMessageDto(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content);

    private sealed record ChatResponse([property: JsonPropertyName("choices")] Choice[] Choices);

    private sealed record Choice([property: JsonPropertyName("message")] ChatMessageDto Message);
}
