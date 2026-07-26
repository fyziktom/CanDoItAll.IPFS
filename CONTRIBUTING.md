# Contributing

This repository accepts code contributions only from partners who have been explicitly
approved by the maintainer. Unsolicited pull requests are not accepted.

To discuss becoming an approved partner, contact the maintainer on LinkedIn using the
handle `fyziktom`. Wait for approval before preparing or opening a pull request.

## Development Setup

1. Install the SDK pinned by `global.json`.
2. Restore packages from nuget.org with the repository `NuGet.config`.
3. Install Docker Desktop or a compatible Docker Engine when changing container assets.
4. Run commands from the repository root.

## Validation

```powershell
dotnet restore .\CanDoItAll.IPFS.slnx --configfile .\NuGet.config
dotnet build .\CanDoItAll.IPFS.slnx --configuration Release --no-restore
dotnet test .\CanDoItAll.IPFS.slnx --configuration Release --no-build
.\tools\deployment\nugets\Build-NuGets.ps1 -WhatIf
.\tools\validation\Test-Docker.ps1
```

Run the relevant runtime, packaging, Docker smoke, persistence, or release validation
when the change affects those surfaces. Record commands and results in the pull request.

## Architecture Rules

- Keep reusable contracts in `CanDoItAll.IPFS.Core`, HTTP transport and consumer
  composition in `CanDoItAll.IPFS.Client`, and node implementation in
  `CanDoItAll.IPFS.Engine`.
- Keep NodeControl abstractions UI-neutral and do not make public packages depend on the
  NodeControl application.
- Use released NuGet packages for cross-repository dependencies; do not add sibling
  source paths to shipping projects.
- Keep generated output, package archives, local state, credentials, and container data
  out of Git.
- Update documentation when public behavior, package metadata, or architecture contracts
  change.

## Pull Requests

- Open a pull request only after partner approval.
- Keep changes focused and preserve unrelated work.
- Add or update tests for behavior changes.
- Describe public API, package, persistence, and migration effects.
- Include the exact validation commands and results.
