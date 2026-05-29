# SB02 Semantic Invariants

## Invariant SB02-I1

- Invariant ID: `SB02-I1`
- Source raw note: `N004`
- Expected behavior: BaseLib `output.css` is served from the package static-web-asset path at runtime.
- Disallowed shallow implementation: accepting a screenshot while `_content/CanDoItAll.Components.BaseLib/css/output.css` returns HTML, 404, or empty content.
- Failing-first test: N/A process/non-production validation subbundle; adversarial CSS guard is in the passing transcript.
- Passing test: `bundle://proof/SB02/transcripts/sb02-browser-validation-transcript.txt`
- Changed source files: `repo://NuGet.config`, `repo://Directory.Build.props`, `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj`
- Production assertions: `bundle://proof/SB02/browser/playwright-proof-summary.md` records 200 text/css length 174525 for IPFS and Economy routes.
- Red-team negative case: a response that starts with HTML or has non-200 status fails the invariant.
- Downstream dependency check: IPFS and Economy visual checks use the same runtime CSS path.

## Invariant SB02-I2

- Invariant ID: `SB02-I2`
- Source raw note: `N004`
- Expected behavior: IPFS NodeControl remains styled after package migration.
- Disallowed shallow implementation: a page that loads but has missing BaseLib shell, icons, buttons, tabs, section cards, or route content.
- Failing-first test: N/A process/non-production validation subbundle; early loading capture was rejected and recaptured after content loaded.
- Passing test: `bundle://proof/SB02/transcripts/sb02-browser-validation-transcript.txt`
- Changed source files: `repo://NuGet.config`, `repo://Directory.Build.props`, `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj`
- Production assertions: `bundle://proof/SB02/browser/ipfs-after.png` and `bundle://proof/SB02/commands/screenshot-diff-summary.txt`.
- Red-team negative case: unstyled controls, missing icons, or layout collapse would fail screenshot review.
- Downstream dependency check: Economy routes were validated after IPFS proof.

## Invariant SB02-I3

- Invariant ID: `SB02-I3`
- Source raw note: `N005`
- Expected behavior: representative Economy apps remain visually equivalent before and after.
- Disallowed shallow implementation: validating only IPFS while ignoring Economy apps that consume BaseLib/Charts/Mermaid packages.
- Failing-first test: N/A process/non-production validation subbundle; requirement would fail if Economy screenshots were missing.
- Passing test: `bundle://proof/SB02/transcripts/sb02-browser-validation-transcript.txt`
- Changed source files: `repo://NuGet.config`, `repo://Directory.Build.props`, `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj`
- Production assertions: `bundle://proof/SB02/browser/economy-components-demo-after.png`, `bundle://proof/SB02/browser/economy-simulator-after.png`, and `bundle://proof/SB02/browser/playwright-proof-summary.md`.
- Red-team negative case: missing chart panels, unstyled action buttons, collapsed project rail, or missing BaseLib CSS would fail visual review.
- Downstream dependency check: final closure uses both Economy screenshot pairs.

## Invariant SB02-I4

- Invariant ID: `SB02-I4`
- Source raw note: `N001`
- Expected behavior: static asset contract remains covered by focused automated tests and no browser rows are skipped.
- Disallowed shallow implementation: skipping tests, disabling stylesheet links, or using a mocked CSS endpoint.
- Failing-first test: N/A process/non-production validation subbundle; broader unrelated suite failures are documented as residual risk rather than hidden.
- Passing test: `bundle://proof/SB02/transcripts/sb02-browser-validation-transcript.txt`
- Changed source files: `repo://NuGet.config`, `repo://Directory.Build.props`, `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj`
- Production assertions: `bundle://proof/SB02/commands/dotnet-test-static-assets-pin-components.txt` passed, and `bundle://proof/SB02/transcripts/sb02-anti-stub-transcript.txt` reports no skipped browser rows or mocked CSS.
- Red-team negative case: disabled stylesheet links, mocked endpoints, or omitted screenshots fail the invariant.
- Downstream dependency check: final bundle closure cites focused tests plus browser proof.

## Production Behavior Artifact Matrix

No new production signal, state, record, or event was introduced in SB02.
