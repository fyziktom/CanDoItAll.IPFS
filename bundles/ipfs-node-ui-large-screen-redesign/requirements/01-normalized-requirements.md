# Normalized Requirements

| ID | Requirement | Source Notes | Acceptance |
| --- | --- | --- | --- |
| R1 | Execute through the CanDoItAll bundle workflow with durable bundle artifacts, gates, and final closure. | N1 | Prepared and completed validators pass, and execution report maps each note to proof or a stated blocker. |
| R2 | Use imagegen as a planning aid to create separate layout proposal coverage for pages, tabs, and dialogs. | N2 | Proposal summaries exist for Dashboard, Files/dialogs, Content tabs, Network tabs, Settings/Pin Requests/Logs, and screenshots are reviewed against them. |
| R3 | Use dialogs or inspector-style secondary panels to hide secondary detail from primary page surfaces. | N3 | File details, upload, topology, share, unpin, and pin-request details remain dialog/overlay surfaces; page proof shows secondary metadata is not permanently occupying the main screen unless it is an inspector. |
| R4 | Treat large-screen-only optimization as a hard constraint. | N4 | No implementation or proof time is spent tuning medium/mobile; large viewport is the required proof viewport. |
| R5 | Replace large stats cards with compact badges or compact stat strips. | N5 | Primary route stat areas and touched metadata result surfaces use `CompactStatStrip`, `CompactStat`, `StatusBadge`, `Pill`, or compact `SummaryTile` only where a compact alternative is not viable. |
| R6 | Use large-screen space efficiently and avoid empty areas. | N6 | Large screenshots show the first viewport occupied by navigation, compact header context, tabs, primary form/table/canvas/list, and relevant inspector/dialog content. |
| R7 | Avoid vertical stacking as the main layout pattern. | N7 | Route/tab panels use grids, rows/columns, tabs, and split panes for primary content; long stacks are limited to one-dimensional content inside a panel. |
| R8 | Validate with Playwright MCP screenshots and compare to design proposals. | N8 | Execution report includes route/tab/dialog screenshot rows, viewport, actions, assertions, screenshot path, proposal comparison, result, and repair notes when needed. |
| R9 | Prefer CanDoItAll BaseLib components and default styles; avoid own pure CSS and raw `div`/`button`/`span` structures. | N9 | Touched markup uses shared components where available; any remaining raw markup/custom CSS is explicitly limited to cases without a suitable component or legacy untouched areas. |
