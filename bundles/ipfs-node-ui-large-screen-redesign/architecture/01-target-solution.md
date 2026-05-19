# Target Solution

## Layout Strategy

- App shell uses nearly full large-screen width with a compact topbar and persistent left rail.
- Route headers use `PageHeader.Stats` with `CompactStatStrip` instead of `Lead` `SummaryTiles`.
- Route families remain segmented with `Tabs`; each tab owns one coherent workbench.
- Primary tab bodies use `Grid`, `Row`, `Column`, `Stack`, and `SectionCard` to place controls and results side by side where that reduces scrolling.
- Secondary detail remains in dialogs or inspector rails rather than occupying permanent top-level space.

## Component Strategy

- Use `CompactStatStrip` and `CompactStat` for stat badges.
- Use `StatusBadge`, `Pill`, and `PillList` for short categorical labels.
- Use `Grid`, `Row`, `Column`, and `Stack` before page-local CSS utilities for structure.
- Use `PageScaffold MaxWidthClass="max-w-full"` for large workspaces.
- Use existing `Dialog` components for modals unless a touched dialog requires `DialogScaffold`/`InspectorDialogLayout`.

## Styling Boundary

- Avoid new app-local pure CSS unless it directly fixes large-screen use, clipping, or layout density that shared components cannot express.
- Existing CSS may be tuned for shell width, body padding, and dense content constraints.
- Tailwind classes are acceptable for small local component tuning, such as `max-w-full`, `min-w-0`, `h-full`, `grid`, or `overflow-hidden`, when shared component parameters do not expose the same concern.

## Page Targets

- Dashboard: compact stats in header; Overview tab uses identity and repository sections side by side; Route Notes stays secondary.
- Files: compact stats in header; explorer and preview use wide horizontal tracks; dialogs handle upload/detail/topology/unpin/share.
- Content: compact stats in header; Blocks + Objects, DAG JSON, and Naming + Keys tabs use split panels for inputs/results.
- Network: compact stats in header; Swarm, Topology, DHT, and PubSub tabs use split panes, canvas workbench, and scroll-contained feeds.
- Settings: Endpoint, Config, and Maintenance remain tabs with compact action groups and constrained result panels.
- Pin Requests: compact stats in header; request filters and cards/list use dense workspace; details remain dialog.
- Logs: compact stats in header; filters, list, and message details stay within the visible route workspace.
