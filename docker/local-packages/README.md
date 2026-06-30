# Local Docker Package Feed

The Docker build context cannot use the workstation-only `..\CanDoItAll\ExternalPackages` source from `NuGet.config`.

Until `CanDoItAll.Components.*` packages are published to a public feed, copy the required `.nupkg` files here before running `docker compose build`:

```powershell
New-Item -ItemType Directory -Force .\docker\local-packages
Copy-Item C:\repositories\CanDoItAll\ExternalPackages\CanDoItAll.Components.BaseLib.0.1.0.nupkg .\docker\local-packages\
Copy-Item C:\repositories\CanDoItAll\ExternalPackages\CanDoItAll.Components.CanvasLib.0.1.0.nupkg .\docker\local-packages\
```

The `.nupkg` files are intentionally ignored by git.
