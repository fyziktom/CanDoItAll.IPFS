# Current State

## Repo And App

- Workspace: `C:\repositories\CanDoItAll.IPFS`
- Blazor app project: `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\CanDoItAll.IPFS.NodeControl.csproj`
- Shared component references already present in `_Imports.razor`: `CanDoItAll.Components`, `CanDoItAll.Components.BaseLib`, `CanDoItAll.Components.CanvasLib`, and `CanDoItAll.Components.Common`.
- Routes already use several BaseLib components: `PageScaffold`, `PageHeader`, `SummaryTiles`, `SummaryTile`, `Tabs`, `SectionCard`, `FormField`, `FormSection`, `Stack`, `Row`, `Column`, `Button`, `Alert`, `Dialog`, `SelectionListItem`, `EmptyState`, and `LoadingState`.

## Component MCP Findings

- `PageScaffold`: shared page shell for dense pages and intentional width use.
- `PageHeader`: route-level orientation with `Actions` and `Stats` slots; useful for moving summary stats out of bulky lead sections.
- `Grid`: explicit page tracks using `ColumnTemplate*`, `RowTemplate*`, `Gap`, `AlignItems`, and `FillHeight`.
- `Stack`: one-dimensional flow with `GapScale`, `Orientation`, `Wrap`, `AlignItems`, and `JustifyContent`.
- `SectionCard`: grouped content blocks inside page/tabs panels.
- `Tabs`/`TabsItem`: primary route segmentation and scroll reduction.
- `CompactStatStrip`/`CompactStat`: compact status/count replacement for large summary cards.
- `StatusBadge`, `PillList`, `Pill`: short categorical status/count badges.
- `DialogScaffold` and `InspectorDialogLayout`: shared modal structure exists, but current product pages already use `Dialog`; do not introduce a second dialog system unless a touched dialog needs inspector structure that `Dialog` cannot express.

## Current UI Surfaces

- App shell: `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Layout\MainLayout.razor`
- Shell CSS: `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Layout\MainLayout.razor.css`
- App stylesheet: `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\wwwroot\app.css`
- Dashboard: `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Home.razor`
- Files page: `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Files.razor`
- Files logic: `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Files.razor.cs`
- Files components: `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\FilesComponents`
- Content: `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Content.razor`
- Network: `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Network.razor`
- Settings: `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Settings.razor`
- Pin Requests: `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\PinRequests.razor`
- Logs: `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Logs.razor`

## Current Issues Against The Request

- Route `Lead` regions rely on `SummaryTiles`/`SummaryTile`, which read as large stat cards and take vertical space on large monitors.
- Several result surfaces also use `SummaryTiles` for metadata that should become compact stat strips or badges.
- The app shell currently caps the viewport width at `2048px`, leaving avoidable unused space on very large monitors.
- Some pages are already tabbed, but individual tab panels still contain vertical stacks of form, action, and result sections where split grids would use space better.
- Several pages and dialogs still include raw `div`, `button`, and `span` wrappers for filters, callouts, context menus, steppers, and copy rows. Some are pragmatic because there is no exact shared component, but touched controls should prefer BaseLib.
- Existing CSS contains legacy page-local selectors. This bundle should avoid adding more pure CSS and only tune existing CSS when it directly affects large-screen density or layout proof.
