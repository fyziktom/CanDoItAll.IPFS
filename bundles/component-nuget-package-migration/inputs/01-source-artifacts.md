# Source Artifacts

| Artifact | Path / Location | Notes |
| --- | --- | --- |
| User request | `bundle://inputs/00-original-request.md` | Raw migration and validation instruction. |
| IPFS solution | `repo://CanDoItAll.IPFS.slnx` | Primary workspace for the package migration. |
| IPFS Directory.Build.props | `repo://Directory.Build.props` | Currently defines `CanDoItAllRepoRoot`; candidate cleanup point. |
| IPFS NodeControl project | `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj` | Contains old external component `ProjectReference`. |
| IPFS App head | `repo://src/CanDoItAll.IPFS.NodeControl/Components/App.razor` | Links BaseLib `material-icons.css`, BaseLib `output.css`, and CanvasLib assets. |
| IPFS static asset test | `repo://tests/CanDoItAll.IPFS.Tests/NodeControl/PublishedStaticAssetManifestTests.cs` | Existing package-output.css expectation. |
| External package feed | `C:\repositories\CanDoItAll\ExternalPackages` | Contains split component packages at version `0.1.0`. |
| Economy repo package config | `C:\repositories\CanDoItAll.Economy\NuGet.config` | Already points to `..\CanDoItAll\ExternalPackages`. |
| Economy component-consuming apps | `C:\repositories\CanDoItAll.Economy\examples\CanDoItAll.Economy.Components.Demo`, `C:\repositories\CanDoItAll.Economy\examples\CanDoItAll.Economy.Simulator.App`, `C:\repositories\CanDoItAll.Economy\examples\CanDoItAll.Economy.MarketSandbox.Demo`, `C:\repositories\CanDoItAll.Economy\src\CanDoItAll.Economy.Node` | Browser validation candidates. |
