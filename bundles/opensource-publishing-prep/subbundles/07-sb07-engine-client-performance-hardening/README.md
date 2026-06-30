# SB07 Engine Client Performance Hardening

## Status

- `Completed`

## Completion Evidence

- Scan before: `bundle://proof/SB07/transcripts/performance-scan-before-sb07.txt`
- Scan after: `bundle://proof/SB07/transcripts/performance-scan-after-sb07.txt`
- Production targeted scan: `bundle://proof/SB07/transcripts/production-targeted-scan-after-sb07.txt`
- Build proof: `bundle://proof/SB07/transcripts/build-after-sb07-performance-fixes.txt`
- Focused tests: `bundle://proof/SB07/transcripts/focused-performance-tests.txt`
- Triage rationale: `bundle://proof/SB07/performance-triage.md`

## Objective

- Use the .NET performance scan as a triage guide to fix high-value async, allocation, collection, string, HTTP, and lifecycle issues before publication.

## Covered Inputs

- R007 .NET performance analysis.
- R003 messy long runtime files where performance and lifecycle issues can hide.
- R011 final release validation support.

## Prerequisites

- SB01 baseline and scan counts are refreshed.
- SB03 boundaries are complete if NodeControl performance work touches reusable workflow services.

## Exact Source References

- repo://src/CanDoItAll.IPFS.Engine/IpfsEngine.cs
- repo://src/CanDoItAll.IPFS.Engine/Base/peer-talk/Swarm.cs
- repo://src/CanDoItAll.IPFS.Engine/Base/net-mdns/MulticastService.cs
- repo://src/CanDoItAll.IPFS.Engine/Base/net-ipfs-core/Cid.cs
- repo://src/CanDoItAll.IPFS.Client
- repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeOperatorService.cs
- repo://src/CanDoItAll.IPFS.NodeControl/DesktopHost/DesktopAppProcessUtilities.cs
- repo://tests/CanDoItAll.IPFS.Tests
- bundle://analysis/01-current-state.md
- bundle://inventories/publishing-prep-checklists.xlsx

## Deliverables

- Prioritized performance issue list with rationale for fix, defer, or no-op.
- Targeted fixes for confirmed hot/risky issues, especially blocking waits, async correctness, cancellation, manual `HttpClient` construction, repeated allocations, and stream handling.
- Focused tests or benchmarks for changed hot paths.

## Dependency Impact

- SB09 depends on final release confidence and no new performance/lifecycle regressions.
- SB05 and SB08 may expose smaller seams for safer performance fixes.

## Validation Depth

- Targeted performance hardening with tests or benchmark-style evidence for selected fixes.

## Implementation Steps

1. Re-run the performance scans from SB01 and group findings by risk and execution frequency.
2. Prioritize issues in Engine/Client/NodeControl hot paths rather than mechanical rewrite counts.
3. Fix a small, well-evidenced set of issues.
4. Add tests, benchmark harnesses, or command-based before/after evidence appropriate to each fix.
5. Ensure cancellation, exceptions, and resource disposal remain correct.
6. Update workbook, execution report, and any proof artifacts.

## Do Not Do

- Do not rewrite broad LINQ/string code mechanically without proof.
- Do not trade readability for micro-optimizations in cold paths.
- Do not suppress warnings instead of fixing or documenting them.
- Do not change protocol behavior without tests.

## Acceptance Checklist

- Each fixed performance issue has a rationale and proof.
- Deferred findings have explicit reasons.
- Build and focused tests pass.
- No new blocking waits, `async void`, or manual `HttpClient` construction are introduced.
- Workbook performance rows are updated.

## Proof Required

- Scan transcript before/after for relevant categories.
- Build transcript.
- Focused test or benchmark transcript for changed paths.
- Execution report row with fixed/deferred counts.

## Browser Validation Logging

- N/A unless NodeControl UI-visible performance work changes route behavior.
- If UI route behavior changes, smoke affected route at `1920x1080` and record screenshot/console status.

## Progression Gate

- SB09 may proceed only after selected performance fixes are proven and deferred scan findings are documented.

## Suggested Agent Prompt

```text
Implement SB07 only. Treat performance scan results as triage leads, fix only high-value verified issues, preserve protocol behavior, run focused proof, and document deferred findings clearly.
```
