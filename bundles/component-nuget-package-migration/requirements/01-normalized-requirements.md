# Normalized Requirements

| ID | Requirement | Source | Acceptance |
| --- | --- | --- | --- |
| R1 | Use the CanDoItAll bundle workflow for planning, execution, proof, and closure. | N001 | Bundle files, subbundle gates, execution report, proof manifests, and final validator output are present. |
| R2 | Configure IPFS restore to use the component NuGet packages from `C:\repositories\CanDoItAll\ExternalPackages`. | N002 | `NuGet.config` or equivalent local source exists and restore/build logs show the app can resolve `CanDoItAll.Components.*` packages. |
| R3 | Remove IPFS dependency on old CanDoItAll component source projects. | N003 | `rg "ProjectReference.*CanDoItAll.Components|CanDoItAllRepoRoot"` has no stale external component project dependency in active IPFS project files. |
| R4 | Reference the split component packages the app actually uses. | N002, N003 | `CanDoItAll.IPFS.NodeControl.csproj` uses package references for BaseLib and CanvasLib at the shared package version. |
| R5 | Preserve BaseLib `output.css` and related static web assets. | N004 | App head remains pointed at `_content/CanDoItAll.Components.BaseLib/css/output.css`; build/test and browser/HTTP checks prove it returns CSS. |
| R6 | Validate IPFS visual behavior after migration. | N004, N005 | Playwright MCP screenshots before/after exist for the IPFS app, with review noting no missing shared styling. |
| R7 | Validate representative Economy component apps visually before and after. | N005 | Playwright MCP before/after screenshots exist for selected Economy apps using BaseLib/Charts/Mermaid, with review noting no material regression or documented blocker. |
