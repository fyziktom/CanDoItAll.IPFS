# Component NuGet Package Migration

This bundle coordinates the component-package migration and visual regression proof for `component-nuget-package-migration`.

## Profile

- `initiative`

## Mission

Move the IPFS node-control app off the old in-repo CanDoItAll component project reference and onto the component NuGet packages produced in `C:\repositories\CanDoItAll\ExternalPackages`, while proving the shared BaseLib `output.css` static web asset still resolves and the economy component-consuming apps remain visually equivalent before and after the migration.

## Outcome Contract

- Requested outcome: local restore uses the ExternalPackages feed, `CanDoItAll.IPFS.NodeControl` references split component packages instead of `$(CanDoItAllRepoRoot)src\CanDoItAll.Components`, and UI/static-asset proof confirms no visible regression.
- Hard constraints: do not edit the component package source repo; do not replace shared component usage with custom markup or CSS; preserve existing app `output.css` links unless proof shows a package path change is required.
- Evidence required before closure: package/reference source assertions, `dotnet restore/build/test` transcripts, static asset endpoint proof for `_content/CanDoItAll.Components.BaseLib/css/output.css`, and Playwright MCP before/after screenshots for IPFS and economy apps.
- Known blockers or explicit scope exceptions: Economy already has a NuGet.config and package references, so Economy is a validation target unless fresh analysis discovers an old external component project reference.

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

## Recommended Execution Order

1. `subbundles/01-01-package-source-and-reference-migration`
2. `subbundles/02-02-build-static-assets-and-browser-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Pass`
- Final closure gate: `Pass`
- Browser validation analytics: `Pass`
