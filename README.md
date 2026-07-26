# CanDoItAll.IPFS

[![Validation](https://github.com/fyziktom/CanDoItAll.IPFS/actions/workflows/validation.yml/badge.svg?branch=main&event=push)](https://github.com/fyziktom/CanDoItAll.IPFS/actions/workflows/validation.yml)
[![Client version](https://img.shields.io/nuget/v/CanDoItAll.IPFS.Client.svg?logo=nuget&label=Client)](https://www.nuget.org/packages/CanDoItAll.IPFS.Client)
[![Client downloads](https://img.shields.io/nuget/dt/CanDoItAll.IPFS.Client.svg?logo=nuget&label=Client%20downloads)](https://www.nuget.org/packages/CanDoItAll.IPFS.Client)
[![Engine version](https://img.shields.io/nuget/v/CanDoItAll.IPFS.Engine.svg?logo=nuget&label=Engine)](https://www.nuget.org/packages/CanDoItAll.IPFS.Engine)
[![Engine downloads](https://img.shields.io/nuget/dt/CanDoItAll.IPFS.Engine.svg?logo=nuget&label=Engine%20downloads)](https://www.nuget.org/packages/CanDoItAll.IPFS.Engine)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License](https://img.shields.io/badge/license-MIT--derived%20with%20website%20link-blue.svg)](LICENSE)

`CanDoItAll.IPFS` provides an embedded IPFS engine, reusable IPFS contracts, a typed
.NET client, and a Blazor node-control application for operating a local or remote node.

## Ownership

This repository owns:

- the reusable `CanDoItAll.IPFS.Client`, `CanDoItAll.IPFS.Core`, and
  `CanDoItAll.IPFS.Engine` packages;
- the maintained embedded IPFS engine and its HTTP API;
- the NodeControl application and its repository-specific Docker and release tooling.

This repository does not own:

- the IPFS protocol specification or public IPFS network;
- the `CanDoItAll.Components.*` packages, which are consumed from nuget.org;
- cross-repository standards and orchestration, which are maintained in
  `CanDoItAll.SharedInfo`.

## Projects And Packages

| Project or package | Purpose |
|---|---|
| `src/CanDoItAll.IPFS.Client` | Consumer-facing typed HTTP client for an IPFS node |
| `src/CanDoItAll.IPFS.Core` | Shared IPFS contracts and value types |
| `src/CanDoItAll.IPFS.Engine` | Embedded IPFS node and HTTP API |
| `src/CanDoItAll.IPFS.NodeControl.Abstractions` | UI-neutral NodeControl contracts and models |
| `src/CanDoItAll.IPFS.NodeControl` | Desktop-oriented Blazor node-control application |
| `tests/CanDoItAll.IPFS.Client.Tests` | Isolated Client/Core transport and contract tests |
| `tests/CanDoItAll.IPFS.Tests` | Unit, integration, contract, and UI-facing tests |

Install the public packages directly from nuget.org:

```powershell
dotnet add package CanDoItAll.IPFS.Client
dotnet add package CanDoItAll.IPFS.Core
dotnet add package CanDoItAll.IPFS.Engine
```

## Requirements

- .NET SDK `10.0.200`, with compatible patch roll-forward as pinned by `global.json`.
- Access to [nuget.org](https://www.nuget.org/) for package restore.
- Docker Desktop or a compatible Docker Engine for container validation and local
  container workflows.

## Build And Test

Run from the repository root:

```powershell
dotnet restore .\CanDoItAll.IPFS.slnx --configfile .\NuGet.config
dotnet build .\CanDoItAll.IPFS.slnx --configuration Release --no-restore
dotnet test .\CanDoItAll.IPFS.slnx --configuration Release --no-build
```

## Run NodeControl

Set a development passphrase and node repository path in the shell that starts the app:

```powershell
$env:IPFS_PASS = "Choose-A-Strong-Local-Passphrase"
$env:IPFS_PATH = "C:\ipfs-data\local-node"
dotnet run --project .\src\CanDoItAll.IPFS.NodeControl\CanDoItAll.IPFS.NodeControl.csproj --framework net10.0
```

The control app can connect to a configured remote endpoint or start the local engine
when the configured target resolves to this machine and no node is listening.
On Windows, use `--framework net10.0-windows` to run the desktop/tray variant.

Important configuration:

- `IPFS_PASS` unlocks the local engine repository.
- `IPFS_PATH` selects the local node repository path.
- `IPFS_NODE_API_URL` overrides the engine API bind URL.
- `src/CanDoItAll.IPFS.NodeControl/appsettings.json` contains NodeControl defaults.

## Containers

The canonical Compose model is a loopback-only local development stack:

```powershell
Copy-Item .env.example .env
# Replace the placeholder IPFS_PASS in the ignored .env file.
docker compose --env-file .env config --quiet
docker compose --env-file .env up -d --build --wait --wait-timeout 120
docker compose --env-file .env down
```

Default endpoints:

- NodeControl UI: `http://127.0.0.1:5093`
- IPFS node API: `http://127.0.0.1:5001`
- IPFS gateway: `http://127.0.0.1:5001/ipfs/{cid}`

Normal teardown preserves named volumes. Destructive volume reset is intentionally not
part of the normal workflow. See [container operations](docs/operations/containers.md)
and [backup and restore](docs/operations/backup-and-restore.md).

After the .NET unit tests and container smoke validation pass, pushes to `main` publish
commit-tagged Linux images to GitHub Container Registry:

- `ghcr.io/fyziktom/candoitall-ipfs-node:sha-<commit>`
- `ghcr.io/fyziktom/candoitall-ipfs-node-control:sha-<commit>`

Tags named `v<version>` additionally publish the immutable semantic version tag.

## Documentation

- [Architecture and ownership](docs/architecture.md)
- [Container operations](docs/operations/containers.md)
- [Backup and restore](docs/operations/backup-and-restore.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)

## Packaging And Releases

Preview or produce all public NuGet packages through the repository-owned entry point:

```powershell
.\tools\deployment\nugets\Build-NuGets.ps1 -WhatIf
.\tools\deployment\nugets\Build-NuGets.ps1 `
    -Configuration Release `
    -Version 0.1.15 `
    -OutputDirectory .\artifacts\packages
```

The command restores, builds, tests, and packs without publishing. Publishing to a
package source is a separate, explicitly authorized operation.

Build standalone NodeControl/engine release bundles with:

```powershell
.\tools\deployment\Build-Release.ps1 -WhatIf
.\tools\deployment\Build-Release.ps1
```

## License And Contributions

This repository uses the
[MIT-Derived License with CanDoItAll Website Link Requirement](LICENSE). Redistributions
of the software or a substantial portion of it in source or binary form must include at
least one link to [aicandoitall.com](https://aicandoitall.com). One such link satisfies
the added condition for a distribution containing multiple covered CanDoItAll
libraries. The upstream 2018 Richard Schneider copyright notice is retained.

Code contributions are limited to partners explicitly approved by the maintainer.
Unsolicited pull requests are not accepted. See [CONTRIBUTING.md](CONTRIBUTING.md) and
contact the `fyziktom` account on LinkedIn before preparing or opening a pull request.
