# QA Prompt

You are validating one completed subbundle from `bundle://README.md` for `CanDoItAll.IPFS`.

Read the subbundle README, `bundle://plan/01-phase-plan.md`, `bundle://traceability/01-requirement-traceability.md`, and `bundle://reviews/01-execution-report.md`.

QA checks:

- Confirm the subbundle did not implement out-of-scope work.
- Confirm all `## Acceptance Checklist` items have concrete proof.
- Confirm every `## Proof Required` bullet has a command transcript, test result, browser screenshot, docker transcript, or proof artifact.
- Confirm downstream dependencies listed in the subbundle README were not broken.
- For SB06 and SB09, verify large desktop Playwright evidence and console status.
- For SB04, verify data survives compose restart and image rebuild.
- For SB08, verify the work targets raw SQLite/JSON/file stores rather than inventing EF Core work.
