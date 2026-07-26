# Backup And Restore

## Data And Recovery Policy

The Compose stack owns two project-scoped named volumes:

| Volume | Contents | Engine or schema | Backup target | RPO | RTO |
|---|---|---|---|---|---|
| `ipfs-node-data` | Node identity, repository configuration, blocks, and pins | Engine release and repository format used by the running image | Daily when used continuously and before every upgrade | 24 hours | 60 minutes |
| `node-control-data` | Node settings, remote-pin requests, explorer SQLite index, and application logs | NodeControl release and its current SQLite/storage formats | Daily when used continuously and before every upgrade | 24 hours | 60 minutes |

The operator owns backup scheduling and restore testing. Retain at least seven daily and
four weekly backups for continuously used nodes. Store backups outside the Docker host,
encrypt them at rest, restrict access to the node operator, and record the application
version and image digest with each backup.

These defaults are for local and small single-host use. A deployment with stricter
recovery requirements must define its own reviewed schedule and managed storage.

## Prepare A Consistent Backup

Quiesce both services before a raw volume backup. Do not archive a live SQLite database
or changing node repository.

```powershell
$projectName = "candoitall-ipfs"
$backupStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupRoot = (
    New-Item -ItemType Directory -Force ".\.artifacts\backups\$backupStamp"
).FullName

docker compose -p $projectName --env-file .env stop
```

Back up the engine volume:

```powershell
docker run --rm `
    --mount "type=volume,source=${projectName}_ipfs-node-data,target=/data,readonly" `
    --mount "type=bind,source=$backupRoot,target=/backup" `
    alpine:3.22 `
    tar -C /data -czf /backup/ipfs-node-data.tgz .
```

Back up the NodeControl volume:

```powershell
docker run --rm `
    --mount "type=volume,source=${projectName}_node-control-data,target=/data,readonly" `
    --mount "type=bind,source=$backupRoot,target=/backup" `
    alpine:3.22 `
    tar -C /data -czf /backup/node-control-data.tgz .
```

Record checksums and the exact application revision:

```powershell
Get-FileHash "$backupRoot\*.tgz" -Algorithm SHA256
git rev-parse HEAD
docker compose -p $projectName --env-file .env images
docker compose -p $projectName --env-file .env start
docker compose -p $projectName --env-file .env ps --all
```

Copy the archives, checksums, commit, and image information to the protected backup
destination. The ignored `.artifacts` copy is staging only.

## Restore Into New Volumes

Never overwrite the only known-good volume. Restore into a new, disposable project and
validate it before deciding whether to cut over.

```powershell
$restoreProject = "candoitall-ipfs-restore"
$backupRoot = (Resolve-Path ".\.artifacts\backups\<backup-stamp>").Path

docker volume create "${restoreProject}_ipfs-node-data"
docker volume create "${restoreProject}_node-control-data"
```

Restore the engine data:

```powershell
docker run --rm `
    --mount "type=volume,source=${restoreProject}_ipfs-node-data,target=/data" `
    --mount "type=bind,source=$backupRoot,target=/backup,readonly" `
    alpine:3.22 `
    tar -C /data -xzf /backup/ipfs-node-data.tgz
```

Restore NodeControl data:

```powershell
docker run --rm `
    --mount "type=volume,source=${restoreProject}_node-control-data,target=/data" `
    --mount "type=bind,source=$backupRoot,target=/backup,readonly" `
    alpine:3.22 `
    tar -C /data -xzf /backup/node-control-data.tgz
```

Start the restored project on different loopback ports:

```powershell
$env:NODE_CONTROL_PORT = "5193"
$env:IPFS_NODE_API_PORT = "5101"
docker compose -p $restoreProject --env-file .env up -d --wait --wait-timeout 120
docker compose -p $restoreProject --env-file .env ps --all
```

Validate:

1. both containers are healthy;
2. the engine reports the expected node identity;
3. expected pinned content resolves and hashes match known values;
4. NodeControl loads the expected node settings and remote-pin requests;
5. the explorer index opens without SQLite integrity errors;
6. restart preserves the restored state.

Run the same application version that produced the backup first. After validation,
upgrade through the repository's supported migration path. Do not reuse a volume after a
failed major-version migration; return to a fresh restore.

Remove only the explicitly disposable restore project after validation:

```powershell
docker compose -p $restoreProject --env-file .env down --volumes --remove-orphans
```

Production or externally managed data must use the deployment platform's backup,
encryption, retention, and restore controls instead of these local commands.
