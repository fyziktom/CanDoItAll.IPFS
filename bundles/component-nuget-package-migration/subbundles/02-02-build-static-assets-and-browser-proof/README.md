# 02-build-static-assets-and-browser-proof

## Status

- `Completed`

## Objective

Prove the package migration works at build, static-asset, and browser levels, including before/after Playwright MCP visual proof for IPFS and representative Economy apps.

## Success Criteria

- IPFS restore/build/tests pass after SB01.
- BaseLib `_content/CanDoItAll.Components.BaseLib/css/output.css` is served as CSS.
- IPFS before/after screenshots show no missing shared-component styling.
- Economy before/after screenshots for representative apps show no material visual regression.
- Execution report and proof manifests cite all artifacts.

## Covered Inputs

- R1, R5, R6, R7.
- Raw notes N001, N004, N005.

## Prerequisites

- SB01 closure gate passed.
- Baseline before screenshots captured before package migration when possible; if not possible, document why and capture the earliest available package-fed baseline.

## Exact Source References

- `repo://src/CanDoItAll.IPFS.NodeControl/Components/App.razor`
- `repo://tests/CanDoItAll.IPFS.Tests/NodeControl/PublishedStaticAssetManifestTests.cs`
- `C:\repositories\CanDoItAll.Economy\examples\CanDoItAll.Economy.Components.Demo\CanDoItAll.Economy.Components.Demo.csproj`
- `C:\repositories\CanDoItAll.Economy\examples\CanDoItAll.Economy.Simulator.App\CanDoItAll.Economy.Simulator.App.csproj`
- `C:\repositories\CanDoItAll.Economy\examples\CanDoItAll.Economy.MarketSandbox.Demo\CanDoItAll.Economy.MarketSandbox.Demo.csproj`

## Deliverables

- Run build/test verification for IPFS.
- Launch IPFS and Economy validation apps.
- Use Playwright MCP to navigate, assert CSS/static assets, capture screenshots, and record visual review.
- Create `proof/SB02/manifest.md` and `proof/SB02/semantic-invariants.md`.

## Dependency Impact

- This is the final closure subbundle.
- Weak static asset or screenshot proof would leave the user's `output.css` and Economy visual-regression requirements unresolved.

## Validation Depth

- Critical UI closure with build, static-asset, browser, screenshot-review, and fake-proof resistance.

## Implementation Steps

1. Capture or verify before screenshots for IPFS and Economy targets.
2. Run IPFS restore/build/tests after SB01.
3. Launch IPFS and check `_content/CanDoItAll.Components.BaseLib/css/output.css` via HTTP/Playwright.
4. Capture IPFS after screenshot and review for missing styling/icons/layout collapse.
5. Launch representative Economy apps and capture after screenshots matching the before routes/viewports.
6. Record Playwright MCP actions, screenshots, assertions, and visual decisions in the execution report.
7. Run final source audit and bundle validators.

## Scope Exceptions

- If an Economy app cannot launch because of an unrelated host dependency, record the failed command/log and validate the next representative app rather than silently dropping Economy visual proof.

## Do Not Do

- Do not edit UI layout to chase unrelated visual differences.
- Do not accept screenshots alone without static asset assertions and written visual review.
- Do not mark closure complete if `output.css` returns an error, HTML fallback, or empty body.

## Acceptance Checklist

- [x] IPFS build/test proof passes for the migration surface: build passed and focused static asset/component tests passed.
- [x] BaseLib `output.css` returns HTTP 200 and non-empty CSS content.
- [x] IPFS after screenshot shows BaseLib styling present.
- [x] Economy before/after screenshots are available and reviewed.
- [x] Final execution report has no pending raw-note closure rows.

## Proof Required

- `dotnet test CanDoItAll.IPFS.slnx --no-build`
- Playwright MCP `browser_resize` to a large viewport, `browser_navigate`, `browser_snapshot`, `browser_run_code_unsafe` for CSS/DOM checks where needed, and `browser_take_screenshot`.
- `bundle://proof/SB02/browser/ipfs-before.png`, `ipfs-after.png`
- `bundle://proof/SB02/browser/economy-components-demo-before.png`, `economy-components-demo-after.png`
- `bundle://proof/SB02/browser/economy-simulator-before.png`, `economy-simulator-after.png`
- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB02/semantic-invariants.md`

## Browser Validation Logging

- Target routes: IPFS `/`; Economy Components Demo `/`; Economy Simulator App `/`; optional Market Sandbox `/` if one primary app is blocked.
- Required viewport: large desktop `1600x1000` or equivalent maximized viewport; narrower pass only if observed layout changed.
- Required Playwright MCP actions: navigate, snapshot or DOM assertion, static CSS fetch assertion for IPFS, screenshot.
- Required review questions: Are BaseLib styles present? Are material icons present? Are chart/mermaid/component surfaces styled? Is there any layout collapse, overlap, clipping, or obvious spacing drift?

## Progression Gate

- Final closure may proceed only when build/test/static-asset proof and browser-validation analytics support every raw note, or when any exception is represented as a blocker with evidence.

## Semantic Adequacy Gate

- Shallow-pass trap: a screenshot of a loaded page without proving `output.css` is served does not pass.
- Adversarial negative proof: a CSS endpoint returning HTML, 404, or empty content fails closure even if the page has cached styles.
- Semantic positive proof: runtime fetch and screenshot review demonstrate package-owned BaseLib CSS and CanvasLib/Charts assets remain available.
- Anti-stub audit: no disabled stylesheet links, mocked CSS endpoints, or skipped browser rows are allowed.
- Raw-note literal closure: N004 and N005 require explicit output.css and Economy before/after proof.

## Suggested Agent Prompt

```text
Implement SB02 only.
Run IPFS build/test/static asset checks, use Playwright MCP for IPFS and Economy screenshots before/after, review the screenshots for shared-component regressions, create proof/SB02 artifacts, and close the execution report only if every raw note has evidence.
```
