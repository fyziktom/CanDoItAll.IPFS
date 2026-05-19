# 03 Page And Dialog Redesign

## Status

- `Completed`

## Objective

Redesign the route tabs and dialogs so the main work surfaces use horizontal large-screen space, reduce scrolling, and keep secondary information in dialogs or inspectors.

## Covered Inputs

- N3, N5, N6, N7, N9
- R3, R5, R6, R7, R9

## Prerequisites

- Subbundle 02 closure gate passed.
- Large-screen compact-stat foundation is visually proven.

## Exact Source References

- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Home.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Files.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\FilesComponents\FilesExplorerPanel.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\FilesComponents\FilesPreviewPanel.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\FilesComponents\FilesDetailDialog.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\FilesComponents\FilesUploadDialog.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\FilesComponents\FilesUnpinDialog.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\RemotePinShareModal.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Content.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Network.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Settings.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\PinRequests.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\PinRequestsComponents\PinRequestDetailsDialog.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Logs.razor`

## Deliverables

- Dashboard, Files, Content, Network, Settings, Pin Requests, and Logs use large-screen workbench layouts.
- Route tabs avoid unnecessary vertical stacking by using grids, split panes, and scroll-contained lists/results.
- Dialogs use compact metadata and avoid large stat cards.
- New pure CSS is avoided unless strictly needed for large-screen layout proof.

## Dependency Impact

- Subbundle 04 depends on this work.
- If any route/tab/dialog still has primary bulky stat cards or avoidable vertical stacking, final screenshot comparison must fail and reopen this subbundle.

## Validation Depth

- UI, component-test, and browser-proof.

## Implementation Steps

1. Redesign route/tab bodies using BaseLib `Grid`, `Row`, `Column`, `Stack`, `SectionCard`, `Tabs`, compact stats, and existing dialogs.
2. Convert touched dialog metadata summaries from large cards to compact stat strips/badges.
3. Replace touched raw controls with `Button`, `StatusBadge`, `Pill`, `InlineActions`, or BaseLib equivalents where possible.
4. Build and run focused UI/component tests.
5. Update execution report with changed surfaces and proof status.

## Scope Exceptions

- Existing raw markup in Blazor reconnect UI and untouched legacy app CSS is out of scope.
- Some low-level file upload inputs may remain raw where Blazor `InputFile` requires it.

## Do Not Do

- Do not change backend service behavior.
- Do not add a custom design system or app-specific component library.
- Do not optimize or test mobile/small screen layouts.

## Acceptance Checklist

- [x] No primary route-level large stat cards remain.
- [x] Touched dialogs use compact metadata.
- [x] Each major route/tab has a large-screen workspace layout.
- [x] Main actions and results are visible without excessive scrolling on large screens.
- [x] Touched controls use BaseLib components where available.

## Proof Required

- Build result.
- Focused tests for NodeControl UI/component coverage.
- Playwright MCP screenshots for every touched route/tab/dialog or explicit blocker rows.

## Browser Validation Logging

- Routes: `/dashboard`, `/files/explorer`, `/content`, `/network`, `/settings`, `/pin-requests`, `/logs`.
- Dialog/open states: upload, details, topology, unpin, share, pin request details when reachable.
- Viewport: large desktop, at least `1920x1080`.
- Actions: navigate, select each tab, open dialogs/overlays, assert compact stats and visible main workbench.
- Screenshot paths: `output/playwright/ui-redesign/<surface>.png`.

## Progression Gate

- Subbundle 04 may start only when build/tests pass or blockers are explicit and all targeted surfaces are ready for Playwright comparison.

## Suggested Agent Prompt

```text
Implement the page and dialog redesign only.
Use BaseLib components first, reduce vertical stacking, keep secondary detail in dialogs or inspectors, avoid mobile tuning, run focused validation, and update the execution report with changed surfaces.
```
