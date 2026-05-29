# SB01 Semantic Invariants

## Invariant SB01-I1

- Invariant ID: `SB01-I1`
- Source raw note: `N002`
- Expected behavior: IPFS restore has a repo-local source for the ExternalPackages component feed.
- Disallowed shallow implementation: relying on a global NuGet source, global cache, or old source project path while claiming package migration is complete.
- Failing-first test: `bundle://proof/SB01/transcripts/sb01-failing-first-transcript.txt`
- Passing test: `bundle://proof/SB01/transcripts/sb01-passing-transcript.txt`
- Changed source files: `repo://NuGet.config`, `repo://Directory.Build.props`, `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj`
- Production assertions: `repo://NuGet.config` contains `CanDoItAllExternalPackages`; `bundle://proof/SB01/commands/package-assets-audit.txt` shows package assets resolved.
- Red-team negative case: old component source reference must fail the invariant.
- Downstream dependency check: SB02 used the package-fed app for runtime CSS and browser proof.

## Invariant SB01-I2

- Invariant ID: `SB01-I2`
- Source raw note: `N003`
- Expected behavior: NodeControl no longer depends on the old external CanDoItAll component project.
- Disallowed shallow implementation: leaving `CanDoItAllRepoRoot` or `ProjectReference` to `CanDoItAll.Components.csproj` in active project files.
- Failing-first test: `bundle://proof/SB01/transcripts/sb01-failing-first-transcript.txt`
- Passing test: `bundle://proof/SB01/transcripts/sb01-passing-transcript.txt`
- Changed source files: `repo://Directory.Build.props`, `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj`
- Production assertions: `bundle://proof/SB01/commands/post-source-audit.txt` shows package refs and no old source-project wiring.
- Red-team negative case: pre-edit `bundle://proof/SB01/commands/pre-source-audit.txt` shows the exact stale wiring that must not remain.
- Downstream dependency check: SB02 build/browser proof ran after the old source project reference was removed.

## Invariant SB01-I3

- Invariant ID: `SB01-I3`
- Source raw note: `N002`
- Expected behavior: package-provided BaseLib and CanvasLib compile for the IPFS solution.
- Disallowed shallow implementation: adding package references that restore but do not provide compile/static assets used by the app.
- Failing-first test: `bundle://proof/SB01/transcripts/sb01-failing-first-transcript.txt`
- Passing test: `bundle://proof/SB01/transcripts/sb01-passing-transcript.txt`
- Changed source files: `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj`
- Production assertions: `bundle://proof/SB01/commands/dotnet-build-no-restore.txt` passes with `0 Error(s)`.
- Red-team negative case: missing BaseLib or CanvasLib compile assets would fail build.
- Downstream dependency check: SB02 live browser proof rendered BaseLib and CanvasLib-dependent app surfaces.

## Invariant SB01-I4

- Invariant ID: `SB01-I4`
- Source raw note: `N001`
- Expected behavior: no placeholder or stub migration wiring was introduced.
- Disallowed shallow implementation: TODO markers, NotImplemented markers, placeholder package IDs, or disabled package/static asset paths.
- Failing-first test: `bundle://proof/SB01/transcripts/sb01-failing-first-transcript.txt`
- Passing test: `bundle://proof/SB01/transcripts/sb01-anti-stub-transcript.txt`
- Changed source files: `repo://NuGet.config`, `repo://Directory.Build.props`, `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj`
- Production assertions: `bundle://proof/SB01/transcripts/sb01-anti-stub-transcript.txt` explicitly states no stubs or blockers.
- Red-team negative case: placeholder package IDs or old project references would appear in the anti-stub audit.
- Downstream dependency check: SB02 accepted no disabled stylesheet or mocked CSS path.

## Production Behavior Artifact Matrix

No new production signal, state, record, or event was introduced in SB01.
