# 02 Layout Foundation

## Status

- `Completed`

## Objective

Establish the large-screen shell and compact-stat pattern that all redesigned routes will share.

## Covered Inputs

- N4, N5, N6, N9
- R4, R5, R6, R9

## Prerequisites

- Subbundle 01 closure gate passed.
- Component MCP findings for `PageScaffold`, `PageHeader`, `Grid`, `Stack`, `Tabs`, `SectionCard`, `CompactStatStrip`, `CompactStat`, and `StatusBadge` are recorded in `analysis/01-current-state.md`.

## Exact Source References

- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Layout\MainLayout.razor.css`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Home.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Files.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Content.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Network.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\PinRequests.razor`
- `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Logs.razor`

## Deliverables

- Shell uses available large-screen width and avoids avoidable max-width waste.
- Route-level `SummaryTiles` are replaced with compact `PageHeader.Stats` patterns where applicable.
- Compact stat pattern is reused rather than custom per-route CSS.

## Dependency Impact

- Subbundle 03 depends on this foundation because page/tab redesign should use one consistent header/stat pattern.
- If this phase is wrong, every route screenshot comparison is untrustworthy.

## Validation Depth

- Critical UI foundation.

## Implementation Steps

1. Update shell width/density using existing shell CSS only where BaseLib parameters cannot express it.
2. Move route-level stats from `Lead` `SummaryTiles` into `PageHeader.Stats` using `CompactStatStrip` and `CompactStat`.
3. Keep loading/error states outside bulky stat areas.
4. Build the app and run a focused smoke screenshot on one route before downstream work.

## Scope Exceptions

- Do not remove all existing CSS. Only edit CSS needed for large-screen shell usage or touched stat layout.

## Do Not Do

- Do not introduce a new custom stats component.
- Do not tune medium/small layouts.
- Do not change backend data loading or route behavior.

## Acceptance Checklist

- [x] Primary route stats are compact badges/compact stats, not large cards.
- [x] App shell no longer wastes large-screen width through avoidable max-width caps.
- [x] Touched markup uses BaseLib components before raw markup.
- [x] Build passes.

## Proof Required

- Build command result.
- Playwright MCP screenshot for at least one route using the foundation before subbundle 03 begins.
- Execution report row with compact-stat foundation decision.

## Browser Validation Logging

- Route: `/dashboard` or another route with compact route stats.
- Viewport: large desktop, at least `1920x1080`.
- Actions: navigate, wait for stable header, assert compact stat strip exists and no route-level `SummaryTiles` are visible in the header/lead.
- Screenshot path: `output/playwright/ui-redesign/foundation-dashboard.png`.

## Progression Gate

- Subbundle 03 may start only when compact route stats render on a large viewport and the build has no Razor/component errors.

## Suggested Agent Prompt

```text
Implement the layout foundation only.
Replace route-level large stat cards with compact BaseLib stats, widen the large-screen shell where needed, build, capture one large-screen screenshot, update the execution report, and stop if compact stats do not render cleanly.
```
