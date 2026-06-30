# SB09 Release Validation Documentation And Handoff

## Status

- `Completed`

## Objective

- Perform final open-source publishing validation and close the bundle with artifact-backed proof.
- Ensure docs, package metadata, tests, docker persistence, large-screen UI behavior, performance/storage decisions, and raw request closure are complete.

## Covered Inputs

- R001 preparation/implementation gate closure.
- R002 open-source publishing readiness.
- R006 large desktop UI proof.
- R007 performance closure.
- R008 storage/query closure.
- R009 docker persistence closure.
- R010 workbook closure.
- R011 final release validation.

## Prerequisites

- SB02 metadata/dependency hardening is complete.
- SB04 docker persistence proof is complete.
- SB06 large-screen UI proof is complete.
- SB07 performance triage/fixes are complete.
- SB08 storage/query hardening is complete.

## Exact Source References

- repo://README.md
- repo://LICENSE
- repo://CanDoItAll.IPFS.slnx
- repo://src/CanDoItAll.IPFS.Engine/CanDoItAll.IPFS.Engine.csproj
- repo://src/CanDoItAll.IPFS.Client/CanDoItAll.IPFS.Client.csproj
- repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj
- repo://tests/CanDoItAll.IPFS.Tests/CanDoItAll.IPFS.Tests.csproj
- bundle://reviews/01-execution-report.md
- bundle://traceability/01-requirement-traceability.md
- bundle://inventories/publishing-prep-checklists.xlsx

## Deliverables

- Final build/test/package/advisory validation.
- Final docker compose persistence validation using the SB04 scenario.
- Final large-screen Playwright smoke across key NodeControl routes.
- README/release handoff text updated with proven commands and known limitations.
- Completed workbook and raw note closure rows.
- Completed-stage validator pass.

## Dependency Impact

- This is the closure subbundle; weak proof here means the app is not ready for open-source publication.
- Any failed dependency gate must reopen the owning earlier subbundle instead of being papered over here.

## Validation Depth

- End-to-end regression, publication readiness, browser proof, docker proof, and closure audit.

## Implementation Steps

1. Confirm all prerequisite subbundle gate rows are closed.
2. Run full build and full relevant test suite.
3. Run package/advisory validation.
4. Run docker compose persistence scenario from SB04.
5. Run Playwright large-screen smoke for key routes and dialogs.
6. Review README/LICENSE/package metadata for consistency with proven behavior.
7. Complete workbook statuses, execution report, raw note closure, and proof manifests.
8. Run completed-stage validator and repair any issues.

## Do Not Do

- Do not close over pending or weak proof.
- Do not make broad source changes in final validation unless reopening the owning subbundle.
- Do not add unproven README claims.
- Do not require small or medium viewport tuning.

## Acceptance Checklist

- Full build and test proof is captured.
- Package/advisory status is captured.
- Docker data survives restart and rebuild.
- Large desktop UI smoke passes with screenshots and no new console errors.
- Workbook rows are closed or explicitly deferred with accepted rationale.
- Raw notes are closed with artifact-backed proof.
- Completed-stage validator passes.

## Proof Required

- Build transcript.
- Test transcript.
- Package/advisory transcript.
- Docker persistence transcript.
- Playwright screenshots and action logs at `1920x1080` and `1600x900`.
- Completed `bundle://reviews/01-execution-report.md`.
- Completed workbook.
- Completed-stage validator transcript.

## Browser Validation Logging

- Routes: `/files`, `/content`, `/network`, `/settings`, and any modal/dialog route involved in publishing workflows.
- Viewports: `1920x1080` and `1600x900`.
- Actions: navigate, perform safe refresh/load operations, open key dialogs, verify no console errors, capture screenshots.
- Screenshot names should include `SB09`, route, viewport, and timestamp or run id.
- Review questions: Does the published app look coherent on desktop? Do dialogs and dense panels fit? Are warnings/errors visible and useful?

## Progression Gate

- The initiative may close only after completed-stage validator passes and every raw request note has artifact-backed proof or an explicit accepted exception.

## Suggested Agent Prompt

```text
Implement SB09 only. Validate the completed initiative end to end, reopen earlier subbundles for weak proof, capture final build/test/package/docker/browser evidence, close the workbook and execution report, and run the completed-stage validator.
```
