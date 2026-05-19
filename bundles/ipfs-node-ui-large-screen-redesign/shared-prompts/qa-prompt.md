# QA Prompt

Validate the implemented UI against the proposal summary, not just against build success.

For each touched route, tab, dialog, or overlay:

- Record route/window, viewport, Playwright MCP actions, assertions, screenshot path, and result.
- Confirm large stats are compact badges/stat strips.
- Confirm primary content uses large-screen width effectively.
- Confirm the UI is not a vertical pile of components.
- Confirm controls and text do not overlap.
- Confirm dialogs are readable, unclipped, and actions remain visible.
- Confirm touched markup prefers BaseLib components and does not add avoidable raw CSS or raw controls.

If screenshots do not match the proposal criteria, reopen the implementation subbundle and repair before closure.
