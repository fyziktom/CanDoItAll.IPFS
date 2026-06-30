# SB04 Proof Manifest

## Subbundle

- SB04 Persistence And Docker Compose Runtime
- Status: Completed
- Completion date: 2026-06-29

## Runtime Topology

- `ipfs-node` builds `src/CanDoItAll.IPFS.Engine` and exposes the Engine API on container port `5001`.
- `node-control` builds `src/CanDoItAll.IPFS.NodeControl` and exposes the Blazor UI on container port `8080`.
- Local context only: the NodeControl container targets the Engine service through the compose service name and API port.
- `IPFS_PASS` is required from the caller environment and is not committed to source.
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.json`.

## Semantic Adequacy Proof

- Failing-first: N/A process/non-production exemption; SB04 introduced runtime wiring and persistence proof rather than a failing unit test.
- Passing transcript: `bundle://proof/SB04/transcripts/docker-compose-restart-and-verify.txt`.
- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/docker-compose-config.txt`.

## Durable Data Surfaces

- Local context only: IPFS repository and pinned blocks live under the container data path for volume `ipfs-node-data`.
- Local context only: NodeControl settings live under the container data path for volume `node-control-data`.
- Local context only: remote pin requests live under the container data path for volume `node-control-data`.
- Local context only: explorer index database lives under the container data path for volume `node-control-data`.
- Local context only: application logs live under the container data path for volume `node-control-data`.

## Validation Evidence

| Evidence | Result |
| --- | --- |
| `bundle://proof/SB04/transcripts/build-after-compose-config.txt` | Full solution build passed after compose/config changes. |
| `bundle://proof/SB04/transcripts/focused-nodecontrol-persistence-config-tests.txt` | Focused NodeControl persistence configuration tests passed. |
| `bundle://proof/SB04/transcripts/docker-compose-config.txt` | Compose config rendered successfully. |
| `bundle://proof/SB04/transcripts/docker-compose-build-after-restore-fixes.txt` | Compose images built successfully after SDK and restore fixes. |
| `bundle://proof/SB04/transcripts/docker-compose-up.txt` | Compose stack started and reached healthy state. |
| `bundle://proof/SB04/transcripts/write-durable-data.txt` | Real IPFS content was added/pinned and a NodeControl remote pin request was stored. |
| `bundle://proof/SB04/transcripts/docker-compose-restart-and-verify.txt` | CID, peer identity, and remote pin request persisted after restart. |
| `bundle://proof/SB04/transcripts/docker-compose-rebuild-no-cache.txt` | Images rebuilt successfully without cache. |
| `bundle://proof/SB04/transcripts/docker-compose-rebuild-and-verify.txt` | CID, peer identity, and remote pin request persisted after rebuild/recreate. |
| `bundle://proof/SB04/transcripts/playwright-dashboard-screenshot.txt` | Host-visible NodeControl screenshot captured at `1920x1080`. |
| `bundle://proof/SB04/nodecontrol-dashboard-compose-1920x1080.png` | Visual UI proof that compose-hosted NodeControl rendered. |

## Persisted Proof Identifiers

- Persisted CID: `QmVj1xyP5jyhYsQkjGbj91eNrK1Tfg2zjtuJd3FiFJtpK5`.
- Persisted peer ID: `QmSTEVhYuLAc6SVjuxdgndLnFTGmVRrGjy7NPqPUHba5FK`.
- Persisted remote pin request ID: `sb04-46adc2a7a15b4d19accdbed51b7b01d7`.

## Changed File Hashes

| SHA-256 | File |
| --- | --- |
| `be8bc38165d6caf3ba4ba118320e335fcc91b302ef4584478d1a4ae8128229bb` | `repo://.dockerignore` |
| `9633fff5668041e988603ac83de41843034c953bb3f4162a78fb02f494697dcd` | `repo://docker-compose.yml` |
| `ff3aa928d20e6ed50ee39c956319017183d9db96fb890ef21a9e26c58bf79e9b` | `repo://docker/Dockerfile.ipfs-node` |
| `af9d4739345f11bbfbd772a814d843fffcebbae125792a88716961f0532e4559` | `repo://docker/Dockerfile.node-control` |
| `7f912771f2aa58fff922e0218bc45f16eb9a74b9f18879f4d37c0d05eb577048` | `repo://docker/NuGet.Docker.config` |
| `e106a40565c51f816f186802d76364b89c311b2cf2af5b911cbdc51a1e0411c0` | `repo://docker/local-packages/README.md` |
| `dd7269590b82fc235ffc51dde55508dfb006bd7fab33b1a5c8798ee4e205a963` | `repo://src/CanDoItAll.IPFS.NodeControl/appsettings.Container.json` |
| `dec96d6ab6b18ec2fee5061bcf005d83f8004038813c1c12ef99c5ed868407bd` | `repo://src/CanDoItAll.IPFS.NodeControl/Composition/NodeControlServiceCollectionExtensions.cs` |
| `611ee93dbc56fb8d6a95797e4ba74ff1df8a1c3618af57d6f1ead4665db311f8` | `repo://tests/CanDoItAll.IPFS.Tests/NodeControl/NodeControlCompositionTests.cs` |
| `5a3343a7316fb6c52182341551c1fc627623fe45db37ec3383a0147f3a965a74` | `repo://README.md` |
| `cade9666631e88faf27944853bb1017f04dee1c9e6972ee5f90d799124edb8f6` | `bundle://architecture/01-target-solution.md` |
| `ab7e322f4cd31507edc1f8284869b333418aa1076a857f5ca49555d5d179ef1d` | `bundle://reviews/01-execution-report.md` |
| `280c31fa2715f2bf78ee71e5da91d789e6a69ec49ee336ee73bed108a03ad266` | `bundle://subbundles/04-sb04-persistence-and-docker-compose-runtime/README.md` |
| `d07baf5a0dcbcb5342dcc5785260c7d3dcf3c63b6bb1970c414417298d61cdef` | `bundle://traceability/01-requirement-traceability.md` |
| `5cfbc811ad90e23cd127d3cb402516c3e5c24cb98a6a6c59a4f2f17ceb963192` | `bundle://tools/build-workbook.mjs` |
| `4333ddec329b83106fb2537f8366d9323e910f6a3c3c9cc1a039d4a8ed7faacc` | `bundle://inventories/publishing-prep-checklists.xlsx` |

## Notes

- Docker build initially failed on SDK/global.json mismatch and private package restore path issues; the accepted proof is the later successful transcript after pinning the build SDK image and using `docker/NuGet.Docker.config`.
- Local `CanDoItAll.Components.*.nupkg` files were copied into `docker/local-packages` for validation but are intentionally ignored by source control.
