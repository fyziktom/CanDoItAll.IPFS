# Open Source Publishing Preparation

This bundle is a coordination and execution package for preparing `CanDoItAll.IPFS` for open-source publishing without performing implementation work in the preparation phase.

## Profile

- `initiative`

## Mission

- Produce an implementation-ready plan, checklist workbook, and subbundle sequence for reviewing, hardening, refactoring, validating, and documenting the application before it is published as open source. The target architecture must separate NodeControl workflows from Blazor UI so future non-UI hosts, including a CLI, can reuse node operations safely.

## Outcome Contract

- Requested outcome: a prepared initiative bundle with concrete source anchors, detailed checklist workbook, subbundle acceptance gates, and validation proof requirements.
- Hard constraints: preparation only; do not edit production source, do not add docker compose yet, and treat UI as a large desktop screen experience only.
- Evidence required before closure: prepared-stage validator pass, current-state evidence, normalized requirements, dependency map, traceability matrix, subbundle READMEs, and `bundle://inventories/publishing-prep-checklists.xlsx`.
- Known blockers or explicit scope exceptions: EF Core optimization must be recorded as not directly applicable because the inspected repo uses raw SQLite/JSON/file stores rather than Entity Framework Core.

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

1. `subbundles/01-sb01-publishing-baseline-and-risk-inventory`
2. `subbundles/02-sb02-open-source-metadata-and-dependency-hardening`
3. `subbundles/03-sb03-nodecontrol-layering-and-project-extraction`
4. `subbundles/04-sb04-persistence-and-docker-compose-runtime`
5. `subbundles/05-sb05-nodeoperator-service-decomposition`
6. `subbundles/06-sb06-large-screen-ui-component-decomposition`
7. `subbundles/07-sb07-engine-client-performance-hardening`
8. `subbundles/08-sb08-data-access-query-and-storage-hardening`
9. `subbundles/09-sb09-release-validation-documentation-and-handoff`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Passed prepared-stage validator`
- Execution status: `In progress; SB01-SB08 completed`
- Subbundle gate review: `SB01, SB02, SB03, SB04, SB05, SB06, SB07, and SB08 passed; SB09 next`
- Final closure gate: `Planned in SB09`
- Browser validation analytics: `SB04 compose smoke captured; SB06 large-screen route/modal proof captured; SB09 final rerun remains planned`
