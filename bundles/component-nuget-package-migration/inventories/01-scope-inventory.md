# Scope Inventory

| Area | In Scope | Out Of Scope | Notes |
| --- | --- | --- | --- |
| IPFS package restore | Add/verify local package source for ExternalPackages. | Global machine NuGet config changes. | Prefer repo-local config. |
| IPFS project references | Replace old external component project reference. | Internal IPFS Engine/Client project references. | Engine/Client stay as project refs. |
| Component packages | BaseLib and CanvasLib direct references; transitive Common/OverlayLib as needed. | Rebuilding or changing package contents. | Add direct Common only if compile proves needed. |
| Static assets | BaseLib `output.css`, material icons, CanvasLib assets in source/published paths. | New app-specific Tailwind build. | Existing `App.razor` should remain correct if package static assets work. |
| Economy apps | Browser before/after screenshots for representative component-consuming apps. | Full screenshot coverage of every Economy route. | Use apps with BaseLib/Charts/Mermaid usage. |
