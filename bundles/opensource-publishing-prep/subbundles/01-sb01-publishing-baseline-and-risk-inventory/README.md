# SB01 Publishing Baseline And Risk Inventory

## Status

- `Completed`

## Objective

- Refresh and lock the source-backed baseline before any publishing, refactoring, docker, or UI work begins.
- Convert the current architecture/performance/storage findings into a maintained checklist so downstream subbundles do not work from stale assumptions.

## Covered Inputs

- R001 preparation-only boundary once implementation starts.
- R002 publishing baseline.
- R003 messy part inventory.
- R007 performance scan as initial triage evidence.
- R008 EF absence and storage-query reality check.
- R010 detailed `.xlsx` checklist upkeep.

## Prerequisites

- none

## Exact Source References

- repo://CanDoItAll.IPFS.slnx
- repo://global.json
- repo://Directory.Build.props
- repo://src/CanDoItAll.IPFS.Engine/CanDoItAll.IPFS.Engine.csproj
- repo://src/CanDoItAll.IPFS.Client/CanDoItAll.IPFS.Client.csproj
- repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj
- repo://tests/CanDoItAll.IPFS.Tests/CanDoItAll.IPFS.Tests.csproj
- bundle://analysis/01-current-state.md
- bundle://inventories/01-scope-inventory.md
- bundle://inventories/publishing-prep-checklists.xlsx

## Deliverables

- Updated baseline command transcript for build, warnings, vulnerability advisories, file hotspots, EF absence, and performance scan counts.
- Workbook rows updated from `Planned` to the correct SB01 status.
- Execution report gate row updated with the refreshed baseline proof.

## Dependency Impact

- SB02 depends on accurate package/dependency metadata.
- SB03, SB05, and SB06 depend on the hotspot inventory.
- SB07 depends on the performance scan being triaged as leads, not automatic edits.
- SB08 depends on the EF absence and raw SQLite/file-store finding.

## Validation Depth

- Critical foundation and process-critical closure.

## Implementation Steps

1. Re-run `dotnet build CanDoItAll.IPFS.slnx --no-restore` and record warnings/errors.
2. Re-run package/advisory and project metadata scans.
3. Re-run line-count and large-file scans for source, tests, and CSS.
4. Re-check EF Core absence using source-wide searches for EF Core markers.
5. Refresh the workbook status, owner, and proof columns for SB01 findings.
6. Update `bundle://reviews/01-execution-report.md` with exact command outcomes and downstream dependency decision.

## Do Not Do

- Do not edit production source.
- Do not add docker compose.
- Do not begin metadata, refactoring, UI, performance, or storage implementation.
- Do not hide baseline warnings by suppressing analyzers.

## Acceptance Checklist

- Baseline build result is recorded with command and exit code.
- Vulnerability/package warning summary is recorded.
- Large-file and responsibility hotspots are current.
- EF absence is rechecked and recorded.
- Workbook SB01 rows are updated.
- Downstream subbundles can rely on this baseline without conversational context.

## Proof Required

- Command transcript for `dotnet build CanDoItAll.IPFS.slnx --no-restore`.
- `rg` or equivalent scan transcript for EF Core markers.
- File-size/hotspot scan transcript.
- Updated `bundle://inventories/publishing-prep-checklists.xlsx`.
- Updated `bundle://reviews/01-execution-report.md` SB01 row.

## Browser Validation Logging

- N/A: this subbundle does not change browser-visible or host-visible UI behavior.
- Record `N/A` in the browser analytics row if SB01 validation is mentioned there.

## Progression Gate

- SB02, SB03, SB04, SB07, and SB08 may start only after SB01 records a refreshed baseline and no unexplained source changes exist.

## Suggested Agent Prompt

```text
Implement SB01 only. Refresh the baseline evidence, update the workbook and execution report, do not edit production source, and stop if the current repo state differs enough that downstream subbundle scopes need to be reopened.
```
