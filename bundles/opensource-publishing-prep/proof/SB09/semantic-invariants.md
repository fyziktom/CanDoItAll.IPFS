# SB09 Semantic Invariants

## Release Validation Invariants

- Final closure must rely on real build, test, package, vulnerability, Docker, and browser evidence rather than earlier baseline-only proof.
- A remotely fetched block that is explicitly pinned must remain visible through pin listing after the fetch completes.
- Docker validation must include at least two nodes and prove add/pin/fetch/pin-list/unpin behavior across node boundaries.
- Docker validation must preserve IPFS and NodeControl data across restart/rebuild using named volumes.
- Browser validation is desktop-only and must cover `1920x1080` and `1600x900`; small and medium viewport tuning is out of scope.
- Remaining inherited TODO/NotImplemented-style protocol markers are follow-up work unless final tests, docs, or package claims make them reachable release blockers.

## Behavioral Invariants Proven

- The full solution test suite passed after the final pin/progress fixes.
- Engine and Client release packages and symbols packages were produced.
- Vulnerability scan reported no vulnerable packages.
- Docker multi-node e2e proved node A persistence and node B remote pin/unpin behavior.
- `/files`, `/content`, `/network`, and `/settings` load at both desktop viewports with no browser errors.
- `RemotePinShareModal` opens from the Files route at both desktop viewports.

## Handoff Invariants

- `bundle://reviews/01-execution-report.md`, `bundle://traceability/01-requirement-traceability.md`, and `bundle://inventories/publishing-prep-checklists.xlsx` must remain aligned with this manifest.
- CLI/non-UI work is enabled by extracted contracts and workflow boundaries, but CLI implementation remains a later bundle.
