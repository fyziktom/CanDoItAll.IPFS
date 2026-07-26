# Container Operations

## Scope

`compose.yaml` is the canonical local-development model for the embedded IPFS node and
NodeControl application. It is not a production deployment contract. This repository
does not currently own a production Compose overlay, ingress model, external secret
provider, or production volume lifecycle.

Run commands from the repository root with Docker Compose v2.

## Local Configuration

Create an ignored local environment file:

```powershell
Copy-Item .env.example .env
```

Replace the `IPFS_PASS` placeholder in `.env`. Do not commit `.env`, node passphrases,
access keys, local certificates, or exported node data.

The model publishes only to the configured loopback address:

| Service | Default host endpoint | Container endpoint |
|---|---|---|
| NodeControl | `http://127.0.0.1:5093` | `http://node-control:8080` |
| IPFS API and gateway | `http://127.0.0.1:5001` | `http://ipfs-node:5001` |

Override ports or resource guardrails in the ignored `.env` file. Keep
`CDA_BIND_ADDRESS=127.0.0.1` unless a reviewed requirement, authentication model, and
firewall rule justify broader exposure.

## Validate And Start

Resolve and inspect the model before starting containers:

```powershell
docker compose --env-file .env config --quiet
.\tools\validation\Test-Docker.ps1 -EnvFile .env -RunBuildChecks
docker compose --env-file .env up -d --build --wait --wait-timeout 120
docker compose --env-file .env ps --all
```

The NodeControl service waits for the IPFS node healthcheck. Applications must still
handle later dependency loss and reconnect with bounded backoff.

## Stop And Reset

Normal teardown preserves named volumes:

```powershell
docker compose --env-file .env down
```

Destructive reset is a separate, explicit operation:

```powershell
.\tools\dev\Reset-Containers.ps1 `
    -ProjectName candoitall-ipfs `
    -EnvFile .env `
    -Confirm
```

The reset removes both project-scoped volumes. Back up authoritative data first. Never
use the reset operation against a production or externally managed project.

## Storage

| Volume key | Data class | Owner | Container path |
|---|---|---|---|
| `ipfs-node-data` | Authoritative durable | Embedded engine operator | `/data/ipfs` |
| `node-control-data` | Authoritative operational | NodeControl operator | `/data/node-control` |

Normal container restart, rebuild, and `docker compose down` preserve both volumes.
Follow [backup and restore](backup-and-restore.md) before destructive reset, engine
repository migration, or application upgrade.

## Runtime Guardrails

The development model:

- runs the application processes as the non-root user configured by the Dockerfiles;
- drops Linux capabilities and enables `no-new-privileges`;
- uses bounded local logging;
- applies configurable memory, CPU, and PID limits;
- gives both services a measured starting point for graceful shutdown;
- uses mutable `:dev` image tags only for local development.

The application filesystems are not yet mounted read-only. The current images install
runtime tooling and the .NET hosts have not completed a writable-path inventory across
all supported Docker engines. This is an owned local-development exception; production
containerization must inventory writable paths, add explicit volumes or `tmpfs`, enable
read-only roots, and smoke-test shutdown and recovery.

The service Dockerfiles remain under `docker/` because both builds use the repository
root as a multi-project .NET build context and share repository-level restore inputs.
They are local application images, not independently published container products. If
the images become public release artifacts, move each Dockerfile beside its owning
service or document the continuing exception, and add complete OCI source, revision,
version, creation, vendor, license, and documentation labels.

Run these images through Compose so the Dockerfile `VOLUME` declarations resolve to the
named volumes in `compose.yaml`. Standalone `docker run` without explicit named mounts
can create anonymous durable volumes and is not a supported data lifecycle.

## Configuration Exceptions

The current engine accepts its repository passphrase through `IPFS_PASS` and does not yet
support an `IPFS_PASS_FILE` contract. The local model therefore supplies the value from
an ignored `.env` file or current shell. Environment variables are not a production
secret store. A production deployment must add and validate a file-based or
platform-native secret contract before it can be supported.

Both services use the repository-owned `Container` ASP.NET Core environment because the
application currently stores container path defaults in `appsettings.Container.json`.
This is a local application profile, not a production identity.

## Logs And Troubleshooting

```powershell
docker compose --env-file .env logs --tail 200 ipfs-node
docker compose --env-file .env logs --tail 200 node-control
docker compose --env-file .env ps --all
```

Application logs also live under the `node-control-data` volume. Do not publish logs
that may contain node addresses, content identifiers, access decisions, or private
repository paths without review.

The Compose filename changed from `docker-compose.yml` to `compose.yaml`. External
orchestration must pass or discover the canonical path explicitly; compatibility changes
in sibling orchestration repositories are outside this repository's ownership.
