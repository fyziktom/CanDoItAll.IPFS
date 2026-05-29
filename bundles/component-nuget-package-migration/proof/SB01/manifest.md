# SB01 Proof Manifest

## Subbundle

- Subbundle ID: `SB01`
- Status: `Completed`
- Owned requirements: `R1`, `R2`, `R3`, `R4`
- Source raw notes: `N001`, `N002`, `N003`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Changed Files

| Path | SHA-256 |
| --- | --- |
| `repo://NuGet.config` | `55DB7974246108F4C53121F44B488AFDD0EBC4230BF9315242D767AA187AD931` |
| `repo://Directory.Build.props` | `F8543151FC6349CEDC652028259FBA38C32D0CC4653B0ECBC9A4876EC2321718` |
| `repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj` | `0558472FA337AE8F077474CC24A86D99B42FA249493737D52E0C2AD3283E8FA7` |

## Transcripts

- Command transcript: `bundle://proof/SB01/transcripts/sb01-passing-transcript.txt`
- Failing-first transcript: `bundle://proof/SB01/transcripts/sb01-failing-first-transcript.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/sb01-passing-transcript.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/sb01-anti-stub-transcript.txt`

## Supporting Artifacts

- Source assertion: `bundle://proof/SB01/commands/pre-source-audit.txt`
- Source assertion: `bundle://proof/SB01/commands/post-source-audit.txt`
- Restore proof: `bundle://proof/SB01/commands/dotnet-restore.txt`
- Package asset proof: `bundle://proof/SB01/commands/package-assets-audit.txt`
- Build proof: `bundle://proof/SB01/commands/dotnet-build-no-restore.txt`
- Changed-file hashes: `bundle://proof/SB01/commands/changed-file-hashes-full.txt`

## Semantic Adequacy Evidence

- Shallow-pass trap rejected: `bundle://proof/SB01/transcripts/sb01-failing-first-transcript.txt` records that the pre-edit old source reference violates `SB01-I2`.
- Adversarial negative proof: `bundle://proof/SB01/transcripts/sb01-failing-first-transcript.txt`
- Semantic positive proof: `bundle://proof/SB01/transcripts/sb01-passing-transcript.txt`
- Anti-stub audit: `bundle://proof/SB01/transcripts/sb01-anti-stub-transcript.txt`
- Raw-note literal closure: N002 and N003 are closed by package feed, package reference, and no-old-project-reference proof.

## Production Behavior Artifact Matrix

No new production signal, state, record, or event was introduced in SB01; this migration changes package resolution and project references only.
