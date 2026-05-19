# 04 Browser Proof And Closure

## Status

- `Completed`

## Objective

Prove the implemented UI against the proposal criteria with Playwright MCP screenshots, focused build/tests, and final raw-note closure.

## Covered Inputs

- N1 through N9
- R1 through R9

## Prerequisites

- Subbundle 03 closure gate passed.
- Managed app starts successfully or blocker is documented.
- Playwright MCP or Node-backed Playwright is available.

## Exact Source References

- `C:\repositories\CanDoItAll.IPFS\bundles\ipfs-node-ui-large-screen-redesign\evidence\01-design-proposals.md`
- `C:\repositories\CanDoItAll.IPFS\bundles\ipfs-node-ui-large-screen-redesign\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\CanDoItAll.IPFS.NodeControl.csproj`
- `C:\repositories\CanDoItAll.IPFS\tests\CanDoItAll.IPFS.Tests\NodeControl`

## Deliverables

- Large-screen Playwright screenshots for touched routes/tabs/dialogs.
- Proposal comparison result for every screenshot row.
- Focused build/test proof.
- Final raw-note closure table.
- Completed-stage bundle validation.

## Dependency Impact

- This is the final closure phase.
- Any failed screenshot comparison reopens subbundle 02 or 03 instead of being buried as residual risk.

## Validation Depth

- End-to-end UI proof and process-critical closure.

## Implementation Steps

1. Start or reuse the managed NodeControl app with dotnetwatch MCP.
2. Use Playwright MCP to navigate every route and tab at a large desktop viewport.
3. Open required dialogs/overlays and capture open-state screenshots.
4. Compare screenshots against `evidence/01-design-proposals.md`.
5. Repair mismatches and rerun affected proof.
6. Run build and focused tests.
7. Update execution report analytics, subbundle gate rows, and raw-note closure.
8. Run completed-stage bundle validation.

## Scope Exceptions

- If route data is unavailable because the local IPFS node cannot hydrate, record the exact blocker and capture the most stable reachable app state instead of guessing.

## Do Not Do

- Do not count generated imagegen boards as shipped proof.
- Do not close a route/tab/dialog without screenshot review.
- Do not add mobile proof.

## Acceptance Checklist

- [x] Every touched route/tab/dialog has a screenshot row or explicit blocker.
- [x] Each screenshot comparison result is pass or a repaired retry.
- [x] Raw notes N1-N9 are `Solved`, `Partially solved`, or `Not solved` with evidence.
- [x] Final validators pass or blockers are explicit.

## Proof Required

- Managed app session details.
- Playwright MCP screenshots under `output/playwright/ui-redesign/`.
- Build/test command output summarized in execution report.
- `scripts/validate_bundle.py --stage completed` result.

## Browser Validation Logging

- Route or dialog name.
- Large viewport dimensions.
- Actions/assertions.
- Screenshot path.
- Proposal comparison summary.
- Pass/fail result and repair notes.

## Progression Gate

- Bundle may close only when the execution report, raw-note closure, screenshots, build/tests, and final validator agree.

## Suggested Agent Prompt

```text
Validate and close the bundle.
Use Playwright MCP at a large desktop viewport, compare every screenshot against the proposal criteria, repair mismatches, run focused build/tests, update all execution report rows, close raw notes, and run the completed-stage validator.
```
