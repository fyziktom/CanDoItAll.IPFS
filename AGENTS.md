# Repository Agent Instructions

## Shared Standards

Follow the reviewed standards in a resolved `CanDoItAll.SharedInfo` clone. This
repository owns its local implementation and any documented exceptions.

Use `$apply-candoitall-shared-standards` when available. It checks an explicit
`CANDOITALL_SHAREDINFO_ROOT` and nearby sibling locations without assuming that
SharedInfo is a child of this repository or that every machine uses the same root.

## Repository Scope

- This repository owns the IPFS domain model, embedded engine, reusable HTTP client,
  NodeControl abstractions, and the NodeControl Blazor application.
- Public reusable packages are `CanDoItAll.IPFS.Client`, `CanDoItAll.IPFS.Core`, and
  `CanDoItAll.IPFS.Engine`; keep consumer-facing contracts isolated from host and UI
  concerns.
- UI projects consume published `CanDoItAll.Components.*` packages from NuGet.org.
  Prefer BaseLib components and tokens over repository-specific styling.
- The existing file browser is intentionally repository-local until its planned
  replacement; do not refactor or restyle it as part of unrelated UI work.

## Commands

- Build: `dotnet build CanDoItAll.IPFS.slnx --configuration Release`
- Test: `dotnet test CanDoItAll.IPFS.slnx --configuration Release`
- Package: `./tools/deployment/nugets/Build-NuGets.ps1 -Configuration Release`
- Validate containers: `./tools/validation/Test-Docker.ps1 -RunBuildChecks -Smoke`

## Safety

- Keep sibling repositories read-only unless the user explicitly requests a multi-repo
  change.
- Do not commit generated output, local settings, credentials, runtime data, or
  passphrases.
- Preserve repository-specific changes that are unrelated to the active task.
