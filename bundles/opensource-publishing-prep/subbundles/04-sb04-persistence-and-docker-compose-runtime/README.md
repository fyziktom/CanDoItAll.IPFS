# SB04 Persistence And Docker Compose Runtime

## Status

- `Completed`

## Objective

- Add a root docker compose runtime that starts the node/control runtime together and preserves database/index data and file data after container restart and image rebuild.
- Make persistence paths explicit and document the operational model.

## Covered Inputs

- R002 publishing preparation.
- R009 docker compose with persisted database and files.
- R008 raw storage reality because current persistence uses SQLite/JSON/log files.

## Prerequisites

- SB01 baseline is complete.
- SB03 boundary decisions are reviewed if docker composition needs project-level changes.

## Exact Source References

- repo://README.md
- repo://src/CanDoItAll.IPFS.NodeControl/appsettings.json
- repo://src/CanDoItAll.IPFS.NodeControl/Program.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Services/ExplorerIndexStore.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Services/ApplicationLogStore.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Services/RemotePinRequestStore.cs
- repo://src/CanDoItAll.IPFS.NodeControl/Services/ServerNodeSettingsStore.cs
- repo://src/CanDoItAll.IPFS.Engine/HttpApiHost/Program.cs
- bundle://architecture/01-target-solution.md
- bundle://inventories/publishing-prep-checklists.xlsx

## Deliverables

- Root-level docker compose file and any required Dockerfiles/configuration.
- Named volumes or bind mounts for IPFS repo data, Explorer SQLite index, settings JSON, remote pin request JSON, logs, and any uploaded/generated file data.
- Runtime configuration documentation for `IPFS_PASS`, API URL, exposed ports, and durable paths.
- Persistence proof after `up`, data mutation, restart, rebuild, and re-check.

## Dependency Impact

- SB08 depends on concrete persistence paths and storage behavior.
- SB09 depends on docker proof and user-facing docker documentation.
- Publishing cannot close if docker destroys node data or app database/index data after rebuild.

## Validation Depth

- Release-critical host/runtime validation.

## Implementation Steps

1. Decide whether compose starts Engine API only, NodeControl UI, or both, and document the topology.
2. Add root docker compose and any minimal Dockerfiles/config files.
3. Configure durable volumes for all app/node data paths.
4. Run compose, create observable node/app data, restart containers, and verify the data remains.
5. Rebuild images, run compose again, and verify the same data remains.
6. Update README docker instructions only after proof exists.
7. Update workbook, execution report, and proof manifest.

## Do Not Do

- Do not change persistence technology without an explicit architecture update.
- Do not store secrets in compose files.
- Do not accept a proof that only checks container startup.
- Do not tune UI responsiveness in this subbundle.

## Acceptance Checklist

- Docker compose exists at the repository root after implementation.
- All required data surfaces are mapped to durable volumes.
- Data survives container restart.
- Data survives image rebuild.
- README explains how to run and where data is stored.
- Failure modes and required environment variables are documented.

## Proof Required

- `docker compose up` transcript.
- Command or UI/API transcript that writes durable node/app data.
- `docker compose restart` transcript and data verification.
- `docker compose build` or rebuild transcript and data verification.
- `bundle://proof/SB04/manifest.md` with changed-file hashes and portable references.

## Closure Evidence

- Root compose runtime: `repo://docker-compose.yml`.
- Container-specific config: `repo://src/CanDoItAll.IPFS.NodeControl/appsettings.Container.json`.
- Build proof: `bundle://proof/SB04/transcripts/docker-compose-build-after-restore-fixes.txt`.
- Startup proof: `bundle://proof/SB04/transcripts/docker-compose-up.txt` and `bundle://proof/SB04/transcripts/docker-compose-ps-after-up.txt`.
- Durable mutation proof: `bundle://proof/SB04/transcripts/write-durable-data.txt`.
- Restart persistence proof: `bundle://proof/SB04/transcripts/docker-compose-restart-and-verify.txt`.
- Rebuild persistence proof: `bundle://proof/SB04/transcripts/docker-compose-rebuild-no-cache.txt` and `bundle://proof/SB04/transcripts/docker-compose-rebuild-and-verify.txt`.
- Host-visible UI proof: `bundle://proof/SB04/nodecontrol-dashboard-compose-1920x1080.png`.
- Proof manifest: `bundle://proof/SB04/manifest.md`.

## Browser Validation Logging

- Host-visible validation is required if NodeControl UI runs in compose.
- Route/window: NodeControl app route that shows node status or files.
- Viewport: `1920x1080`.
- Evidence: screenshot after compose startup plus console/network error review.
- If compose runs API-only, record `N/A` for browser and put docker proof in the subbundle gate row.

## Progression Gate

- SB08 and SB09 may proceed only after persisted data is proven across restart and rebuild.

## Suggested Agent Prompt

```text
Implement SB04 only. Add the root docker compose runtime, map durable data volumes, prove data survives restart and rebuild, update README only with proven instructions, and stop if persistence cannot be verified.
```
