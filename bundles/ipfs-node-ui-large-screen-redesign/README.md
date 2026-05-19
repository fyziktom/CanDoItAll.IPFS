# IPFS Node Large Screen UI Redesign

This bundle is the delivery contract for improving the CanDoItAll IPFS Node Control UI on large desktop screens.

## Profile

- `initiative`

## Mission

Redesign the node-control surfaces into dense, large-screen-first workspaces that use CanDoItAll BaseLib components, compact stat badges, tabs, split panes, and focused dialogs so operators can see and act on the main page content with minimal scrolling.

## Outcome Contract

- Requested outcome: the Dashboard, Files, Content, Network, Pin Requests, Settings, Logs, and related dialogs use horizontal workspace layouts, compact header/inline badges, and progressive disclosure instead of large stat cards or long vertical stacks.
- Hard constraints: large-screen-only optimization; do not spend time on medium or small screen tuning; prefer BaseLib components and their parameters; avoid adding raw CSS, `div`, `button`, or `span` structures where shared components cover the need; use Tailwind only for small local tuning.
- Evidence required before closure: imagegen proposal boards, prepared-stage bundle validation, focused build/tests, Playwright MCP large-screen screenshots for each route/tab/dialog touched, screenshot review against the proposal notes, and final completed-stage bundle validation.
- Known blockers or explicit scope exceptions: generated image boards are planning aids only and are not shipped proof; existing app-wide CSS contains legacy selectors that may remain where replacing them would be unrelated churn.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report
- `evidence/` design proposals, screenshots, and proof notes

## Recommended Execution Order

1. `subbundles/01-01-design-proposals`
2. `subbundles/02-02-layout-foundation`
3. `subbundles/03-03-page-and-dialog-redesign`
4. `subbundles/04-04-browser-proof-and-closure`

## Dependency And Validation Map

- The dependency map, critical-subbundle notes, and phase gates live in `plan/01-phase-plan.md`.
- If work resumes after compaction or by another agent, use this README, the active subbundle README, and `reviews/01-execution-report.md` as durable state.

## Validation Summary

- Bundle preparation status: `Ready`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `Completed`
