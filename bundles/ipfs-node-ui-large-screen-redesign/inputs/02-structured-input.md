# Structured Input

## Objectives

- Redesign the IPFS Node Control app for large desktop screens.
- Replace large summary-stat surfaces with compact badges or compact stat strips.
- Use tabs, split panes, rails, and dialogs so operators see primary work above the fold.
- Reduce custom page-local structural CSS and raw HTML controls where BaseLib components can express the layout.
- Validate actual rendered UI with Playwright MCP screenshots against the proposal boards.

## Hard Constraints

- Large screen only. Do not spend implementation or proof time tuning medium or small layouts.
- BaseLib first: `PageScaffold`, `PageHeader`, `Grid`, `Row`, `Column`, `Stack`, `SectionCard`, `Tabs`, `CompactStatStrip`, `CompactStat`, `StatusBadge`, `Dialog`, `InlineActions`, `FormSection`, `FormField`, and existing CanvasLib workbench components are preferred.
- Tailwind utility classes are allowed for small tuning. New pure CSS should be avoided unless shared components cannot express the layout.
- Generated design images are planning artifacts and do not count as shipped proof.

## Scope

- In scope: app shell density; route headers and stat areas; Dashboard tabs; Files explorer and file dialogs; Content tabs; Network tabs; Settings tabs; Pin Requests page and details dialog; Logs page.
- Out of scope: backend behavior, IPFS API semantics, responsive redesign below large desktop widths, broad deletion of legacy CSS unrelated to touched markup.

## Validation Expectations

- Prepared-stage validator passes before implementation.
- Each UI subbundle records Playwright MCP route, viewport, actions, assertions, screenshot path, visual review, and result.
- Dialog/open-state proof covers upload, file details, topology, unpin, remote share, and pin-request details when reachable.
- Final closure compares raw notes N1-N9 against shipped code and screenshots.
