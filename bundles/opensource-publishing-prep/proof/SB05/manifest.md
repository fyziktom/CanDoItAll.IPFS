# SB05 Proof Manifest

## Subbundle

- SB05 NodeOperator Service Decomposition
- Status: Completed
- Completion date: 2026-06-29

## Implementation Summary

- Added UI-neutral workflow interfaces in `CanDoItAll.IPFS.NodeControl.Abstractions`.
- Split the former mixed `NodeOperatorService` implementation into concrete workflow services:
  - `NodeFileWorkflowService`
  - `NodeExplorerWorkflowService`
  - `NodeContentWorkflowService`
  - `NodeNetworkWorkflowService`
  - `NodeMaintenanceWorkflowService`
- Kept `NodeOperatorService` as a compatibility facade so current pages remain stable until SB06 migrates route dependencies directly to narrower workflow services.
- Kept browser-file upload outside the abstraction interfaces because it depends on Blazor `IBrowserFile`.

## Validation Evidence

| Evidence | Result |
| --- | --- |
| `bundle://proof/SB05/transcripts/build-after-nodeoperator-decomposition.txt` | Full solution build passed after the initial workflow split. |
| `bundle://proof/SB05/transcripts/focused-nodeoperator-decomposition-tests.txt` | 26 focused workflow/composition/decomposition tests passed. |
| `bundle://proof/SB05/transcripts/build-after-files-smoke-adjustment.txt` | Full solution build passed after smoke-test alignment. |
| `bundle://proof/SB05/transcripts/nodeoperator-page-smoke-tests-passing.txt` | 7 bUnit route smoke tests passed through decomposed workflow registrations. |
| `bundle://proof/SB05/transcripts/nodeoperator-line-counts-after-decomposition.txt` | `NodeOperatorService` reduced to 134 lines; workflow services split by responsibility. |
| `bundle://proof/SB05/transcripts/nodeoperator-public-surface-after-decomposition.txt` | Public surface recorded across the facade, workflow services, and workflow interfaces. |

## Responsibility Reduction

- Before SB05: `NodeOperatorService.cs` had approximately 1113 lines and mixed file, explorer, content, network, config, repo, preview, and index responsibilities.
- After SB05: `NodeOperatorService.cs` has 134 lines and delegates to explicit workflow services.
- The longest extracted service is `NodeExplorerWorkflowService.cs` at 512 lines because it owns virtual-folder navigation, pinned-root indexing, and preview mapping.

## Changed File Hashes

| SHA-256 | File |
| --- | --- |
| `a3a61ba1012128560d8acbebce768b7a8653e69cbb433ae86b68b3be95156883` | `repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions/INodeWorkflows.cs` |
| `9a451b0091644407327b70490240547521ffa7a137ccc6b17bb8f742f5234a4c` | `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeOperatorService.cs` |
| `3e8c342d0d3ca6a13ad5003f7ceb1259242e34b0aa9e5b18d7e0e4055a9d67d2` | `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeFileWorkflowService.cs` |
| `85de65904d4cea0c0ce6b668ba3a063c1978424995fcaa61d571d6b72d9a6e09` | `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeExplorerWorkflowService.cs` |
| `6942a7e31d355d68d59e6117703b78ede4dd669c45160f2dbabe982e8994c12b` | `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeContentWorkflowService.cs` |
| `d20f1c7fc8aca76e27c56b59cce9d7b8017f7320e7db8a8393edfde25ecc63bf` | `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeNetworkWorkflowService.cs` |
| `bfd91518767103421adb5d9ba617a73a83aa5b80b9731b110f5765002609cc07` | `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeMaintenanceWorkflowService.cs` |
| `9002ee6da2e01d1ddef463892497560c32ced1618ff16f81911f93e877200c34` | `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeOperatorShared.cs` |
| `7d3233829121380114348bda36d0bd842a591fa060b83ff11aec00496aab1522` | `repo://src/CanDoItAll.IPFS.NodeControl/Composition/NodeControlServiceCollectionExtensions.cs` |
| `f75f8eec992dbdda84ea97aa3beec4c6fd74dde203de554c2789752d55d957d7` | `repo://tests/CanDoItAll.IPFS.Tests/NodeControl/NodeOperatorTestHarness.cs` |
| `5956a5f4780b1f3b9eda05f8b7405eb9695cb265375465f0fba29f1cadc55a7f` | `repo://tests/CanDoItAll.IPFS.Tests/NodeControl/NodeOperatorDecompositionTests.cs` |
| `c5c312d0dc9f011f9b7bb07548e28bd0e34a8909cb6021bb34aac5b9fef3b934` | `repo://tests/CanDoItAll.IPFS.Tests/NodeControl/NodeControlCompositionTests.cs` |
| `b8adbff6db8a3a058bd8d7fd96d234b790e11a43f763f082b63c2f09148fdd54` | `repo://tests/CanDoItAll.IPFS.Tests/NodeControl/FilesExplorerUiTests.cs` |
| `f69123b10ab801d13d7a88edfc304d064a4c51377b7d1acd882dc4009fa441d9` | `repo://tests/CanDoItAll.IPFS.Tests/NodeControl/HomeDashboardComponentTests.cs` |
| `344d8c27f968d20fbc8c0d928464da9b90235372e85608d8bacbcfb4998cb0c8` | `repo://tests/CanDoItAll.IPFS.Tests/NodeControl/NodeControlPageSmokeTests.cs` |
| `82ab4045559f66ee692418135544341f2f746689acb63f87cc6c1d3ca2dc846e` | `bundle://architecture/01-target-solution.md` |
| `dbd35679f9919a196874033ce2d51d5a78d456b252251d139e741c9f4bec6d22` | `bundle://reviews/01-execution-report.md` |
| `2c6f4fbc47585837d05a0ad57ea47d1a84fa1076d3578ad4baf2cec841bf9a82` | `bundle://subbundles/05-sb05-nodeoperator-service-decomposition/README.md` |
| `315b8fca96a3ae5c2d26fbc7790622ef3f3aa44a537c60941ad0615221c85fcf` | `bundle://traceability/01-requirement-traceability.md` |
| `31cb9ea0492c9cca85f5028c9a6777d351fb7ce0576fb465e5960b6a6550755c` | `bundle://tools/build-workbook.mjs` |
| `8394f31bf8376a5fb8ece647d563a2e34a139edb43a5879f3f637ee897900a02` | `bundle://inventories/publishing-prep-checklists.xlsx` |

## Notes

- The facade is intentionally retained to reduce UI blast radius; SB06 owns direct page dependency cleanup and component decomposition.
- Page smoke tests were updated to assert currently visible tabbed-route content instead of hidden inactive-tab panels.
