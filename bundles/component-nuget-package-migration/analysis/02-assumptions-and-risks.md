# Assumptions And Risks

## Assumptions

- A1: Version `0.1.0` is the intended package version because every package currently present in ExternalPackages uses that version and Economy already centralizes that version.
- A2: IPFS needs direct references to `CanDoItAll.Components.BaseLib` and `CanDoItAll.Components.CanvasLib`; transitive dependencies should bring `Common` and `OverlayLib` unless compilation proves a direct reference is needed.
- A3: Economy is already on the package path, so its role is regression validation rather than source migration unless a stale external component project reference is found.

## Critical Path Risks

- CR1: If package references are wrong, build/test/browser proof is invalid.
- CR2: If the local NuGet feed is not configured portably enough, a clean restore can fall back to stale cache or fail on another checkout.
- CR3: If output.css is absent, apps may load structurally but visually regress across IPFS and Economy.

## Validation Risks

- VR1: Existing global NuGet cache may mask missing feed config; proof must include source assertions and restored package path evidence when practical.
- VR2: Economy apps may require different ports or hosted dependencies; blocked apps need an explicit validation gap, not silent omission.
- VR3: Screenshot diff can vary from live data; review should focus on shared component styling, icons, spacing, and absence of raw unstyled HTML.

## Reopen Triggers

- RT1: Any build error mentioning `CanDoItAll.Components.*` after migration reopens SB01.
- RT2: HTTP/browser proof for `_content/CanDoItAll.Components.BaseLib/css/output.css` returning non-200 or empty CSS reopens SB01 and SB02.
- RT3: Economy after screenshots showing missing BaseLib styling, icons, chart chrome, or layout collapse reopens SB02 and may require package/feed repair.
