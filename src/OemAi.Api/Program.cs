using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using OemAi.Core.Abstractions;
using OemAi.Core.Models;
using OemAi.Core.UseCases;
using OemAi.Infrastructure;
using OemAi.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Same switch as Desktop: `-- --Ai:Provider=Mock` or env Ai__Provider=Mock runs without a model.
builder.Services.AddOemAiServices(useMockAi: builder.Configuration["Ai:Provider"] == "Mock");
builder.Services.AddSingleton<DeviceStore>();
// `-- --Demo:Fleet=true`: simulated OEM devices for the dashboard demo.
if (builder.Configuration.GetValue<bool>("Demo:Fleet"))
    builder.Services.AddHostedService<DemoFleetSimulator>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Dashboard: wwwroot/index.html at "/"
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/telemetry", (IDeviceTelemetryService telemetry, CancellationToken ct) => telemetry.GetSnapshotAsync(ct))
   .WithTags("Telemetry")
   .WithName("GetTelemetry");

app.MapGet("/api/devices", (DeviceStore devices) => devices.All())
   .WithTags("Devices")
   .WithName("GetDevices");

app.MapPost("/api/devices/{deviceId}/telemetry", IngestTelemetry)
   .WithTags("Devices")
   .WithName("IngestTelemetry");

app.MapPost("/api/assistant/ask", AskAssistant)
   .WithTags("Assistant")
   .WithName("AskAssistant");

await app.RunAsync();

static IResult IngestTelemetry(DeviceStore devices, string deviceId, [FromBody] DeviceTelemetry telemetry)
{
    if (!DeviceIdPattern().IsMatch(deviceId))
        return Results.BadRequest(new { error = "deviceId must be 1-64 characters: letters, digits, '.', '_' or '-'." });
    if (SubmittedTelemetry.Validate(telemetry) is { } telemetryError)
        return Results.BadRequest(new { error = telemetryError });

    return devices.TryUpsert(deviceId, telemetry, isSimulated: false)
        ? Results.NoContent()
        : Results.Json(new { error = $"Device limit ({DeviceStore.MaxDevices}) reached." }, statusCode: StatusCodes.Status429TooManyRequests);
}

static async Task<IResult> AskAssistant(
    IDeviceTelemetryService localTelemetry,
    IAIService ai,
    DeviceStore devices,
    [FromBody] AskRequest request,
    CancellationToken ct)
{
    const int maxQuestionLength = 2000;

    if (string.IsNullOrWhiteSpace(request.Question))
        return Results.BadRequest(new { error = "Question is required." });
    if (request.Question.Length > maxQuestionLength)
        return Results.BadRequest(new { error = $"Question must be at most {maxQuestionLength} characters." });
    if (request.History?.Any(h => h is null || string.IsNullOrEmpty(h.Content)) == true)
        return Results.BadRequest(new { error = "Every history item needs a content." });
    if (request.Telemetry is { } submitted && SubmittedTelemetry.Validate(submitted) is { } telemetryError)
        return Results.BadRequest(new { error = telemetryError });

    // Snapshot source: fleet device's stored upload > snapshot sent with the question (hybrid/cloud) > this server's own machine.
    var device = string.IsNullOrEmpty(request.DeviceId) ? null : devices.Find(request.DeviceId);
    if (device is null && !string.IsNullOrEmpty(request.DeviceId))
        return Results.NotFound(new { error = $"Unknown device '{request.DeviceId}'." });
    IDeviceTelemetryService source =
        device is not null ? new SubmittedTelemetry(device.Telemetry)
        : request.Telemetry is { } t ? new SubmittedTelemetry(t)
        : localTelemetry;

    var history = request.History?.Select(h =>
        new ChatTurn(h.Role == "user" ? ChatRole.User : ChatRole.Assistant, h.Content)
    ).ToList();

    AssistantReply reply;
    try
    {
        reply = await new DeviceAssistant(source, new AlertingAIService(ai)).AskAsync(request.Question, history, ct);
    }
    catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
    {
        // AI engine down (Foundry not installed/running, model not loaded): say so instead of an empty 500.
        return Results.Json(new { error = $"AI engine unavailable: {ex.Message}" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new AskResponse(
        reply.Answer,
        reply.Snapshot,
        reply.Elapsed.TotalMilliseconds));
}

public partial class Program
{
    // \z, not $: .NET's $ also matches before a trailing newline (route values arrive URL-decoded).
    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,64}\z")]
    private static partial Regex DeviceIdPattern();
}
