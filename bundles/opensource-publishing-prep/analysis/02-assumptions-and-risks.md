# Assumptions And Risks

## Working Assumptions

- Publishing preparation should not change production source, project files, docker files, tests, or root documentation outside the bundle.
- The future non-UI/CLI scenario should be enabled by extracting NodeControl workflow contracts and implementations away from Blazor components, not by building the CLI in this initiative.
- The phrase "db" in the request refers to the current app data stores unless implementation intentionally introduces a separate database later.
- Large-screen desktop UI is the target; small and medium viewport work is out of scope unless a change obviously breaks basic rendering.

## Critical Path Risks

- Project extraction can create circular dependencies if UI view models, concrete persistence classes, and node workflows are moved without a dependency map.
- Docker persistence proof can be shallow if it verifies only container startup and not actual data survival after restart and rebuild.
- Publishing metadata changes can accidentally misrepresent inherited upstream licensing or package lineage.
- Performance cleanup can create behavior regressions if scan counts are treated as a mechanical rewrite list instead of a hot-path triage list.

## Validation Risks

- The baseline build already has many warnings; implementation must distinguish pre-existing warnings from regressions.
- Raw SQLite/file-store behavior is not covered by EF Core tooling; query/storage proof must use direct tests, schema inspection, and persistence scenarios.
- Blazor page decomposition can look successful while breaking busy states, overlays, modal focus, background refresh, or desktop dense layouts.
- Future implementers may skip browser proof if work seems "only refactoring"; SB06 and SB09 require Playwright evidence anyway.

## Reopen Triggers

- Reopen the bundle if a production source change is needed during preparation.
- Reopen the architecture plan if EF Core is introduced before SB08 runs.
- Reopen SB03/SB05 if CLI requirements arrive before the workflow boundary is implemented.
- Reopen SB04 if the docker runtime decision changes from local SQLite/JSON/file volumes to a separate database container.
