using Microsoft.Extensions.DependencyInjection;
using OemAi.Core.Abstractions;
using OemAi.Core.UseCases;
using OemAi.Infrastructure.AI;
using OemAi.Infrastructure.Telemetry;

namespace OemAi.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Single composition entry point shared by every host: WPF's Generic Host today,
    /// ASP.NET Core's WebApplicationBuilder tomorrow — both expose IServiceCollection.
    /// </summary>
    /// <param name="useMockAi">true = offline rule engine (CI, demo without model); false = on-device LLM via Foundry Local.</param>
    public static IServiceCollection AddOemAiServices(this IServiceCollection services, bool useMockAi = false)
    {
        // Real hardware on Windows; simulated elsewhere (Linux container, CI, cloud demo).
        if (OperatingSystem.IsWindows())
            services.AddSingleton<IDeviceTelemetryService, WindowsDeviceTelemetryService>();
        else
            services.AddSingleton<IDeviceTelemetryService, MockDeviceTelemetryService>();

        if (useMockAi)
            services.AddSingleton<IAIService, MockAIService>();
        else
            services.AddSingleton<IAIService, FoundryLocalAIService>();
        services.AddTransient<DeviceAssistant>();
        return services;
    }
}
