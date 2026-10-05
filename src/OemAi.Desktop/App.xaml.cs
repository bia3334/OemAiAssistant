using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OemAi.Desktop.ViewModels;
using OemAi.Infrastructure;

namespace OemAi.Desktop;

/// <summary>
/// Composition root. Uses the same Generic Host as ASP.NET Core, so config, logging and
/// DI behave identically across desktop and web hosts.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        var builder = Host.CreateApplicationBuilder();

        // Core + Infrastructure (shared with Web API). Set env var Ai__Provider=Mock to run without a model.
        builder.Services.AddOemAiServices(useMockAi: builder.Configuration["Ai:Provider"] == "Mock");
        builder.Services.AddSingleton<MainViewModel>(); // UI layer
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();
        _host.Services.GetRequiredService<MainWindow>().Show();
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync(TimeSpan.FromSeconds(5));
        _host.Dispose(); // disposes MainViewModel → stops telemetry polling
        base.OnExit(e);
    }
}
