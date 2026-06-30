# Scope Inventory

## In-Scope Projects

| Project | Scope reason |
| --- | --- |
| `repo://src/CanDoItAll.IPFS.Engine` | Runtime host, API, peer/network internals, package metadata, performance hot-path review. |
| `repo://src/CanDoItAll.IPFS.Client` | API client surface, package metadata, HTTP usage, CLI-reusable contracts. |
| `repo://src/CanDoItAll.IPFS.NodeControl` | Primary UI, workflow/persistence concentration, docker host configuration, desktop app publish readiness. |
| `repo://tests/CanDoItAll.IPFS.Tests` | Regression proof, warning baseline, new tests for refactors and persistence. |

## Primary File Hotspots

| File | Approx lines | Workstream |
| --- | ---: | --- |
| `repo://src/CanDoItAll.IPFS.NodeControl/wwwroot/app.css` | 1845 | SB06 |
| `repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeOperatorService.cs` | 1113 | SB03, SB05, SB07 |
| `repo://src/CanDoItAll.IPFS.Engine/Base/peer-talk/Swarm.cs` | 971 | SB07 |
| `repo://tests/CanDoItAll.IPFS.Tests/CoreApi/FileSystemApiTest.cs` | 920 | SB01, SB09 |
| `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Files.razor.cs` | 848 | SB06 |
| `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Content.razor` | 794 | SB06 |
| `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Network.razor` | 794 | SB06 |
| `repo://src/CanDoItAll.IPFS.Engine/Base/net-mdns/MulticastService.cs` | 633 | SB07 |
| `repo://src/CanDoItAll.IPFS.Engine/IpfsEngine.cs` | 631 | SB07 |
| `repo://src/CanDoItAll.IPFS.Engine/Base/net-ipfs-core/Cid.cs` | 619 | SB07 |
| `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/RemotePinShareModal.razor` | 607 | SB06 |
| `repo://src/CanDoItAll.IPFS.NodeControl/Services/ExplorerIndexStore.cs` | 464 | SB08 |
| `repo://src/CanDoItAll.IPFS.NodeControl/DesktopHost/DesktopAppProcessUtilities.cs` | 459 | SB04, SB07 |
| `repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Settings.razor` | 447 | SB06 |
| `repo://src/CanDoItAll.IPFS.NodeControl/Services/ApplicationLogStore.cs` | 417 | SB08 |
| `repo://src/CanDoItAll.IPFS.NodeControl/Program.cs` | 364 | SB03, SB04 |

## Checklist Workbook

- Detailed checklist workbook: `bundle://inventories/publishing-prep-checklists.xlsx`.
- Workbook sheets cover overview, architecture hotspots, open-source metadata/dependencies, docker persistence, UI decomposition, performance, storage/query hardening, validation evidence, and traceability.

## Explicitly Out Of Scope During Preparation

- Editing production source.
- Adding the root docker compose file.
- Building the future CLI.
- Tuning small or medium responsive UI behavior.
- Replacing SQLite/file stores with a new database without a separate architecture decision.
