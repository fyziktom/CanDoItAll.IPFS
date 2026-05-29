# Current State

## Repository Observations

- `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj` targets `net10.0;net10.0-windows` and currently references `$(CanDoItAllRepoRoot)src\CanDoItAll.Components\CanDoItAll.Components.csproj`.
- `repo://Directory.Build.props` defines `CanDoItAllRepoRoot` solely to locate the old CanDoItAll repo; no IPFS package feed config exists yet.
- `repo://src/CanDoItAll.IPFS.NodeControl/Components/App.razor` already links BaseLib package static-web-asset paths through `@Assets["_content/CanDoItAll.Components.BaseLib/css/output.css"]` and `material-icons.css`, plus CanvasLib head/body assets.
- `repo://tests/CanDoItAll.IPFS.Tests/NodeControl/PublishedStaticAssetManifestTests.cs` already encodes the expected published endpoint for BaseLib `output.css`.
- External packages in `C:\repositories\CanDoItAll\ExternalPackages` include split component packages at version `0.1.0`; `CanvasLib` depends on `BaseLib`, `Common`, and `OverlayLib`, while `BaseLib` depends on `Common`.
- `C:\repositories\CanDoItAll.Economy` already has `NuGet.config` with `..\CanDoItAll\ExternalPackages` and uses `CanDoItAllComponentsPackageVersion` `0.1.0` in package references.

## Relevant Files

- `repo://NuGet.config` should be added to IPFS if absent.
- `repo://Directory.Build.props` should hold shared package version metadata after removing the old repo-root property if unused.
- `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj` is the old project-reference migration target.
- `repo://src/CanDoItAll.IPFS.NodeControl/Components/App.razor` is the output.css/static asset integration checkpoint.
- `repo://tests/CanDoItAll.IPFS.Tests/CanDoItAll.IPFS.Tests.csproj` consumes NodeControl and should continue compiling package-provided BaseLib types.
