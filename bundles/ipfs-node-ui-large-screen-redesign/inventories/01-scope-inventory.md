# Scope Inventory

## Routes

| Route | File | Tabs/Primary States |
| --- | --- | --- |
| `/`, `/dashboard` | `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Home.razor` | Overview, Route notes |
| `/files`, `/files/explorer` | `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Files.razor` | Explorer, preview hidden/shown |
| `/content` | `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Content.razor` | Blocks + objects, DAG JSON, Naming + keys |
| `/network` | `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Network.razor` | Swarm, Topology, DHT, PubSub |
| `/settings` | `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Settings.razor` | Endpoint, Config, Maintenance |
| `/pin-requests` | `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\PinRequests.razor` | Request inbox and filters |
| `/logs` | `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\Logs.razor` | Window filters and log list |

## Dialogs And Overlays

| Surface | File |
| --- | --- |
| Upload dialog | `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\FilesComponents\FilesUploadDialog.razor` |
| File details dialog | `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\FilesComponents\FilesDetailDialog.razor` |
| File topology dialog | `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\FilesComponents\FilesTopologyDialog.razor` |
| Unpin dialog | `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\FilesComponents\FilesUnpinDialog.razor` |
| File item context menu | `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\FilesComponents\FilesItemContextMenu.razor` |
| Remote pin share modal | `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\RemotePinShareModal.razor` |
| Pin request details dialog | `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Pages\PinRequestsComponents\PinRequestDetailsDialog.razor` |

## Shared CSS Touch Boundary

- Primary allowed CSS touchpoints:
  - `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\Components\Layout\MainLayout.razor.css`
  - `C:\repositories\CanDoItAll.IPFS\src\CanDoItAll.IPFS.NodeControl\wwwroot\app.css`
- CSS edits should be limited to large-screen shell width, page density, legacy selector removal where replaced by BaseLib, and unavoidable large-screen layout tuning.
