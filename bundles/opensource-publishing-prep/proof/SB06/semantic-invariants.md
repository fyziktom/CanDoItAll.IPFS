# SB06 Semantic Invariants

## UI Architecture Invariants

- Page markup may be paired with `.razor.cs` code-behind when it reduces mixed markup/handler responsibility.
- Large-screen desktop behavior must remain stable; SB06 must not introduce small or medium screen tuning.
- Pages should depend on the narrowest workflow interface available after SB05.
- UI-bound APIs, including Blazor browser file APIs and component-lifecycle PubSub subscriptions, must not be pushed into reusable workflow abstractions.
- `Files.razor.cs` may remain a route-state hotspot when a behavior-preserving extraction cannot be completed safely in the current pass, but the follow-up must be recorded.

## Behavioral Invariants Proven

- `/files`, `/content`, `/network`, and `/settings` load at `1920x1080` and `1600x900`.
- `RemotePinShareModal` opens from the Files route at both desktop viewports.
- Browser validation records no console errors, page errors, or failed requests after filtering expected teardown noise.
- Focused component tests continue to render affected pages and modal behavior after code-behind extraction.
- Build remains green after the UI split and import fixes.

## Measured Invariants

- `Content.razor` is 401 lines and `Content.razor.cs` is 397 lines.
- `Network.razor` is 377 lines and `Network.razor.cs` is 423 lines.
- `Settings.razor` is 229 lines and `Settings.razor.cs` is 222 lines.
- `RemotePinShareModal.razor` is 246 lines and `RemotePinShareModal.razor.cs` is 370 lines.
- `Files.razor.cs` remains 848 lines and is tracked as a future state-helper extraction candidate.
