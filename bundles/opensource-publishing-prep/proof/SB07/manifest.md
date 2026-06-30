# SB07 Proof Manifest

## Subbundle

- SB07 Engine Client Performance Hardening
- Status: Completed
- Completion date: 2026-06-30

## Implementation Summary

- Hardened `DohClient` HTTP behavior by replacing per-instance lazy client construction with a shared fallback client and preserving explicit client injection.
- Changed DoH requests to use `ResponseHeadersRead`, dispose responses, dispose linked timeout tokens, and pass cancellation into response stream reading.
- Updated `NodeNetworkWorkflowService` to await task results after `Task.WhenAll` instead of using `.Result`.
- Updated known-node API resolution to use `IHttpClientFactory` and the existing NodeControl named-client policy.
- Cached health-check JSON serializer options instead of allocating options on every response.
- Documented broad performance scan deferrals rather than rewriting cold or protocol-sensitive paths mechanically.

## Validation Evidence

| Evidence | Result |
| --- | --- |
| `bundle://proof/SB07/transcripts/performance-scan-before-sb07.txt` | Full scan profile captured before SB07 fixes. |
| `bundle://proof/SB07/transcripts/performance-scan-after-sb07.txt` | Full scan profile captured after SB07 fixes. |
| `bundle://proof/SB07/transcripts/production-targeted-scan-after-sb07.txt` | Production targeted scan shows zero manual `HttpClient`, zero ad hoc JSON options, and zero `.Result`/`.Wait` in `NodeNetworkWorkflowService`. |
| `bundle://proof/SB07/transcripts/build-after-sb07-performance-fixes.txt` | Full solution build passed after SB07 fixes. |
| `bundle://proof/SB07/transcripts/focused-performance-tests.txt` | 16 focused composition, HTTP client policy, lease factory, and network workflow tests passed. |
| `bundle://proof/SB07/performance-triage.md` | Fixed/deferred scan findings documented with rationale. |
| `bundle://proof/SB07/transcripts/workbook-regenerate-after-sb07.txt` | Checklist workbook regenerated from source. |

## Scan Delta

| Category | Before | After | Notes |
| --- | ---: | ---: | --- |
| Manual `HttpClient` construction | 4 | 3 | Remaining broad-scan hits are test harness clients; production targeted count is zero. |
| `.Result`/`.Wait` candidates | 82 | 81 | `NodeNetworkWorkflowService` candidate removed; remaining hits are tests and inherited DNS/MDNS sync surfaces. |
| Ad hoc `JsonSerializerOptions` construction | 1 | 0 | Health-check response writer now uses cached options. |

## Changed File Hashes

| SHA-256 | File |
| --- | --- |
| `fd326c56850f88c8b1fec05e4c7961c7b2cc8361cd1a19d97339c0b5627f02ba` | `repo://src/CanDoItAll.IPFS.Engine/Base/net-udns/DohClient.cs` |
| `cadcc72c42af4247f25c09c8902ad6fca69ebde02078a32fae9ae3ba5d2f7760` | `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeNetworkWorkflowService.cs` |
| `2effb832bea160cc6b7c9d9bf429ef7b0a8cca934690ab041e8a194858df4ab1` | `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeControlHealthCheckResponseWriter.cs` |
| `45f02987f8d305880c8cd1e50242f00311ea61765dc5b8750aa76c5dbebc17ee` | `bundle://proof/SB07/performance-triage.md` |
| `acb7ff6eba7ce82a98b6fdb4c6acdde49b329a28794b46bc21197c5b412c7bae` | `bundle://inventories/publishing-prep-checklists.xlsx` |
| `da029fdfc201133e80462f78743e1fddd6dfa21924df97642c9ac3823fe405be` | `bundle://tools/build-workbook.mjs` |
| `331abe29910f6c834b9d8f65bc7306f80923a3ac6f420d785595c4e58a0a7f7c` | `bundle://reviews/01-execution-report.md` |
| `e18c53107f412edd0c0f2543a5fe031f312c2e9538bd12037e285e0f6ffaa813` | `bundle://architecture/01-target-solution.md` |
| `856059ed5f930fe71bbfd8fb26eabd6a1111efe9f707d4c3000527f8883967f1` | `bundle://traceability/01-requirement-traceability.md` |
| `0ec06a5c9c67fbc54b0bb81f9e0fb61caa49c11307c4f8206c01545bcd9548a8` | `bundle://subbundles/07-sb07-engine-client-performance-hardening/README.md` |

## Notes

- The EF Core optimization skill does not directly apply because the repo has no EF Core usage. SB08 continues the same query-performance lens against raw SQLite and file-backed stores.
- No browser validation was required for SB07 because no UI behavior changed.
