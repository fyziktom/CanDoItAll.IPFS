# CanDoItAll.IPFS

`CanDoItAll.IPFS` hosts the embedded IPFS engine, typed client, and the browser-based node control app used to inspect, upload, pin, and manage a local or remote node.

### Solution layout

- `src/CanDoItAll.IPFS.Engine` contains the embedded node and HTTP API host.
- `src/CanDoItAll.IPFS.Client` contains the typed HTTP client for node operations.
- `src/CanDoItAll.IPFS.NodeControl` contains the Blazor node control app.
- `tests/CanDoItAll.IPFS.Tests` contains unit, integration, and UI-facing tests that remain relevant after the migration.

### External shared dependencies

The node control app references shared UI libraries from the sibling `CanDoItAll` repository instead of copying them into this repo.

- Default shared repo root: `C:\repositories\CanDoItAll`
- Override path with MSBuild property: `CanDoItAllRepoRoot`

### Running locally

Set a passphrase and a repository path in the same shell you use to start the app:

```powershell
$env:IPFS_PASS = "Choose-A-Strong-Passphrase"
$env:IPFS_PATH = "C:\\ipfs-data\\local-node"
dotnet run --project .\\src\\CanDoItAll.IPFS.NodeControl\\CanDoItAll.IPFS.NodeControl.csproj
```

The control app will connect to the configured node endpoint and will auto-start the local engine when the target URL resolves to the current machine and nothing is listening yet.
