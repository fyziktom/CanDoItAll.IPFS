# SB06 Large Screen UI Component Decomposition

## Status

- `Completed`

## Completion Evidence

- Build proof: `bundle://proof/SB06/transcripts/build-after-ui-codebehind-complete.txt`
- Focused component proof: `bundle://proof/SB06/transcripts/focused-ui-component-tests.txt`
- Large-screen browser proof: `bundle://proof/SB06/transcripts/browser-smoke-playwright-passing-filtered.txt`
- Browser summary: `bundle://proof/SB06/browser-smoke-summary.json`
- Screenshots: `bundle://proof/SB06/screenshots/sb06-files-1920x1080.png`, `bundle://proof/SB06/screenshots/sb06-content-1920x1080.png`, `bundle://proof/SB06/screenshots/sb06-network-1920x1080.png`, `bundle://proof/SB06/screenshots/sb06-settings-1920x1080.png`, `bundle://proof/SB06/screenshots/sb06-remote-pin-share-modal-1920x1080.png`, plus matching `1600x900` captures.
- Line-count proof: `bundle://proof/SB06/transcripts/ui-line-counts-after-codebehind-split.txt`

## Objective

- Decompose the largest Blazor UI files into maintainable route shells, panels, dialogs, and view-model/state helpers while preserving the desktop large-screen user experience.

## Covered Inputs

- R003 messy large UI files.
- R006 desktop large-screen UI only.
- R010 checklist upkeep for UI decomposition.

## Prerequisites

- SB05 service decomposition is complete or has a documented temporary facade.
- Large-screen target viewports are confirmed in the execution report.

## Exact Source References

- repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Files.razor.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Content.razor
- repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Network.razor
- repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/RemotePinShareModal.razor
- repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Settings.razor
- repo://src/CanDoItAll.IPFS.NodeControl/wwwroot/app.css
- repo://src/CanDoItAll.IPFS.NodeControl/Program.cs
- bundle://inventories/publishing-prep-checklists.xlsx

## Deliverables

- Smaller components for repeated panels, action bars, lists/tables, modals, status/error areas, and command forms.
- Reduced route file responsibilities without changing desktop workflows.
- CSS organization improvements scoped to maintainability, not mobile redesign.
- Playwright evidence for major routes at large desktop viewports.

## Dependency Impact

- SB09 final release validation depends on stable UI route behavior and browser evidence.
- Future UI work becomes lower risk because large route files are split.

## Validation Depth

- UI, component-test, and browser-proof for large desktop only.

## Implementation Steps

1. Split one page or modal at a time, starting with highest-risk files from the inventory.
2. Extract reusable child components only when a responsibility boundary is clear.
3. Keep visible behavior, labels, workflows, and desktop density stable.
4. Avoid mobile-specific breakpoints or redesign work.
5. Run component tests where available and build.
6. Use Playwright at `1920x1080` and `1600x900` for affected routes.
7. Update browser analytics rows, workbook, and execution report.

## Do Not Do

- Do not tune small or medium screens.
- Do not redesign the visual language beyond splitting maintainability concerns.
- Do not reintroduce a giant shared component that repeats the same problem.
- Do not change workflow behavior unless required by a failing test and then document it.

## Acceptance Checklist

- Large route/modal files are meaningfully smaller or have clear follow-up rows if a file cannot be split safely.
- Extracted components have clear inputs/outputs and do not own unrelated workflows.
- Affected routes load and operate at `1920x1080` and `1600x900`.
- No incoherent overlap, clipped controls, or broken dialogs appear in screenshot review.
- Browser console has no new errors.

## Proof Required

- Build transcript.
- Component/focused test transcript where available.
- Playwright route actions and screenshots for affected routes.
- Updated `bundle://reviews/01-execution-report.md` browser analytics rows.
- Updated workbook UI decomposition rows.

## Browser Validation Logging

- Routes: `/files`, `/content`, `/network`, `/settings`, and any route hosting `RemotePinShareModal`.
- Viewports: `1920x1080` and `1600x900`.
- Actions: navigate, open major panels/modals, trigger safe load/refresh actions, verify no console errors, capture screenshots.
- Screenshot names should include subbundle, route, and viewport.
- Review questions: Are controls readable? Are dialogs framed correctly? Did any extracted component lose state, busy indicators, or error display?

## Progression Gate

- SB09 may proceed only after all affected UI routes have large-screen browser proof or documented blockers accepted in the execution report.

## Suggested Agent Prompt

```text
Implement SB06 only. Split the large Blazor pages/components for maintainability, preserve large desktop behavior, capture Playwright proof at 1920x1080 and 1600x900, and do not spend effort on small or medium screen tuning.
```
