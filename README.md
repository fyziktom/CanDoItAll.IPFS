# CanDoItAll.IPFS

`CanDoItAll.IPFS` contains an embedded IPFS engine, a typed .NET client, and a Blazor node-control app for inspecting, uploading, pinning, and managing a local or remote IPFS-style node.

## Projects

- `src/CanDoItAll.IPFS.Engine` hosts the embedded node and HTTP API.
- `src/CanDoItAll.IPFS.Client` provides typed HTTP client operations.
- `src/CanDoItAll.IPFS.NodeControl.Abstractions` contains UI-neutral NodeControl contracts and models for reusable node workflows.
- `src/CanDoItAll.IPFS.NodeControl` provides the large-screen desktop-oriented Blazor control app.
- `tests/CanDoItAll.IPFS.Tests` contains unit, integration, and UI-facing tests.

## Requirements

- .NET SDK `10.0.200` or later compatible patch version.
- A package source for `CanDoItAll.Components.*` packages. They are currently resolved by `NuGet.config` from the local `CanDoItAllExternalPackages` source and are not available on nuget.org yet.
- Docker Desktop or a compatible Docker engine for the compose and multi-node validation flows.

## Restore And Build

```powershell
dotnet restore .\CanDoItAll.IPFS.slnx
dotnet build .\CanDoItAll.IPFS.slnx --no-restore
```

The test project uses VSTest with MSTest.

```powershell
dotnet test .\tests\CanDoItAll.IPFS.Tests\CanDoItAll.IPFS.Tests.csproj --no-build
```

## Running NodeControl Locally

Set a passphrase and node repository path in the same shell that starts the app:

```powershell
$env:IPFS_PASS = "Choose-A-Strong-Passphrase"
$env:IPFS_PATH = "C:\ipfs-data\local-node"
dotnet run --project .\src\CanDoItAll.IPFS.NodeControl\CanDoItAll.IPFS.NodeControl.csproj
```

The control app connects to the configured node endpoint and can auto-start the local engine when the target URL resolves to the current machine and no node is listening.

## Configuration

- `IPFS_PASS` is required by the engine host for protected node repository access.
- `IPFS_PATH` selects the local node repository path.
- `IPFS_NODE_API_URL` can override the API bind URL for the engine host.
- `src/CanDoItAll.IPFS.NodeControl/appsettings.json` contains the default NodeControl endpoint and mode settings.

## Docker

The root `docker-compose.yml` starts an IPFS node API container and the NodeControl UI container. The compose stack uses named volumes so node repo data and NodeControl app data survive container restart and image rebuild.

Until the private `CanDoItAll.Components.*` packages are published to a shared feed, copy the required component packages into `docker/local-packages` before building the NodeControl image. The directory contains a README with the expected package pattern.

```powershell
$env:IPFS_PASS = "Choose-A-Strong-Passphrase"
docker compose up --build -d
```

Default endpoints:

- NodeControl UI: `http://127.0.0.1:5093`
- IPFS node API: `http://127.0.0.1:5001`

Optional port overrides:

```powershell
$env:NODE_CONTROL_PORT = "6093"
$env:IPFS_NODE_API_PORT = "6001"
docker compose up --build -d
```

Durable container paths:

- IPFS repository and pinned file blocks: `/data/ipfs`
- NodeControl settings JSON: `/data/node-control/settings/current-node-settings.json`
- Remote pin request JSON: `/data/node-control/remote-pin/remote-pin-requests.json`
- Explorer SQLite index: `/data/node-control/explorer-index/explorer.db`
- Application logs: `/data/node-control/logs/application.log`

`docker compose down` stops the stack and preserves named volumes. `docker compose down --volumes` deletes the persisted data.

## Packages

`CanDoItAll.IPFS.Engine` and `CanDoItAll.IPFS.Client` are configured as NuGet packages with MIT license metadata, source-link metadata, package readme metadata, and a local package icon.

```powershell
dotnet pack .\src\CanDoItAll.IPFS.Engine\CanDoItAll.IPFS.Engine.csproj --configuration Release
dotnet pack .\src\CanDoItAll.IPFS.Client\CanDoItAll.IPFS.Client.csproj --configuration Release
```

## Security

Do not commit node passphrases, access keys, private repositories, generated node data, or docker volume contents. Report security-sensitive issues privately until a disclosure process is published for the repository.

## License And Lineage

This repository is MIT licensed. The original `net-ipfs-engine` copyright notice is retained in `LICENSE`; package metadata also identifies CanDoItAll contributors for this maintained fork.

## Contributing

Before submitting changes:

1. Run `dotnet build .\CanDoItAll.IPFS.slnx --no-restore`.
2. Run the relevant `dotnet test` command for touched areas.
3. For UI changes, capture large-screen evidence for the affected NodeControl routes.
4. For persistence or docker changes, prove data survives restart and rebuild.
