# SB09 Proof Manifest

## Subbundle

- SB09 Release Validation Documentation And Handoff
- Status: Completed
- Completion date: 2026-06-30

## Implementation Summary

- Completed final release validation after the NodeControl abstraction, workflow, UI, performance, storage, docker, and packaging subbundles.
- Fixed final validation regressions found during real tests: remote block fetches now preserve remote pins, upload progress tests wait for the expected progress value, and multi-node pin tests assert the fetched CID remains listed as pinned.
- Ran full solution tests, focused regression tests, package/advisory checks, release package generation, Docker multi-node e2e, and large-screen browser smoke.
- Regenerated bundle traceability and workbook closure artifacts.
- Semantic invariant contract: `bundle://proof/SB09/semantic-invariants.json`.

## Semantic Adequacy Proof

- Failing-first transcript: `bundle://proof/SB09/transcripts/test-final-full-after-pinapi-fix.txt`.
- Passing transcript: `bundle://proof/SB09/transcripts/test-final-full-after-progress-fix.txt`.
- Anti-stub audit transcript: `bundle://proof/SB09/transcripts/release-risk-marker-scan.txt`.
- Red-team verifier artifact: `bundle://proof/SB09/fake-proof-resistance-audit.md`.

## Validation Evidence

| Evidence | Result |
| --- | --- |
| `bundle://proof/SB09/transcripts/test-final-full-after-progress-fix.txt` | Full suite passed: 383 total, 372 passed, 11 skipped. |
| `bundle://proof/SB09/transcripts/focused-progress-and-pin-after-fix.txt` | Focused upload progress and remote pin regression tests passed. |
| `bundle://proof/SB09/transcripts/focused-pin-regression-after-remote-pin-fix.txt` | Focused pin API and multi-node pin regression tests passed. |
| `bundle://proof/SB09/transcripts/package-vulnerable-final-after-fixes.txt` | Vulnerability scan reported no vulnerable packages. |
| `bundle://proof/SB09/transcripts/build-pack-engine-final-after-fixes.txt` | Engine release package and symbols package were produced. |
| `bundle://proof/SB09/transcripts/build-pack-client-final-after-fixes.txt` | Client release package and symbols package were produced. |
| `bundle://proof/SB09/transcripts/docker-multinode-e2e.txt` | Docker multi-node add/pin/fetch/pin-list/unpin workflow passed with persistence after restart/rebuild. |
| `bundle://proof/SB09/docker-multinode-e2e-summary.json` | Machine-readable Docker e2e summary captured node IDs, CID, and persistence booleans. |
| `bundle://proof/SB09/browser-smoke-summary.json` | Files, Content, Network, Settings, and `RemotePinShareModal` were captured at both desktop viewports with no console errors, page errors, or failed requests. |
| `bundle://proof/SB09/transcripts/release-risk-marker-scan.txt` | Remaining TODO/NotImplemented-style markers were reviewed and deferred as inherited protocol/unsupported capability follow-up work. |
| `bundle://proof/SB09/transcripts/bundle-validator-completed.txt` | Completed-stage bundle validator passed. |

## Browser Screenshots

| Route or modal | 1920x1080 | 1600x900 |
| --- | --- | --- |
| Files route | `bundle://proof/SB09/screenshots/SB09-files-1920x1080.png` | `bundle://proof/SB09/screenshots/SB09-files-1600x900.png` |
| Content route | `bundle://proof/SB09/screenshots/SB09-content-1920x1080.png` | `bundle://proof/SB09/screenshots/SB09-content-1600x900.png` |
| Network route | `bundle://proof/SB09/screenshots/SB09-network-1920x1080.png` | `bundle://proof/SB09/screenshots/SB09-network-1600x900.png` |
| Settings route | `bundle://proof/SB09/screenshots/SB09-settings-1920x1080.png` | `bundle://proof/SB09/screenshots/SB09-settings-1600x900.png` |
| `RemotePinShareModal` | `bundle://proof/SB09/screenshots/SB09-remote-pin-share-modal-1920x1080.png` | `bundle://proof/SB09/screenshots/SB09-remote-pin-share-modal-1600x900.png` |

## Docker E2E Summary

- Project: `candoitallipfssb09`
- Node A: `QmbHdoXrNQEZGsADjHSxsM13F5tY7XCC9yhxibm9onbQmG`
- Node B: `QmWaH2nbSW6QVgwuzkhw9YzaicVAhQymFdGgLs9dZzYRUS`
- CID: `QmZWQuVsqg5FoTLPn3PAek6kbgN8QgScaPCrL8mj8iNhYo`
- Node A persistence after restart/rebuild: true
- Node B pin add/list/unpin workflow: true

## Package Artifacts

- `.artifacts/packages/CanDoItAll.IPFS.Engine.0.42.0.nupkg`
- `.artifacts/packages/CanDoItAll.IPFS.Engine.0.42.0.snupkg`
- `.artifacts/packages/CanDoItAll.IPFS.Client.0.42.0.nupkg`
- `.artifacts/packages/CanDoItAll.IPFS.Client.0.42.0.snupkg`

## Changed File Hashes

| SHA-256 | File |
| --- | --- |
| `0b9b166af718f41471f584bdd9afb30b17e43c58bf59f9eace9b091fc1805798` | `repo://src/CanDoItAll.IPFS.Engine/CoreApi/PinApi.cs` |
| `83e03cafcc831a23b4796ab75323492e79ffcc5de82d1df3c5519ef7fcc432ce` | `repo://tests/CanDoItAll.IPFS.Tests/CoreApi/MultiNodePinWorkflowTest.cs` |
| `f5c17547bcc760841ef9564593eaa10ebfb45dc132ca03b7a5ba5359678a52c2` | `repo://tests/CanDoItAll.IPFS.Tests/CoreApi/FileSystemApiTest.cs` |

## Notes

- The final browser stack used alternate host ports `5103` and `5193` because the SB04 validation stack was still occupying host port `5001`; no unrelated containers were stopped.
- The Docker e2e stack was taken down without deleting volumes, preserving the validation data.
- The full test run can leave an Engine child process behind; the stale validation process was stopped after the successful run.
