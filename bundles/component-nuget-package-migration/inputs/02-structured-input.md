# Structured Input

## Core Objective

- Migrate IPFS from old CanDoItAll component source project references to local NuGet packages from `C:\repositories\CanDoItAll\ExternalPackages`.

## Success Criteria

- IPFS has a repo-local package source for ExternalPackages.
- `CanDoItAll.IPFS.NodeControl` uses component package references instead of an external component `ProjectReference`.
- BaseLib `output.css` is linked and served from package static web assets.
- Playwright MCP before/after screenshots for IPFS and representative Economy apps show no material shared-component styling regression.

## Hard Constraints

- Do not edit or depend on component source projects under `C:\repositories\CanDoItAll.Components` or `C:\repositories\CanDoItAll\src\CanDoItAll.Components`.
- Do not swap shared components for custom raw markup or local structural CSS.
- Keep package versions aligned with the ExternalPackages artifacts, currently `0.1.0`.
- Use Playwright MCP for browser screenshots and assertions.

## Allowed Side Effects

- Add IPFS `NuGet.config`.
- Edit IPFS `Directory.Build.props` and `CanDoItAll.IPFS.NodeControl.csproj`.
- Add bundle proof artifacts under `bundles/component-nuget-package-migration/proof/`.

## Source Artifacts

- `bundle://inputs/00-original-request.md`
- `bundle://inputs/01-source-artifacts.md`

## Input Coverage Signals

- Preserve literal requirements to remove old component project connections and validate Economy before/after visually.

## Dependency And Sequencing Signals

- Package migration must land before build, output.css, and browser proof.
- Economy validation is downstream visual proof, not a prerequisite for IPFS project edits.

## Validation Expectations

- `rg` confirms no stale external CanDoItAll component project references remain in the migrated IPFS app.
- `dotnet restore`, `dotnet build`, and focused tests pass from the local feed.
- Browser/HTTP proof confirms `_content/CanDoItAll.Components.BaseLib/css/output.css` returns CSS after migration.

## Evidence Contract

- Command transcripts under `bundle://proof/SB01/commands/` and `bundle://proof/SB02/commands/`.
- Playwright screenshots under `bundle://proof/SB02/browser/`.
- Artifact-backed manifests for both subbundles.

## UI Validation Strategy

- Capture before screenshots before package edits when possible.
- Use a large desktop viewport first (`1600x1000` or equivalent).
- Review screenshots for missing BaseLib styling, missing icons, unstyled controls, chart/mermaid chrome regressions, layout collapse, overlap, or clipping.

## Browser Validation Analytics

- Record route, viewport, Playwright MCP actions, assertions, screenshot path, and result in `bundle://reviews/01-execution-report.md`.

## Working Assumptions

- Version `0.1.0` is the intended component package version because every package currently present in ExternalPackages uses that version and Economy already centralizes it.
- IPFS needs direct references to `CanDoItAll.Components.BaseLib` and `CanDoItAll.Components.CanvasLib`.

## Primary Risks

- A global package cache could mask a missing local feed.
- Static web assets may build but fail to serve if the package manifest is not consumed.
- Screenshots without review would not prove visual equivalence.
