# Imagegen Design Proposals

Imagegen was used as a planning aid to produce high-fidelity proposal boards. The generated boards are visible in the Codex thread and are summarized here as the implementation target. They are not proof of shipped behavior.

## Proposal Board A: Dashboard

- Separate desktop artboards: Dashboard Overview tab and Dashboard Route Notes tab.
- Target composition: compact app shell, left rail, header stat badges, top tab strip, two-column content, route notes as dense table/list instead of large cards.
- Key comparison points: no oversized summary cards; no decorative hero; main status and route table fit above the fold.

## Proposal Board B: Files And Dialogs

- Separate desktop artboards: Files Explorer page, Upload dialog, File Details dialog, Share/Remote Pin dialog.
- Target composition: explorer table/grid plus collapsible inspector; dialogs use compact metadata badges, two-column body regions, and footer actions.
- Key comparison points: preview/detail information is progressively disclosed; upload options and selected-file list share one dialog viewport; details and share dialogs avoid long stacks.

## Proposal Board C: Content Tabs

- Separate desktop artboards: Blocks + Objects tab, DAG JSON tab, Naming + Keys tab.
- Target composition: split workbench panels, compact stat badges in header, code/result panes beside controls, keys and IPNS details in dense panels.
- Key comparison points: each tab stands alone; forms and results use horizontal space; code panes do not push controls far below the fold.

## Proposal Board D: Network Tabs

- Separate desktop artboards: Swarm tab, Topology tab, DHT tab, PubSub tab.
- Target composition: tables with inspector rails, canvas topology with side context, DHT query/results split, PubSub topics/feed/details split.
- Key comparison points: topology remains a full workbench; tables and inspectors share the viewport; topic/feed detail stays above fold.

## Proposal Board E: Settings, Pin Requests, Logs

- Separate desktop artboards: Settings Endpoint tab, Settings Config tab, Settings Maintenance tab, Pin Requests page/details dialog, Logs page.
- Target composition: settings tabs use tables/forms in columns; pin requests use list/table plus details dialog/rail; logs use filter toolbar, log table, and details inspector.
- Key comparison points: large stats are compact badges; pages are not vertical piles; secondary details live in dialogs or inspectors.

## Visual Thesis

Large-screen operational console: compact, calm, BaseLib-native surfaces with high information density, short action paths, and contextual detail hidden in tabs, rails, or dialogs until needed.

## Interaction Thesis

- One tabbed workbench per route family.
- One compact stat strip per route header or important result surface.
- Dialogs and inspector rails carry secondary metadata so primary tables, forms, and canvases own the first viewport.
