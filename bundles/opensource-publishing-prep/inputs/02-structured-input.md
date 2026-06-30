# Structured Input

## Core Objective

- Prepare an implementation-ready initiative bundle for publishing `CanDoItAll.IPFS` as open source.
- Identify architecture, maintainability, UI decomposition, data persistence, performance, query/storage, packaging, docker, and release-validation work before implementation starts.
- Plan reusable project boundaries so future non-UI usage, especially a CLI, can call node-control workflows without depending on Blazor UI.

## Success Criteria

- Raw request is preserved and mapped to normalized requirements.
- Current-state analysis cites concrete repo paths and the baseline build result.
- Subbundles have prerequisites, exact source references, acceptance criteria, proof requirements, browser/host logging, and progression gates.
- A detailed `.xlsx` checklist exists at `bundle://inventories/publishing-prep-checklists.xlsx`.
- Prepared-stage bundle validation passes before implementation starts.

## Hard Constraints

- Preparation only: do not implement source refactors, do not add docker compose, and do not edit production code during this phase.
- Treat the UI as a large-screen desktop experience only.
- Preserve the raw request literally and keep every requested concern traceable.
- Use the requested .NET performance and EF Core query optimization perspectives while recording the actual repo reality when EF Core is absent.
- Use portable `repo://` and `bundle://` references in durable bundle artifacts.

## Allowed Side Effects

- Bundle files under `repo://bundles/opensource-publishing-prep`.
- Checklist workbook under `bundle://inventories/publishing-prep-checklists.xlsx`.
- No production source, project, docker, test, or README implementation changes during preparation.

## Source Artifacts

- `bundle://inputs/00-original-request.md`
- `repo://CanDoItAll.IPFS.slnx`
- `repo://src/CanDoItAll.IPFS.Engine`
- `repo://src/CanDoItAll.IPFS.Client`
- `repo://src/CanDoItAll.IPFS.NodeControl`
- `repo://tests/CanDoItAll.IPFS.Tests`

## Input Coverage Signals

- Open-source publishing readiness.
- Messy/large/mixed-responsibility parts.
- NodeControl isolation for future non-UI/CLI use.
- Large-screen desktop UI only.
- .NET performance scan.
- EF Core query optimization perspective with EF absence recorded.
- Root docker compose with persisted database and file data.
- Detailed `.xlsx` checklist and plan.

## Dependency And Sequencing Signals

- Baseline inventory must precede source edits.
- Project/layer extraction must precede service decomposition and future CLI-safe work.
- Docker persistence path design depends on runtime/persistence boundaries.
- UI decomposition should follow workflow-service decomposition.
- Final release validation depends on all previous subbundles.

## Validation Expectations

- Build/test/package/docker/browser proof must be captured as transcripts or screenshots during implementation.
- Critical subbundles must produce proof manifests and semantic invariant contracts.
- UI work must use large desktop viewports first.
- Docker work must mutate data, restart containers, rebuild images, and verify persistence.

## Evidence Contract

- `dotnet build CanDoItAll.IPFS.slnx`
- Targeted `dotnet test` runs for touched areas, plus full test pass before closure.
- Package validation and dependency advisory checks.
- Docker compose up/restart/rebuild transcripts with persistence checks.
- Playwright screenshots and assertions for large desktop UI routes.
- Critical proof manifests under `bundle://proof/SBxx`.

## UI Validation Strategy

- Use `1920x1080` and `1600x900` large-screen desktop viewports for SB06 and final release smoke.
- Validate route load, dense content fit, dialogs, menus, overlays, tab state, and no console errors.
- Do not require small or medium viewport tuning unless a change catastrophically breaks resizing.

## Browser Validation Analytics

- SB06 and SB09 must populate `bundle://reviews/01-execution-report.md` browser analytics rows with route, viewport, Playwright actions, screenshot paths, and pass/fail result.
- SB04 must populate host/docker proof rows instead of browser rows.

## Working Assumptions

- The docker compose implementation should be planned in SB04 because adding it now would violate preparation-only scope.
- The current "db" surface means SQLite/JSON/log persistence unless implementation intentionally introduces a separate database.
- Future CLI implementation is a follow-up, but the project extraction must make it feasible.

## Primary Risks

- Wrong project boundaries can force Blazor dependencies into future CLI code.
- Shallow docker proof can miss data loss after rebuild.
- Treating all performance scan hits as hot-path issues can create risky churn.
- Assuming EF Core exists would miss the actual SQLite/file-store issues.
