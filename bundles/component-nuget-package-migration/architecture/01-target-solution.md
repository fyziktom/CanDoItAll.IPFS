# Target Solution

## Intended Shape

- IPFS owns a repo-local `NuGet.config` with `..\CanDoItAll\ExternalPackages` plus `nuget.org`, matching the Economy repo pattern.
- IPFS centralizes `CanDoItAllComponentsPackageVersion` in `Directory.Build.props`.
- `CanDoItAll.IPFS.NodeControl.csproj` keeps IPFS project references to `Engine` and `Client`, but replaces the external component project reference with package references to `CanDoItAll.Components.BaseLib` and `CanDoItAll.Components.CanvasLib`.
- `App.razor` continues to load BaseLib `material-icons.css`, BaseLib `output.css`, and CanvasLib assets from static web assets.
- Economy remains package-fed and is used as a regression witness for the shared component package output.

## Boundaries

- Do not edit component package source or rebuild packages.
- Do not edit Economy source unless validation reveals a stale component source-project reference or missing local feed.
- Do not introduce a new CSS pipeline in IPFS; the packages own `output.css`.
- Do not broaden this migration to unrelated IPFS architecture or UI redesign.
