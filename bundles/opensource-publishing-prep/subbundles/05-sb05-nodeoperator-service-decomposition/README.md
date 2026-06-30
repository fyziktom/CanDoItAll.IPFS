# SB05 NodeOperator Service Decomposition

## Status

- `Completed`

## Objective

- Split `NodeOperatorService` into smaller workflow services with stable interfaces while preserving behavior.
- Reduce mixed responsibilities around file, content, network, maintenance, explorer, preview, and virtual folder operations.

## Covered Inputs

- R003 messy/mixed responsibility cleanup.
- R004 NodeControl isolation.
- R005 future non-UI/CLI feasibility.
- R006 NodeOperator decomposition.

## Prerequisites

- SB03 dependency boundaries are complete and proven.
- NodeControl workflow contracts are available outside Razor UI code.

## Exact Source References

- repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeOperatorService.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Services/ExplorerIndexStore.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Files.razor.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Content.razor
- repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Network.razor
- repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Settings.razor
- repo://tests/CanDoItAll.IPFS.Tests/CanDoItAll.IPFS.Tests.csproj
- bundle://architecture/01-target-solution.md
- bundle://inventories/publishing-prep-checklists.xlsx

## Deliverables

- Smaller workflow services such as file, content, network, repo/config, explorer/index, and preview workflows where warranted by actual code shape.
- Thin compatibility facade only if needed to avoid a risky all-at-once UI rewrite.
- Tests proving decomposed services preserve key workflows.
- Updated DI registration and page dependencies.

## Dependency Impact

- SB06 should consume smaller workflow boundaries instead of a giant service.
- SB07 performance work can target smaller services with less risk.
- Future CLI work depends on these services remaining UI-neutral.

## Validation Depth

- Behavior-preserving refactor with focused service tests and app composition proof.

## Implementation Steps

1. Group existing public `NodeOperatorService` methods by workflow responsibility.
2. Identify private helper clusters that should move with each workflow.
3. Create interfaces and services inside the SB03-approved boundary.
4. Keep a temporary facade only when it materially lowers blast radius.
5. Update page/service consumers incrementally.
6. Add tests around upload, preview, pin, explorer refresh, content, network, and repo/config workflows based on feasible test seams.
7. Run build and focused tests; update workbook and execution report.

## Do Not Do

- Do not rewrite Engine or Client protocols in this subbundle.
- Do not redesign UI pages beyond dependency updates.
- Do not create an abstraction for every private helper without a real responsibility boundary.
- Do not break future CLI feasibility by introducing UI dependencies into workflow services.

## Acceptance Checklist

- `NodeOperatorService` no longer owns unrelated workflow categories in one large class.
- Each extracted service has a clear responsibility and interface.
- Page dependencies are clearer and no longer force every page through all node operations.
- Tests or proof cover behavior preservation for each moved workflow category.
- Build passes without new warnings attributable to the refactor.

## Proof Required

- Build transcript.
- Focused test transcript for moved workflow services.
- Diff summary showing reduced responsibility concentration.
- Updated workbook rows for NodeOperator decomposition.
- Updated execution report gate row.

## Closure Evidence

- Facade after decomposition: `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeOperatorService.cs`.
- Workflow interfaces: `repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions/INodeWorkflows.cs`.
- Concrete workflow services:
  - `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeFileWorkflowService.cs`
  - `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeExplorerWorkflowService.cs`
  - `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeContentWorkflowService.cs`
  - `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeNetworkWorkflowService.cs`
  - `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeMaintenanceWorkflowService.cs`
- Build proof: `bundle://proof/SB05/transcripts/build-after-nodeoperator-decomposition.txt`, `bundle://proof/SB05/transcripts/build-after-files-smoke-adjustment.txt`.
- Focused workflow/composition proof: `bundle://proof/SB05/transcripts/focused-nodeoperator-decomposition-tests.txt`.
- Page smoke proof: `bundle://proof/SB05/transcripts/nodeoperator-page-smoke-tests-passing.txt`.
- Responsibility reduction proof: `bundle://proof/SB05/transcripts/nodeoperator-line-counts-after-decomposition.txt` and `bundle://proof/SB05/transcripts/nodeoperator-public-surface-after-decomposition.txt`.
- Proof manifest: `bundle://proof/SB05/manifest.md`.

## Browser Validation Logging

- Host-visible smoke is required if page dependencies change.
- Routes: `/files`, `/content`, `/network`, `/settings`.
- Viewport: `1920x1080`.
- Evidence: route load, one representative command per touched page where safe, screenshot paths, and console error review.

## Progression Gate

- SB06 may start only after pages can depend on smaller workflow services or a documented temporary facade with migration notes.

## Suggested Agent Prompt

```text
Implement SB05 only. Split NodeOperator responsibilities along the SB03 boundary, preserve behavior with focused tests and route smoke, update the execution report, and stop before visual page decomposition.
```
