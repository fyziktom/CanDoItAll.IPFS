# Current State

## Repository Shape

- Solution: `repo://CanDoItAll.IPFS.slnx`.
- SDK pin: `repo://global.json` targets .NET SDK `10.0.200`.
- Shared build settings: `repo://Directory.Build.props` enables nullable reference types and implicit usings.
- Main source projects: `repo://src/CanDoItAll.IPFS.Engine`, `repo://src/CanDoItAll.IPFS.Client`, and `repo://src/CanDoItAll.IPFS.NodeControl`.
- Test project: `repo://tests/CanDoItAll.IPFS.Tests`.
- No existing prepared bundle was present before this bundle was scaffolded; `repo://bundles` was empty or absent for this initiative.

## Baseline Build Evidence

- Command executed during SB01 execution: `dotnet build CanDoItAll.IPFS.slnx --no-restore`.
- Transcript: `bundle://proof/SB01/transcripts/build-no-restore.txt`.
- Result: build succeeded with `0` errors and `15` warning lines in the refreshed incremental baseline.
- Notable warning groups:
  - `NU1902` moderate advisories for `OpenTelemetry.Api` `1.15.0`.
  - `NU1902` moderate advisories for `OpenTelemetry.Exporter.OpenTelemetryProtocol` `1.15.0`.
  - `NU1903` high advisory for `SQLitePCLRaw.lib.e_sqlite3` `2.1.11`.
  - Numerous nullable reference warnings across Engine, Client, NodeControl, and tests.
  - `VSTHRD200`, `VSTHRD103`, `ASP0019`, and `CA2022` warnings in API and test areas.

## Publishing Metadata Findings

- `repo://src/CanDoItAll.IPFS.Engine/CanDoItAll.IPFS.Engine.csproj` and `repo://src/CanDoItAll.IPFS.Client/CanDoItAll.IPFS.Client.csproj` still contain old repository/package metadata such as richardschneider URLs, version `0.42`, copyright years, and `PackageIconUrl`.
- `repo://LICENSE` still reflects the inherited upstream copyright and must be reviewed for the intended open-source publication posture.
- `repo://README.md` is minimal and still includes local run instructions rather than a complete open-source onboarding, security, docker, and release story.

## Maintainability Hotspots

| Area | Evidence | Preparation concern |
| --- | --- | --- |
| Node operation orchestration | `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeOperatorService.cs` is about 1113 lines | UI-facing orchestration, explorer refresh, upload/download/pin workflows, networking, config, repo maintenance, preview mapping, and lease handling are concentrated in one service. |
| File explorer page | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Files.razor.cs` is about 848 lines | Page state, pinned cache, upload workflows, explorer navigation, and background refresh are coupled. |
| Content page | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Content.razor` is about 794 lines | Block/object/DAG/name/key UI and command handling are in one large component. |
| Network page | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Network.razor` is about 794 lines | Peer, bootstrap, filters, DHT, PubSub, and busy-state logic are mixed in one page. |
| Remote pin sharing | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/RemotePinShareModal.razor` is about 607 lines | Modal owns target management, probing, live sends, offline export, envelope building, and UI state. |
| CSS surface | `repo://src/CanDoItAll.IPFS.NodeControl/wwwroot/app.css` is about 1845 lines | Styling is global and broad; implementation should split only where it supports maintainability, not mobile responsiveness. |
| Engine internals | `repo://src/CanDoItAll.IPFS.Engine/IpfsEngine.cs`, `repo://src/CanDoItAll.IPFS.Engine/Base/peer-talk/Swarm.cs`, `repo://src/CanDoItAll.IPFS.Engine/Base/net-mdns/MulticastService.cs` | Long runtime files should be reviewed for lifecycle, async, allocation, and responsibility boundaries before publishing. |

## NodeControl Boundary Findings

- NodeControl currently acts as both desktop UI host and node workflow layer.
- `NodeOperatorService` is the clearest extraction candidate for a UI-independent project that can later be reused by a CLI.
- Candidate future projects to evaluate in implementation:
  - `CanDoItAll.IPFS.NodeControl.Abstractions` for DTOs, workflow interfaces, and settings contracts.
  - `CanDoItAll.IPFS.NodeControl.Core` for node workflows, preview mapping, explorer orchestration, and validation.
  - `CanDoItAll.IPFS.NodeControl.Persistence` for SQLite/JSON/log persistence implementations.
  - `CanDoItAll.IPFS.NodeControl.Desktop` or current NodeControl web app for Blazor/desktop-specific composition.
- The current bundle only plans this split; implementation must prove that Blazor components depend on interfaces or view models rather than concrete mixed workflow services.

## Persistence And Docker Findings

- No root docker compose file exists yet.
- `repo://src/CanDoItAll.IPFS.NodeControl/appsettings.json` defaults to local API `http://127.0.0.1:5001/`.
- `repo://src/CanDoItAll.IPFS.Engine/HttpApiHost/Program.cs` reads `IPFS_NODE_API_URL` and `IPFS_PASS`, which makes containerized API hosting feasible.
- NodeControl persistence is currently local-path based:
  - `repo://src/CanDoItAll.IPFS.NodeControl/Services/ExplorerIndexStore.cs` uses raw SQLite with WAL and defaults under LocalApplicationData.
  - `repo://src/CanDoItAll.IPFS.NodeControl/Services/ApplicationLogStore.cs` uses rolling text logs under LocalApplicationData.
  - `repo://src/CanDoItAll.IPFS.NodeControl/Services/RemotePinRequestStore.cs` and `repo://src/CanDoItAll.IPFS.NodeControl/Services/ServerNodeSettingsStore.cs` use JSON stores.
- SB04 must plan explicit container paths and named volumes for IPFS repo data, Explorer SQLite database, JSON settings/requests, and logs so data survives restart and rebuild.

## Performance Scan Findings

- The requested .NET performance skill was applied as a planning lens.
- Transcript: `bundle://proof/SB01/transcripts/performance-source-scan-counts.txt`.
- Static scan counts:
  - `Substring` allocations: `19`.
  - `StartsWith`/`EndsWith`/`Contains` calls: `286`.
  - LINQ `Select`/`Where`/`OrderBy`/`GroupBy`: `358`.
  - LINQ `All`/`Any`: `101`.
  - `new Dictionary`/`new List` allocations: `177`.
  - Blocking waits: `115`.
  - `async void`: `18`.
  - Manual `HttpClient` construction: `4`.
  - `TODO`/`FIXME`/`HACK`: `81`.
  - `NotImplemented`: `27`.
  - `catch` clauses: `241`.
- These are triage leads, not automatic refactoring instructions. Implementation must target hot paths and risky lifecycle code with tests.

## EF Core Query Optimization Finding

- The requested EF Core query optimization skill was applied as an audit lens.
- Repository scans found no `EntityFrameworkCore`, `DbContext`, `DbSet`, `UseSqlite`, or `UseSqlServer` usage in source/tests.
- Transcript: `bundle://proof/SB01/transcripts/ef-core-marker-scan.txt`.
- The relevant query/storage work is raw SQLite and file-store hardening, especially `ExplorerIndexStore` queries, write locking, schema/indexing, `AddWithValue`, full reads/writes in JSON stores, and log rotation costs.
