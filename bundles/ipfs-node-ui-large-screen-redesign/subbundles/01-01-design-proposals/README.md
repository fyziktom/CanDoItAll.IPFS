# 01 Design Proposals

## Status

- `Completed`

## Objective

Create and preserve route/tab/dialog design proposal coverage so implementation and Playwright screenshots have explicit large-screen layout targets.

## Covered Inputs

- N1, N2, N8
- R1, R2, R8

## Prerequisites

- Original request preserved in `inputs/00-original-request.md`.
- `imagegen` skill available and used through the built-in image generation path.

## Exact Source References

- `C:\repositories\CanDoItAll.IPFS\bundles\ipfs-node-ui-large-screen-redesign\inputs\00-original-request.md`
- `C:\repositories\CanDoItAll.IPFS\bundles\ipfs-node-ui-large-screen-redesign\evidence\01-design-proposals.md`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Home.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Files.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Content.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Network.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Settings.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\PinRequests.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Logs.razor`

## Deliverables

- Imagegen proposal boards for route/tab/dialog layout groups.
- Proposal summary and comparison criteria in `evidence/01-design-proposals.md`.
- Execution report row noting imagegen completion.

## Dependency Impact

- Subbundles 02-04 depend on this phase because screenshot validation compares rendered UI against the proposal criteria.
- Weak or missing proposal criteria would make the final visual gate subjective.

## Validation Depth

- Critical planning foundation.

## Implementation Steps

1. Generate imagegen proposal boards covering Dashboard, Files/dialogs, Content tabs, Network tabs, Settings tabs, Pin Requests, and Logs.
2. Summarize each proposal board into concrete implementation targets.
3. Record comparison criteria that Playwright screenshots must satisfy.
4. Update traceability and execution report.

## Scope Exceptions

- Imagegen text rendering is not authoritative; use proposal structure and density rather than exact generated labels as the comparison target.

## Do Not Do

- Do not treat imagegen output as shipped proof.
- Do not start product code changes before this planning foundation is recorded.

## Acceptance Checklist

- [x] Proposal coverage exists for every route family and dialog group.
- [x] Proposal criteria include compact stats, horizontal workspace use, and dialog/inspector progressive disclosure.
- [x] Execution report records the imagegen step.

## Proof Required

- `evidence/01-design-proposals.md`
- Execution report command/proof row for generated proposal boards.

## Browser Validation Logging

- N/A for this planning-only subbundle.

## Progression Gate

- Subbundle 02 may start only after proposal criteria are recorded and traceability maps N2 to R2 and this subbundle.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Use imagegen as a planning aid, summarize every proposal into concrete route/tab/dialog comparison criteria, update the execution report, and stop before touching product code.
```
