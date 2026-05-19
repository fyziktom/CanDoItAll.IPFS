# Assumptions And Risks

## Assumptions

- Large desktop viewport proof is sufficient because the user explicitly made large-screen-only a hard rule.
- Existing BaseLib components should be used even when a small amount of raw markup would be faster.
- Current behavior should remain unchanged; this bundle is a UI structure and density pass, not a backend feature pass.
- Existing legacy CSS can remain when not touched by the redesign, because broad cleanup would add risk without directly improving the requested UI.

## Critical Path Risks

- Replacing large summary tiles everywhere may reveal that some values are too long for compact badges. Mitigation: shorten visible values and keep full values in tooltip/helper text where component support exists.
- Removing vertical stacks too aggressively can make form flows harder to scan. Mitigation: use `Grid`, `Row`, `Column`, `Stack`, and `Tabs` based on task grouping, not arbitrary horizontal packing.
- Dialog validation depends on reachable UI states. Some dialogs require data or actions; if live data is unavailable, proof must document the blocker and use route/test fixture evidence where possible.
- Existing custom CSS and raw HTML are broad. Attempting to remove all of it in one pass risks unrelated regressions. Mitigation: stop at touched surfaces and record any legacy CSS as out-of-scope residual debt.

## Validation Risks

- Playwright screenshots can show a disconnected/loading state if the local IPFS node or app startup is still hydrating. Proof must record the route state and wait for stable visible content before judging layout.
- Generated image boards are not pixel specs. Comparison should use structural criteria: compact stats, split workspace, tab separation, reduced vertical stacking, and absence of large empty regions.
- The app uses Blazor Server and may need manual refresh after Razor edits. Use the managed dotnetwatch MCP wait loop and refresh the same Playwright page after watch settles.

## Reopen Triggers

- Any touched route still shows large `SummaryTiles` in primary stat positions after implementation.
- A route/tab screenshot shows the main action/results area below the fold because of avoidable vertical stacking.
- A dialog open-state screenshot clips content, overflows laterally, or buries primary actions.
- New raw structural CSS or raw controls are introduced where a BaseLib component would work.
- Playwright proof cannot be tied to a screenshot path, route, viewport, and comparison result.
