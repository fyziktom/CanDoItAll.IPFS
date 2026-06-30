# SB08 Proof Manifest

## Subbundle

- SB08 Data Access Query And Storage Hardening
- Status: Completed
- Completion date: 2026-06-30

## Implementation Summary

- Reconfirmed that EF Core remains absent; no `DbContext`, `DbSet`, `EntityFrameworkCore`, `UseSqlite`, or `UseSqlServer` markers were found.
- Added an Explorer SQLite index for the pinned-root list query shape: `IsPinned`, `IsDirectory DESC`, `DisplayName COLLATE NOCASE`, and `Target`.
- Replaced `ExplorerIndexStore` `AddWithValue` calls with explicit SQLite text/integer parameter helpers.
- Normalized pinned target collections in one pass before generating `IN` and `NOT IN` updates.
- Updated application log rotation to count the active file once per store lifetime or after rotation, then maintain the active count in memory.
- Added focused storage tests for runtime index presence, normalized target updates, and log rotation after store reload.
- Semantic invariant contract: `bundle://proof/SB08/semantic-invariants.json`.

## Semantic Adequacy Proof

- Failing-first: N/A process/non-production exemption; SB08 hardened existing storage internals and records semantic negative checks in focused storage/source proof.
- Passing transcript: `bundle://proof/SB08/transcripts/focused-storage-tests.txt`.
- Anti-stub audit transcript: `bundle://proof/SB08/transcripts/sqlite-storage-source-proof.txt`.

## Validation Evidence

| Evidence | Result |
| --- | --- |
| `bundle://proof/SB08/transcripts/ef-core-marker-scan-after-sb08-start.txt` | EF Core remains absent. |
| `bundle://proof/SB08/transcripts/sqlite-storage-source-proof.txt` | Source proof shows schema/index artifacts, typed parameters, target normalization, log rotation accounting, and zero `AddWithValue` in `ExplorerIndexStore`. |
| `bundle://proof/SB08/transcripts/build-after-storage-hardening.txt` | Full solution build passed. |
| `bundle://proof/SB08/transcripts/focused-storage-tests.txt` | 20 focused storage/composition tests passed. |
| `bundle://proof/SB08/transcripts/workbook-regenerate-after-sb08.txt` | Checklist workbook regenerated from source. |

## Docker Persistence Note

- SB08 did not change container paths or JSON persisted document formats.
- The Explorer SQLite change is additive and backwards-compatible: existing databases receive the new index through `CREATE INDEX IF NOT EXISTS`.
- Docker persistence proof is therefore deferred to the final SB09 rerun rather than repeated in SB08.

## Changed File Hashes

| SHA-256 | File |
| --- | --- |
| `db6f01cf0564fdc7bddea3c5d883b600301d55367582a32591bba4f242695c08` | `repo://src/CanDoItAll.IPFS.NodeControl/Services/ExplorerIndexStore.cs` |
| `6bc4c382a0168c1f340452311142d94a487ad28199e42072fe9108298eb4bace` | `repo://src/CanDoItAll.IPFS.NodeControl/Services/ApplicationLogStore.cs` |
| `fbeb718d99124ccee6dcf450897c912bcc158d2693a940e15803679920ff395e` | `repo://tests/CanDoItAll.IPFS.Tests/NodeControl/ExplorerIndexStoreTests.cs` |
| `6f198ece33cc867432646be476067fd4389980631056435b5f56b5087b6fb735` | `repo://tests/CanDoItAll.IPFS.Tests/NodeControl/ApplicationLogStoreTests.cs` |
| `a7d2c55aa4a004b121ca4102c6d7d32f973c07ce2a05571e33687cbdf0432dc5` | `bundle://inventories/publishing-prep-checklists.xlsx` |
| `495b6855a9c07c2455c97bda9b3f0b6f714662f23af45f26ae6347dd278caf90` | `bundle://tools/build-workbook.mjs` |
| `034885ff64048725c072f7fdbf3261edca704bcc92dafeac3a56faf4d52e08c2` | `bundle://reviews/01-execution-report.md` |
| `f60bea6292b521dc55277741a8d2ecd06c56780abfad1629a9236424b464fe46` | `bundle://architecture/01-target-solution.md` |
| `f1c0b58182459914aede4a0e4c22813d1514068a140dbd005b013778c4eb6113` | `bundle://traceability/01-requirement-traceability.md` |
| `f8cc075e396f20852c0ce6f0b9a444824ee1b93cd7c7d545644830ff9e0d4244` | `bundle://subbundles/08-sb08-data-access-query-and-storage-hardening/README.md` |

## Notes

- JSON settings/request stores already had schema documents, atomic write backups, and corruption quarantine tests; SB08 preserved their persisted formats and reran those tests.
- Application log read remains bounded by the requested window and maximum entry count after deserialization; the selected fix targeted write-path rotation cost.
