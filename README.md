# OEM AI Assistant

AI assistant for OEM Windows laptops, in two phases:

- **Phase 1 — Desktop:** WPF app (.NET 8) that reads the laptop's real telemetry (CPU, RAM, battery)
  and answers questions with an on-device LLM ([Foundry Local](https://github.com/microsoft/Foundry-Local), Phi-4-mini).
- **Phase 2 — Web/Cloud demo:** fleet console for OEM partners. Each laptop runs an agent that uploads
  telemetry to an ASP.NET Core API (.NET 10); partners see the whole fleet and ask the AI about any device.
  UI in Japanese by default, with an English switch.

```
[Laptop] OemAi.Agent ──telemetry──► OemAi.Api ──► AI (Foundry Local / Mock)
         OemAi.Desktop (WPF)            └ Fleet Console at /
              └── both reuse OemAi.Core + OemAi.Infrastructure
```

## Run

Requires the .NET 10 SDK (Desktop/Core also need the .NET 8 runtime) and Windows for real telemetry.

```powershell
.\demo.ps1          # build, start API + agent, open http://localhost:5000
.\demo.ps1 -Mock    # no model needed: offline rule-based AI
dotnet run --project src/OemAi.Desktop   # Phase 1 desktop app
dotnet test OemAiAssistant.slnx
```

Real AI: install Foundry Local (`winget install Microsoft.FoundryLocal`) and load a model
(`foundry model load phi-4-mini`). Optional logo: drop `src/OemAi.Api/wwwroot/logo.svg`.

## Projects

| Project | Role |
|---|---|
| `OemAi.Core` | Ports (`IDeviceTelemetryService`, `IAIService`) + `DeviceAssistant` use case |
| `OemAi.Infrastructure` | Windows telemetry (Win32/BIOS), Foundry Local client, mocks |
| `OemAi.Desktop` | WPF + MVVM front end (Phase 1) |
| `OemAi.Api` | Minimal API, in-memory device store, simulated fleet, Fleet Console (Phase 2) |
| `OemAi.Agent` | Console agent that uploads a laptop's telemetry |

Docs: [roadmap](docs/roadmap.md) · [demo script (VI)](docs/demo.md) · [guide (VI)](docs/huong-dan.html)
