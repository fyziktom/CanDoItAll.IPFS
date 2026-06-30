# Source Artifacts

- `bundle://inputs/00-original-request.md` preserves the raw user request.
- `repo://CanDoItAll.IPFS.slnx` is the solution entry point used for baseline build and project inventory.
- `repo://Directory.Build.props` defines shared build settings and the CanDoItAll component package version.
- `repo://global.json` pins the SDK to `10.0.200`.
- `repo://src/CanDoItAll.IPFS.Engine/CanDoItAll.IPFS.Engine.csproj` is the embedded node and HTTP API host project.
- `repo://src/CanDoItAll.IPFS.Client/CanDoItAll.IPFS.Client.csproj` is the typed HTTP client project.
- `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj` is the Blazor desktop/control app project.
- `repo://tests/CanDoItAll.IPFS.Tests/CanDoItAll.IPFS.Tests.csproj` is the main validation project.
- `repo://README.md` and `repo://LICENSE` are the current open-source-facing root documents.
- `bundle://inventories/publishing-prep-checklists.xlsx` is the detailed checklist workbook required by the user.

## Baseline Commands Run During Preparation

- `dotnet build CanDoItAll.IPFS.slnx --no-restore` from `repo://.` completed successfully with 968 warnings.
- `rg` inventory scans were run over `src` and `tests` for file size, EF Core/SQLite usage, performance-pattern signals, source layout, publishing metadata, and docker/runtime configuration signals.

## Source Observations

- No existing bundle for this request was present under `repo://bundles` before this bundle was scaffolded.
- No `docker-compose.yml` or docker-specific runtime file was present at preparation time.
- No Entity Framework Core package or `DbContext` was found; data access currently uses raw `Microsoft.Data.Sqlite` and JSON/file stores.
