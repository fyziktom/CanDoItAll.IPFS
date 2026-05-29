# 01-package-source-and-reference-migration

## Status

- `Completed`

## Objective

Configure IPFS to restore CanDoItAll component packages from the local ExternalPackages feed and replace the old external component project reference with package references.

## Success Criteria

- IPFS has a repo-local package source for `..\CanDoItAll\ExternalPackages`.
- `CanDoItAllComponentsPackageVersion` is defined for package references.
- `CanDoItAll.IPFS.NodeControl.csproj` references BaseLib and CanvasLib packages and no longer references the old external `CanDoItAll.Components.csproj`.
- Restore/build source assertions prove package wiring is not relying on the old project.

## Covered Inputs

- R1, R2, R3, R4.
- Raw notes N001, N002, N003.

## Prerequisites

- Prepared bundle files and readiness validator have passed or any validator gaps are documented in the execution report.
- Current-state source references in `bundle://analysis/01-current-state.md` have been checked against the repo immediately before editing.

## Exact Source References

- `repo://Directory.Build.props`
- `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj`
- `repo://src/CanDoItAll.IPFS.NodeControl/Components/App.razor`
- `C:\repositories\CanDoItAll\ExternalPackages`
- `C:\repositories\CanDoItAll.Economy\NuGet.config`

## Deliverables

- Add `repo://NuGet.config` if missing, matching the Economy local feed pattern.
- Add or preserve `CanDoItAllComponentsPackageVersion` at `0.1.0`.
- Remove obsolete `CanDoItAllRepoRoot` if it no longer has a use.
- Replace the old component `ProjectReference` with package references to `CanDoItAll.Components.BaseLib` and `CanDoItAll.Components.CanvasLib`.

## Dependency Impact

- SB02 depends on this subbundle.
- If the app still compiles through the old component project or a stale global cache path, browser proof cannot establish that the NuGet package migration works.

## Validation Depth

- Critical foundation with semantic adequacy gate.

## Implementation Steps

1. Capture pre-edit source assertions for current project references and package sources.
2. Add repo-local NuGet package source for ExternalPackages.
3. Define `CanDoItAllComponentsPackageVersion` as `0.1.0`.
4. Replace the external component project reference with direct package references.
5. Run restore/build source audits and update `reviews/01-execution-report.md`.

## Scope Exceptions

- Economy visual validation is owned by SB02.

## Do Not Do

- Do not edit package source repos or package contents.
- Do not change UI markup or CSS.
- Do not remove IPFS Engine/Client project references.

## Acceptance Checklist

- [x] `NuGet.config` contains `CanDoItAllExternalPackages`.
- [x] Active IPFS project files contain no `ProjectReference` to `CanDoItAll.Components`.
- [x] Build or restore resolves package IDs `CanDoItAll.Components.BaseLib` and `CanDoItAll.Components.CanvasLib`.
- [x] `App.razor` still points to BaseLib static web asset paths for `material-icons.css` and `output.css`.

## Proof Required

- `rg -n "CanDoItAllRepoRoot|ProjectReference.*CanDoItAll.Components|CanDoItAll.Components.(BaseLib|CanvasLib)" -S`
- `dotnet restore CanDoItAll.IPFS.slnx`
- `dotnet build CanDoItAll.IPFS.slnx --no-restore`
- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`

## Browser Validation Logging

- N/A for this subbundle; browser-visible proof is blocked until package wiring builds and is owned by SB02.

## Progression Gate

- SB02 may start only when source assertions and build/restore proof show IPFS uses package references for component libraries and has no old external component project reference.

## Semantic Adequacy Gate

- Shallow-pass trap: a build that still uses the old project reference must fail the gate even if the app runs.
- Adversarial negative proof: source search must reject old `ProjectReference`/`CanDoItAllRepoRoot` wiring after migration.
- Semantic positive proof: restore/build must compile package-provided BaseLib/CanvasLib APIs used by the app.
- Anti-stub audit: no placeholder package IDs, disabled CSS links, or TODO migration comments are allowed.
- Raw-note literal closure: N002 and N003 must cite package feed and no-old-project-reference proof.

## Suggested Agent Prompt

```text
Implement SB01 only.
Configure the local ExternalPackages feed, replace the old external CanDoItAll component project reference with package references, run the source/build audits, create proof/SB01 artifacts, update reviews/01-execution-report.md, and stop before browser validation.
```
