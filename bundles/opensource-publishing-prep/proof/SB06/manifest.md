# SB06 Proof Manifest

## Subbundle

- SB06 Large Screen UI Component Decomposition
- Status: Completed
- Completion date: 2026-06-30

## Implementation Summary

- Split `Content`, `Network`, `Settings`, and `RemotePinShareModal` into `.razor` markup plus `.razor.cs` code-behind files.
- Migrated `Content`, `Network`, `Settings`, `Home`, and `Files` away from the broad `INodeOperator` facade where narrower workflow interfaces exist.
- Preserved direct `IpfsClientFactory` usage in `Network` for the live PubSub subscription path because that behavior is tied to component lifecycle.
- Left `Files.razor.cs` as a documented future state-helper extraction candidate after verifying its existing child components and narrower workflow dependencies.
- Captured large desktop browser proof only, at `1920x1080` and `1600x900`, per the bundle constraint.

## Validation Evidence

| Evidence | Result |
| --- | --- |
| `bundle://proof/SB06/transcripts/build-after-ui-codebehind-complete.txt` | Full solution build passed after UI code-behind/import fixes. |
| `bundle://proof/SB06/transcripts/focused-ui-component-tests.txt` | 15 focused UI/component tests passed. |
| `bundle://proof/SB06/transcripts/browser-smoke-playwright-passing-filtered.txt` | Playwright browser smoke completed successfully. |
| `bundle://proof/SB06/browser-smoke-summary.json` | `/files`, `/content`, `/network`, `/settings`, and `RemotePinShareModal` were captured at both desktop viewports with no console errors, page errors, or failed requests. |
| `bundle://proof/SB06/transcripts/ui-line-counts-after-codebehind-split.txt` | Line counts recorded after the code-behind split. |
| `bundle://proof/SB06/transcripts/workbook-regenerate-after-sb06.txt` | Checklist workbook regenerated from source. |

## Browser Screenshots

| Route or modal | 1920x1080 | 1600x900 |
| --- | --- | --- |
| `/files` | `bundle://proof/SB06/screenshots/sb06-files-1920x1080.png` | `bundle://proof/SB06/screenshots/sb06-files-1600x900.png` |
| `/content` | `bundle://proof/SB06/screenshots/sb06-content-1920x1080.png` | `bundle://proof/SB06/screenshots/sb06-content-1600x900.png` |
| `/network` | `bundle://proof/SB06/screenshots/sb06-network-1920x1080.png` | `bundle://proof/SB06/screenshots/sb06-network-1600x900.png` |
| `/settings` | `bundle://proof/SB06/screenshots/sb06-settings-1920x1080.png` | `bundle://proof/SB06/screenshots/sb06-settings-1600x900.png` |
| `RemotePinShareModal` | `bundle://proof/SB06/screenshots/sb06-remote-pin-share-modal-1920x1080.png` | `bundle://proof/SB06/screenshots/sb06-remote-pin-share-modal-1600x900.png` |

## Changed File Hashes

| SHA-256 | File |
| --- | --- |
| `2016c562b88624e32e12077df42cfd4781c8b365a652f657bd8d8c4a48c435a4` | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Content.razor` |
| `54a6a7395eee85384f5df5c1302c046a7a4bdc0c2deeaef34a82d83c12ba67b0` | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Content.razor.cs` |
| `e1b2b1b54eace9d8f5e8a27bba8bd57e142b5f66787c5e784528f6b5511feb06` | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Network.razor` |
| `f9a60ff66325e542081757c97165320d77976a905fb5a293819e4156eff7eba0` | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Network.razor.cs` |
| `6e0bfd9c3a0ce3f5e2da7f0c2422389e39e38876f3b038b4bd8a4ee0d6f3d44c` | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Settings.razor` |
| `73116bc161762c2a92fba94bfec6a64499512013298cdcfc4465fd3ef43e5466` | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Settings.razor.cs` |
| `ef109adbc05a0607c552acae3b7cf5e4a92d0f91adfbf063fbf40f79bb46d88f` | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/RemotePinShareModal.razor` |
| `a19c1caebb5b303ab0a0b00b441df6c6b639a70bef0cfa05de2bb55618367d4b` | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/RemotePinShareModal.razor.cs` |
| `cfee307dc20dd3f62b3c6a48d4a7d8afe82504d45d978487806040e01e65d569` | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Files.razor` |
| `21b401b35df12b154ad475294cf1e7feb6d4332fb80bc37b94580ca0f36a0c92` | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Files.razor.cs` |
| `8a2735dd97ac6d524f39116af509f11c4277b75566a0b26a094c18ac89dfce80` | `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Home.razor` |
| `d015bedc5e558e4f0cbbb411fa700643368ac5f541e82def3c02eb0386dfcfcf` | `bundle://proof/SB06/browser-smoke.cjs` |
| `bbde23a6c6c185a97fecc02afc0f5abe7ea904971279aa86d4e10e970179562c` | `bundle://proof/SB06/browser-smoke-summary.json` |
| `447503cf021347b4ddfca3522aeeaa88d5adac611cc0352e72faf2dcf5fdbe50` | `bundle://inventories/publishing-prep-checklists.xlsx` |
| `3de90a3c2bdb3af39acb3c30a6ba5a6de01ee4902950f19798d3a35fce7e470e` | `bundle://tools/build-workbook.mjs` |
| `73c65f8b1a49fd0295c51072f66c59cd98302d6d45219046f5135f01c6ee7023` | `bundle://reviews/01-execution-report.md` |
| `920e3e49d26079069839c4412851373c61eb2c01e6cfb26396b4bb7132541fe7` | `bundle://architecture/01-target-solution.md` |
| `65197a39b93d22956e668740c49fa9349d85f759b23adccbd82f2d558932563f` | `bundle://traceability/01-requirement-traceability.md` |

## Notes

- `Files.razor.cs` remains 848 lines. The SB06 split preserved behavior while reducing page dependency breadth; a later helper extraction should focus on route state, upload actions, and explorer cache orchestration.
- Playwright was installed into a temporary runtime outside the repository for SB06 browser proof. No `node_modules` directory was added to the repo.
