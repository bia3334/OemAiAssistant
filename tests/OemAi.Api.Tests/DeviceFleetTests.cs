using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OemAi.Core.Abstractions;
using OemAi.Core.Models;

namespace OemAi.Api.Tests;

public class DeviceFleetTests
{
    private const string DeviceId = "CONTOSO-Y-001";

    private static readonly DeviceTelemetry Uploaded = new(
        "Contoso OEM", "Fleet Model Y", "Windows 11", CpuUsagePercent: 35, RamUsedGb: 6, RamTotalGb: 16,
        BatteryPercent: 70, IsCharging: false, CapturedAt: DateTimeOffset.UnixEpoch);

    private sealed class RecordingAi : IAIService
    {
        public AiPrompt? Received;
        public Task<string> CompleteAsync(AiPrompt prompt, CancellationToken ct = default)
        {
            Received = prompt;
            return Task.FromResult("ok");
        }
    }

    /// <summary>Real API pipeline (no Demo:Fleet, so the store starts empty) with a fake AI engine.</summary>
    private static WebApplicationFactory<Program> CreateApp(RecordingAi ai) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureTestServices(services => services.AddSingleton<IAIService>(ai)));

    private static Task<HttpResponseMessage> Ingest(HttpClient client, string deviceId, DeviceTelemetry telemetry) =>
        client.PostAsJsonAsync($"/api/devices/{Uri.EscapeDataString(deviceId)}/telemetry", telemetry);

    [Fact]
    public async Task Ingest_ValidTelemetry_IsListedAsRealDevice()
    {
        using var app = CreateApp(new RecordingAi());
        var client = app.CreateClient();

        var ingest = await Ingest(client, DeviceId, Uploaded);
        var devices = await client.GetFromJsonAsync<List<DeviceRecord>>("/api/devices");

        Assert.Equal(HttpStatusCode.NoContent, ingest.StatusCode);
        var device = Assert.Single(devices!, d => d.DeviceId == DeviceId);
        Assert.False(device.IsSimulated);
        Assert.Equal("Fleet Model Y", device.Telemetry.DeviceModel);
    }

    public static TheoryData<string> MalformedDeviceIds => ["bad id!", new string('a', 65), "dev\n"];

    [Theory]
    [MemberData(nameof(MalformedDeviceIds))]
    public async Task Ingest_RejectsMalformedDeviceId(string deviceId)
    {
        using var app = CreateApp(new RecordingAi());

        var response = await Ingest(app.CreateClient(), deviceId, Uploaded);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ask_WithDeviceId_GroundsOnStoredSnapshotOverSubmittedTelemetry()
    {
        var ai = new RecordingAi();
        using var app = CreateApp(ai);
        var client = app.CreateClient();
        await Ingest(client, DeviceId, Uploaded);

        var response = await client.PostAsJsonAsync("/api/assistant/ask",
            new AskRequest("Máy có ổn không?", Telemetry: Uploaded with { DeviceModel = "Other Model" }, DeviceId: DeviceId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Fleet Model Y", ai.Received!.Context.DeviceModel);
    }

    [Fact]
    public async Task Ask_WithUnknownDeviceId_IsNotFound()
    {
        var ai = new RecordingAi();
        using var app = CreateApp(ai);

        var response = await app.CreateClient().PostAsJsonAsync("/api/assistant/ask",
            new AskRequest("Máy có ổn không?", DeviceId: "NO-SUCH-DEVICE"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(ai.Received);
    }
}
