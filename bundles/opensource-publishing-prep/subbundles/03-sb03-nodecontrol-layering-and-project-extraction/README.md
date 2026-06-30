# SB03 NodeControl Layering And Project Extraction

## Status

- `Completed`

## Objective

- Establish clean NodeControl boundaries so node workflows can be reused without Blazor UI or desktop host dependencies.
- Decide and implement the minimal project/folder extraction needed to support SB05 service decomposition and future CLI work.

## Covered Inputs

- R003 messy/mixed responsibility identification.
- R004 NodeControl responsibility isolation.
- R005 future non-UI/CLI feasibility.
- R006 NodeOperator decomposition prerequisite.

## Prerequisites

- SB01 baseline is complete.
- No unrelated production source changes are in progress for NodeControl.

## Exact Source References

- repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj
- repo://src/CanDoItAll.IPFS.NodeControl/Program.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeOperatorService.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Services/ExplorerIndexStore.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Services/ApplicationLogStore.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Services/RemotePinRequestStore.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Services/ServerNodeSettingsStore.cs
- repo://src/CanDoItAll.IPFS.Client/CanDoItAll.IPFS.Client.csproj
- repo://src/CanDoItAll.IPFS.Engine/CanDoItAll.IPFS.Engine.csproj
- bundle://architecture/01-target-solution.md
- bundle://inventories/publishing-prep-checklists.xlsx

## Deliverables

- A clean dependency direction for NodeControl abstractions, core workflows, persistence, and UI composition.
- Extracted interfaces/DTOs/options that do not depend on Razor, desktop host APIs, or UI state.
- Build/test proof that existing UI composition still works.
- Updated dependency diagram in the bundle if implementation changes the planned boundaries.

## Dependency Impact

- SB05 cannot safely split `NodeOperatorService` until this boundary exists.
- SB06 should not split pages around old mixed concrete services.
- Future CLI work depends on this project graph staying UI-independent.

## Validation Depth

- Architecture-critical foundation with build, test, and dependency-direction proof.

## Implementation Steps

1. Map current NodeControl service, DTO, persistence, and UI dependencies.
2. Choose the smallest extraction strategy that creates UI-independent contracts and workflow homes.
3. Move or introduce abstractions without changing behavior.
4. Update dependency injection composition in `Program.cs`.
5. Add or update tests that prove extracted services can be constructed without Blazor components.
6. Run build and focused tests.
7. Update workbook, execution report, and proof manifest.

## Do Not Do

- Do not build the CLI.
- Do not split every service if a smaller boundary proves the architecture.
- Do not move Razor components into reusable workflow projects.
- Do not perform UI visual redesign.

## Acceptance Checklist

- UI projects depend inward on abstractions/workflows; reusable projects do not depend on Blazor UI.
- Workflow contracts and persistence contracts are testable without desktop host startup.
- Existing app composition builds and existing behavior is preserved.
- A downstream SB05 implementer can split `NodeOperatorService` without redoing project boundaries.

## Proof Required

- Build transcript for the solution.
- Focused test transcript for NodeControl/service composition.
- Dependency graph or project reference proof.
- `bundle://proof/SB03/manifest.md` with changed-file hashes and portable references.
- Updated workbook rows for architecture boundaries.

## Browser Validation Logging

- Host-visible smoke is recommended if app startup composition changed.
- Route/window: NodeControl home route or default desktop host launch.
- Viewport: `1920x1080` if browser validation is used.
- Evidence: screenshot and console status only if UI host wiring changed.

## Progression Gate

- SB05 and SB06 may start only after dependency direction proves reusable NodeControl code has no Blazor UI dependency.

## Closure Evidence

- `bundle://proof/SB03/transcripts/failing-first-boundary-missing.txt`
- `bundle://proof/SB03/transcripts/restore-after-abstractions.txt`
- `bundle://proof/SB03/transcripts/build-after-abstractions.txt`
- `bundle://proof/SB03/transcripts/focused-nodecontrol-layering-tests.txt`
- `bundle://proof/SB03/transcripts/project-reference-graph.txt`
- `bundle://proof/SB03/transcripts/abstractions-forbidden-dependency-scan.txt`
- `bundle://proof/SB03/manifest.md`
- `bundle://proof/SB03/semantic-invariants.md`

## Suggested Agent Prompt

```text
Implement SB03 only. Create the minimum UI-independent NodeControl boundary, prove dependency direction and app composition, update the proof manifest, and stop before service or page decomposition beyond what the boundary requires.
```
