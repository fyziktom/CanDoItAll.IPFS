# SB02 Open Source Metadata And Dependency Hardening

## Status

- `Completed`

## Objective

- Prepare the repository metadata, package configuration, dependency posture, and public documentation for open-source publication.
- Resolve or explicitly document vulnerability advisories and inherited metadata before publishing.

## Covered Inputs

- R002 open-source publishing readiness.
- R003 messy publishing metadata.
- R011 final baseline/dependency validation.

## Prerequisites

- SB01 refreshed baseline is complete.
- Current package/advisory warnings are recorded in the execution report.

## Exact Source References

- repo://README.md
- repo://LICENSE
- repo://src/CanDoItAll.IPFS.Engine/CanDoItAll.IPFS.Engine.csproj
- repo://src/CanDoItAll.IPFS.Client/CanDoItAll.IPFS.Client.csproj
- repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj
- repo://Directory.Build.props
- bundle://requirements/01-normalized-requirements.md
- bundle://inventories/publishing-prep-checklists.xlsx

## Deliverables

- Correct repository/package URLs, package icon metadata, license metadata, project descriptions, authorship/copyright posture, and release notes strategy.
- README sections for getting started, configuration, docker usage after SB04, testing, security advisories, contribution workflow, and publication caveats.
- Dependency advisory plan and implemented package updates where safe.

## Dependency Impact

- SB09 cannot close a release until package metadata and dependency advisories have a documented decision.
- SB04 docker docs and SB09 final README proof depend on the public docs shape created here.

## Validation Depth

- Open-source release readiness and package/dependency proof.

## Implementation Steps

1. Compare current project metadata against intended open-source identity.
2. Replace stale upstream URLs or explicitly document inherited upstream lineage where required.
3. Replace deprecated `PackageIconUrl` with supported package icon packaging when applicable.
4. Update vulnerable dependencies where compatible; otherwise document risk and mitigation.
5. Update README/LICENSE/package docs without claiming SB04 docker proof before it exists.
6. Run build and package validation commands for touched projects.
7. Update workbook rows and the execution report.

## Do Not Do

- Do not rewrite license history without verifying intended ownership.
- Do not introduce docker instructions that have not been proven by SB04.
- Do not perform NodeControl layering or UI refactors in this subbundle.

## Acceptance Checklist

- Stale package URLs and metadata are corrected or intentionally documented.
- Vulnerability advisories are resolved or tracked with explicit risk acceptance.
- README is publishable and does not depend on local sibling paths.
- Package validation commands run successfully or document actionable blockers.
- Workbook and execution report are updated.

## Proof Required

- `dotnet build CanDoItAll.IPFS.slnx --no-restore` after metadata/dependency changes.
- Package/advisory command transcript.
- Package validation or pack command transcript for package projects.
- Diff references for README, project files, and license-related decisions.

## Browser Validation Logging

- N/A: this subbundle is documentation/package focused.
- If README links are rendered in a browser manually, record optional screenshot paths in the execution report notes.

## Progression Gate

- SB09 final publication validation may proceed only after metadata and dependency risks are closed or explicitly accepted with proof.

## Suggested Agent Prompt

```text
Implement SB02 only. Refresh package and dependency evidence from SB01, fix open-source metadata/documentation issues, avoid unproven docker claims, and capture package/advisory proof before closing the gate.
```
