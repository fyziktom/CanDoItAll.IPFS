# Implementation Prompt

Implement only the current subbundle.

Use the bundle files as the contract:

- Reopen `inputs/00-original-request.md`, `requirements/01-normalized-requirements.md`, `plan/01-phase-plan.md`, and the active subbundle README before editing.
- Use CanDoItAll BaseLib components before custom structure.
- Replace large stat cards with `CompactStatStrip`, `CompactStat`, `StatusBadge`, `Pill`, or equivalent compact shared components.
- Keep large-screen density first; do not tune medium or small layouts unless needed to avoid breaking the large-screen viewport.
- Do not add backend behavior.
- Capture or update Playwright screenshot proof while the rendered UI is fresh.
- Update `reviews/01-execution-report.md` after each subbundle.

Stop and repair the bundle if implementation reality contradicts any raw note or prerequisite.
