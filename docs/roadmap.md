# Roadmap & worklog — what's next, and what's shipped

Forward-looking plan up top; the **Shipped** changelog (newest first) at the bottom is
the running worklog — **log finished work there** (one line per change).

Workflow: pick **one ticket at a time** (respect `Depends on:`), implement, then
`dotnet build` (0 warnings) + `dotnet test` green + run the app, and only then move on.
Estimates are story points (Fibonacci 1–13: relative effort + uncertainty, not days).

## Where we are now ✅
- **Desktop PoC runs end-to-end**: WPF (.NET 8) · MVVM (CommunityToolkit) · Clean
  Architecture (`Core` ← `Infrastructure` ← `Desktop`) · Generic Host DI.
- **Real telemetry** of the local machine (BIOS registry + Win32: CPU, RAM, battery).
- **On-device AI** via Foundry Local (Phi-4-mini on the RTX GPU, OpenAI-compatible API),
  telemetry as system-prompt context, 8-message conversation memory, cancel support.
  `Ai__Provider=Mock` switches to the offline rule engine.
- **Phase 2 demo — OEM AI Fleet Console** (Phase 1 code untouched):
  - `OemAi.Api` (ASP.NET Core **.NET 10** + Swashbuckle; Core/Infrastructure stay .NET 8):
    device ingest `POST /api/devices/{id}/telemetry`, `GET /api/devices` (in-memory store),
    `POST /api/assistant/ask` grounded on a chosen `deviceId`, simulated OEM fleet (`--Demo:Fleet=true`),
    Mock AI (`--Ai:Provider=Mock`). Fleet dashboard at `/`, Swagger at `/swagger`.
  - `OemAi.Agent`: console agent on each laptop, uploads real telemetry every 2 s (reuses Infrastructure).
  - One command: `.\demo.ps1 [-Mock] [-Url http://0.0.0.0:5000]` · talk track: [`demo.md`](./demo.md).
- `dotnet build` 0 warnings · `dotnet test` **22/22** (Core 4 · Api 18).
- Guide: [`huong-dan.html`](./huong-dan.html).

## Next up — Web/Cloud for OEM partners

**Target architecture: hybrid.** A server cannot read the user's hardware, so the
Windows app stays on the device (telemetry + local AI) and a cloud API adds bigger
models, fleet data and a partner dashboard.

```
[OEM device] Windows app/agent ──telemetry, questions──► [Cloud] ASP.NET Core API ──► Azure OpenAI
             ├ reads hardware (WindowsDeviceTelemetryService)        ├ stores telemetry / history
             └ local AI (Foundry) — simple questions, offline       └ web dashboard for partners
```

| Phase | ID | Title | Pts | Depends on |
|-------|------|-------|-----|------------|
| 1 | OA-1 | Add `deviceId` to the telemetry port + use case | 3 | — |
| 1 | OA-2 | `OemAi.Api` project (ASP.NET Core minimal API) | 5 | OA-1 |
| 1 | OA-3 | `StoredTelemetryService` + telemetry ingest endpoint | 5 | OA-2 |
| 1 | OA-4 | `OpenAiCompatibleAIService` (Foundry Local + Azure OpenAI) + `IOptions` config | 5 | — |
| 1 | OA-5 | Secrets in Key Vault, simple per-partner API key | 3 | OA-2 |
| 2 | OA-6 | Hybrid AI routing (local first, cloud fallback) | 5 | OA-4 |
| 2 | OA-7 | Streaming answers (SSE) end-to-end | 5 | OA-2, OA-4 |
| 2 | OA-8 | Desktop as client: telemetry upload + cloud `IAIService` | 5 | OA-3, OA-6 |
| 3 | OA-9 | Multi-tenant (one tenant per OEM): data isolation, per-tenant config | 8 | OA-3 |
| 3 | OA-10 | Device identity (cert/token issued at install) | 8 | OA-8 |
| 3 | OA-11 | Privacy: consent in app, retention policy, data residency (ND 13/2023, GDPR) | 5 | OA-9 |
| 3 | OA-12 | AI safety & cost: prompt-injection guard, content safety, prompt versioning, eval set, token cost per tenant | 8 | OA-6 |
| 3 | OA-13 | Ops: Docker, CI/CD (GitHub Actions), Azure Container Apps, OpenTelemetry + App Insights, rate limiting | 8 | OA-2 |
| 3 | OA-14 | Partner web dashboard (fleet view) — Blazor or React | 13 | OA-9 |

**Totals:** Phase 1 = 21 · Phase 2 = 15 · Phase 3 = 50.

### Phase 1 — MVP API (single tenant, Azure OpenAI)
- [ ] **OA-1 — `deviceId` in the telemetry port** · 3 pts
  The one place the current design breaks: `IDeviceTelemetryService.GetSnapshotAsync()`
  implicitly means "this machine". On a server it must be `GetSnapshotAsync(deviceId)`,
  and `DeviceAssistant.AskAsync` takes the `deviceId`. Desktop passes its own id.
  *AC:* Core compiles with the new signature; desktop behaviour unchanged; tests updated.
- [ ] **OA-2 — `OemAi.Api` project** · 5 pts
  ASP.NET Core minimal API referencing Core + Infrastructure; `AddOemAiServices()`;
  `POST /api/assistant/ask` → `DeviceAssistant.AskAsync(deviceId, question, history)`.
  *AC:* `dotnet run` serves the endpoint; OpenAPI page; request cancellation honoured.
- [ ] **OA-3 — Stored telemetry** · 5 pts
  `POST /api/devices/{id}/telemetry` persists snapshots (PostgreSQL to start;
  TimescaleDB / Azure Data Explorer when volume needs it). `StoredTelemetryService`
  returns the latest snapshot per device.
  *AC:* ask endpoint answers using the last uploaded snapshot; 404 for unknown device.
- [ ] **OA-4 — `OpenAiCompatibleAIService`** · 5 pts
  Generalise `FoundryLocalAIService`: base URL, model, API key from `IOptions`
  (`appsettings.json`). Same class serves Foundry Local and Azure OpenAI.
  *AC:* switching provider is config-only; Foundry discovery kept as one option.
- [ ] **OA-5 — Secrets & API key** · 3 pts
  Azure Key Vault for the AI key; partner calls carry an API key header (temporary
  until OA-10). *AC:* no secret in source or `appsettings.json`.

### Phase 2 — Hybrid
- [ ] **OA-6 — Hybrid AI routing** · 5 pts — an `IAIService` that tries local, falls
  back to cloud on error / long prompts. *AC:* offline device still answers.
- [ ] **OA-7 — Streaming** · 5 pts — `IAsyncEnumerable<string>` through the port, SSE
  on the API, incremental bubble in WPF. *AC:* first token visible < 1 s on cloud.
- [ ] **OA-8 — Desktop as client** · 5 pts — background telemetry upload + cloud
  `IAIService`. *AC:* dashboard data matches the device panel.

### Phase 3 — Partner-ready
- [ ] **OA-9 — Multi-tenant** · 8 pts — tenant per OEM; data isolation, per-tenant
  prompt/model/branding, quotas.
- [ ] **OA-10 — Device identity** · 8 pts — per-device credential at install; reject
  spoofed telemetry and unauthenticated AI calls.
- [ ] **OA-11 — Privacy & compliance** · 5 pts — telemetry with serials is personal
  data: consent screen, retention, deletion on request, data residency per contract.
- [ ] **OA-12 — AI safety & cost** · 8 pts — prompt-injection guard, Azure AI Content
  Safety, versioned prompts, regression eval set, token cost per tenant.
- [ ] **OA-13 — Ops** · 8 pts — container, CI/CD, Azure Container Apps, health checks,
  OpenTelemetry + Application Insights, ASP.NET Core rate limiting, autoscale.
- [ ] **OA-14 — Partner dashboard** · 13 pts *(split before starting)* — fleet view:
  devices with high RAM / degraded battery, question trends.

### Desktop backlog (independent of the cloud track)
- [ ] Persist conversation across app restarts.
- [ ] Pick the GPU model variant automatically (today: `foundry model load Phi-4-mini-instruct-cuda-gpu:5` before launch — the default alias resolves to the NPU variant, whose driver fails on this machine).
- [ ] Try `qwen2.5-7b` for better Vietnamese (config-only).
- [ ] Unit test for the CPU % delta calculation.

## Shipped (changelog)

### 2026-10-06
- Fleet demo: `DeviceStore` (in-memory, max 500 devices) + `POST /api/devices/{id}/telemetry` (id `^[A-Za-z0-9._-]{1,64}\z`) + `GET /api/devices`; ask takes `deviceId` (stored snapshot > submitted telemetry > server machine; unknown → 404).
- `DemoFleetSimulator` (`--Demo:Fleet=true`): 6 simulated Dell/HP/Lenovo/Asus/Acer devices with fixed profiles (healthy, high CPU, RAM pressure, low battery, offline 3 h).
- `OemAi.Agent` console (net10, reuses `WindowsDeviceTelemetryService`) uploads real telemetry; `demo.ps1` starts API + agent + browser; `docs/demo.md` talk track.
- Dashboard → OEM AI Fleet Console: KPIs, OEM filter, attention-first device list (LIVE/SIM), per-device detail + AI chat with quick-ask chips.
- Fix (found by QA): NaN/Infinity telemetry and `history:[null]` returned 500 → now 400.
- Pre-demo QA fixes: AI engine failure → 503 `{error}` shown in chat (was empty 500 + "API not running"); chat keeps line breaks; switching device no longer locks the chat while an old reply is pending; list no longer scroll-jumps on refresh; favicon 404 gone.
- Fleet Console restyled to a corporate light theme (gradient band, flat/square, blue accent; logo slot `wwwroot/logo.svg`) with **Japanese UI by default** + `日本語 | EN` switch.
- `AlertingAIService` (API-side `IAIService` wrapper): appends threshold alerts (CPU ≥ 85, RAM ≥ 90, battery ≤ 20 unplugged — same as the dashboard badges) to the system prompt; fixes the small model calling 94% RAM "normal". Real AI answers in Japanese verified.
- API + API tests target **net10.0** (LTS to 11/2028; .NET 8 ends 2026-11-10 and this machine has no ASP.NET Core 8 runtime). Desktop/Core/Infrastructure unchanged on net8; `Microsoft.AspNetCore.OpenApi` + `.WithOpenApi()` dropped (Swashbuckle reads endpoints itself).
- API: `POST /api/assistant/ask` takes optional `telemetry` — the device uploads its own snapshot (hybrid path); the server validates it (required text ≤ 100 chars, no control chars, numeric ranges) → 400 on bad data.
- API: `GET /api/telemetry` returns the server machine's snapshot.
- API Mock mode: `dotnet run --project src/OemAi.Api -- --Ai:Provider=Mock` (or `Ai__Provider=Mock`), same switch as Desktop.
- API: static files via built-in `UseDefaultFiles` + `UseStaticFiles` (SpaServices package removed).
- Dashboard shows real telemetry polled from the API and sends conversation history.
- Tests: `OemAi.Api.Tests` (WebApplicationFactory + fake AI): submitted telemetry grounds the answer, invalid telemetry → 400, `GET /api/telemetry`.

### 2026-10-05
- Web API + Dashboard: `OemAi.Api` (ASP.NET Core minimal API + Swashbuckle + vanilla JS dashboard at `/`). Run: `dotnet run --project src/OemAi.Api`
- Docs: `docs/huong-dan.html` (structure, why 3 projects, how to run, model loading) + this roadmap.
- Conversation memory: `ChatTurn` history, last 8 messages, "Cuộc trò chuyện mới" button; prompt fix so follow-ups ("Tôi tên gì?") resolve.
- Prompt fix: telemetry moved to the system message → off-topic questions ("1+1 = mấy") answered directly; OS shown as "Windows 11 (build N)".
- On-device AI: `FoundryLocalAIService` (Foundry Local, Phi-4-mini on CUDA GPU; auto server start / port discovery / model load). Mock kept behind `Ai__Provider=Mock`.
- Real telemetry: `WindowsDeviceTelemetryService` (BIOS registry + Win32 CPU/RAM/battery); manufacturer shown in UI.
- PoC scaffold: Core / Infrastructure / Desktop / Tests, WPF + CommunityToolkit.Mvvm + Generic Host DI, mock telemetry + mock AI, xUnit tests.
