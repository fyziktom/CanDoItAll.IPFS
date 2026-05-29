# Requirement Traceability

| Requirement | Owning Subbundle | Bundle Files | Proof Target | Status |
| --- | --- | --- | --- | --- |
| R1 | SB01, SB02 | `bundle://README.md`, `bundle://reviews/01-execution-report.md` | Prepared/final validator transcripts and closure rows. | Planned |
| R2 | SB01 | `bundle://subbundles/01-01-package-source-and-reference-migration/README.md` | `NuGet.config`, restore transcript, package source assertion. | Planned |
| R3 | SB01 | `bundle://analysis/01-current-state.md`, `bundle://architecture/01-target-solution.md` | `rg` transcript showing no stale external component project reference. | Planned |
| R4 | SB01 | `bundle://requirements/01-normalized-requirements.md` | `CanDoItAll.IPFS.NodeControl.csproj` package reference diff and build transcript. | Planned |
| R5 | SB02 | `bundle://subbundles/02-02-build-static-assets-and-browser-proof/README.md` | HTTP/Playwright proof for `_content/CanDoItAll.Components.BaseLib/css/output.css`. | Planned |
| R6 | SB02 | `bundle://reviews/01-execution-report.md` | IPFS before/after screenshots and visual review. | Planned |
| R7 | SB02 | `bundle://reviews/01-execution-report.md` | Economy before/after screenshots and visual review. | Planned |

## Raw Note Closure Plan

| Raw note | Requirement IDs | Owning subbundle | Planned proof |
| --- | --- | --- | --- |
| N001 | R1 | SB01, SB02 | Bundle validators and execution report. |
| N002 | R2, R4 | SB01 | Feed config, package references, restore/build. |
| N003 | R3 | SB01 | Source search and project-file diff. |
| N004 | R5, R6 | SB02 | CSS endpoint proof, App.razor/source assertions, IPFS screenshots. |
| N005 | R7 | SB02 | Economy before/after screenshots and review. |
