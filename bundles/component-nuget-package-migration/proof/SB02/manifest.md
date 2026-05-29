# SB02 Proof Manifest

## Subbundle

- Subbundle ID: `SB02`
- Status: `Completed`
- Owned requirements: `R1`, `R5`, `R6`, `R7`
- Source raw notes: `N001`, `N004`, `N005`
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`

## Changed Files

| Path | SHA-256 |
| --- | --- |
| `repo://NuGet.config` | `55DB7974246108F4C53121F44B488AFDD0EBC4230BF9315242D767AA187AD931` |
| `repo://Directory.Build.props` | `F8543151FC6349CEDC652028259FBA38C32D0CC4653B0ECBC9A4876EC2321718` |
| `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj` | `0558472FA337AE8F077474CC24A86D99B42FA249493737D52E0C2AD3283E8FA7` |

## Transcripts

- Command transcript: `bundle://proof/SB02/transcripts/sb02-browser-validation-transcript.txt`
- Failing-first: N/A process/non-production validation subbundle with no additional behavior change beyond SB01; adversarial negative CSS guard is recorded in the passing transcript.
- Passing transcript: `bundle://proof/SB02/transcripts/sb02-browser-validation-transcript.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/sb02-anti-stub-transcript.txt`

## Supporting Artifacts

- Focused passing tests: `bundle://proof/SB02/commands/dotnet-test-static-assets-pin-components.txt`
- Full-suite gap transcript: `bundle://proof/SB02/commands/dotnet-test-no-build.txt`
- Wider NodeControl gap transcript: `bundle://proof/SB02/commands/dotnet-test-nodecontrol-filter.txt`
- Source assertion: `bundle://proof/SB02/commands/final-source-audit.txt`
- Source assertion: `bundle://proof/SB02/commands/final-package-source-audit.txt`
- Screenshot diff summary: `bundle://proof/SB02/commands/screenshot-diff-summary.txt`
- Browser proof summary: `bundle://proof/SB02/browser/playwright-proof-summary.md`

## Browser Artifacts

| Route | Screenshot Pair | Result |
| --- | --- | --- |
| IPFS `/` | `bundle://proof/SB02/browser/ipfs-before.png`, `bundle://proof/SB02/browser/ipfs-after.png` | Pass: BaseLib CSS 200, styled shell and dashboard retained. |
| Economy Components Demo `/` | `bundle://proof/SB02/browser/economy-components-demo-before.png`, `bundle://proof/SB02/browser/economy-components-demo-after.png` | Pass: BaseLib CSS 200, charts/action surfaces retained. |
| Economy Simulator `/` | `bundle://proof/SB02/browser/economy-simulator-before.png`, `bundle://proof/SB02/browser/economy-simulator-after.png` | Pass: BaseLib CSS 200, simulator shell/forms/tabs retained. |

## Semantic Adequacy Evidence

- Shallow-pass trap rejected: screenshots are paired with CSS fetch assertions in `bundle://proof/SB02/transcripts/sb02-browser-validation-transcript.txt`.
- Adversarial negative proof: CSS responses were checked for `200`, `text/css`, non-empty length, and not-HTML.
- Semantic positive proof: `bundle://proof/SB02/transcripts/sb02-browser-validation-transcript.txt`
- Anti-stub audit: `bundle://proof/SB02/transcripts/sb02-anti-stub-transcript.txt`
- Raw-note literal closure: N004 and N005 are supported by live CSS assertions and before/after Playwright screenshots.
- Red-team verifier artifact: `bundle://proof/final-red-team.md`

## Production Behavior Artifact Matrix

No new production signal, state, record, or event was introduced in SB02; this subbundle validates runtime asset serving and visual behavior.
