# SB08 Data Access Query And Storage Hardening

## Status

- `Completed`

## Completion Evidence

- EF absence scan: `bundle://proof/SB08/transcripts/ef-core-marker-scan-after-sb08-start.txt`
- Build proof: `bundle://proof/SB08/transcripts/build-after-storage-hardening.txt`
- Focused storage tests: `bundle://proof/SB08/transcripts/focused-storage-tests.txt`
- SQLite/storage source proof: `bundle://proof/SB08/transcripts/sqlite-storage-source-proof.txt`
- Manifest: `bundle://proof/SB08/manifest.md`

## Objective

- Apply the EF Core query optimization intent to the actual persistence implementation: raw SQLite, JSON stores, and log files.
- Harden query shapes, indexes, path configuration, concurrency, bounded reads, parameter typing, and persistence tests.

## Covered Inputs

- R008 EF/query optimization perspective with EF absence recorded.
- R009 docker persistence data safety.
- R003 storage responsibility cleanup.

## Prerequisites

- SB01 confirms EF Core remains absent or documents any change.
- SB04 defines docker/local durable paths.
- SB05 stabilizes workflow service consumers of persistence.

## Exact Source References

- repo://src/CanDoItAll.IPFS.NodeControl/Services/ExplorerIndexStore.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Services/ApplicationLogStore.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Services/RemotePinRequestStore.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Services/ServerNodeSettingsStore.cs
- repo://src/CanDoItAll.IPFS.NodeControl/appsettings.json
- repo://src/CanDoItAll.IPFS.NodeControl/Program.cs
- repo://tests/CanDoItAll.IPFS.Tests
- bundle://analysis/01-current-state.md
- bundle://inventories/publishing-prep-checklists.xlsx

## Deliverables

- Confirmation that EF Core is absent or a revised EF-specific plan if it has been introduced.
- SQLite schema/index review for `ExplorerIndexStore`.
- Safer query parameterization/typing, bounded reads, and transaction/write-lock behavior where needed.
- JSON/log store hardening for atomic writes, rotation cost, path configurability, and corruption/error handling.
- Tests that cover persistence and query behavior.

## Dependency Impact

- SB04 docker proof depends on stable persisted paths and data semantics.
- SB09 cannot close release validation if persistence can corrupt or lose app data.

## Validation Depth

- Data-critical storage and query validation.

## Implementation Steps

1. Re-run EF marker scans and record whether EF remains absent.
2. Review `ExplorerIndexStore` schema, indexes, SQL, parameter usage, and locking.
3. Review JSON store read/write/atomicity and error handling.
4. Review log rotation for repeated full reads or interactive-path cost.
5. Implement targeted hardening with tests.
6. Re-run docker persistence proof if changes affect SB04 paths.
7. Update workbook, execution report, and proof manifest.

## Do Not Do

- Do not invent EF Core work when no DbContext exists.
- Do not replace the persistence model without an explicit architecture decision.
- Do not make unbounded reads worse.
- Do not change persisted formats without migration/backward compatibility proof.

## Acceptance Checklist

- EF absence or presence is explicitly verified.
- SQLite queries/indexes are reviewed and hardened where needed.
- JSON/log store durability and bounded behavior are tested or documented.
- Docker/local persistence paths are still correct.
- Build and focused storage tests pass.

## Proof Required

- EF marker scan transcript.
- Storage-focused test transcript.
- SQLite schema/query proof where applicable.
- Docker persistence re-check if paths or persisted formats changed.
- `bundle://proof/SB08/manifest.md` with changed-file hashes and portable references.

## Browser Validation Logging

- N/A unless UI-visible storage behavior changes.
- If file explorer persistence behavior changes, validate `/files` at `1920x1080` with a refresh/navigation scenario and screenshot.

## Progression Gate

- SB09 may proceed only after persistence and query/storage proof is complete and no data-loss risk remains open.

## Suggested Agent Prompt

```text
Implement SB08 only. Reconfirm EF Core absence, harden the actual SQLite/JSON/log stores, prove storage behavior with tests and persistence checks, and do not replace the persistence model without reopening architecture.
```
