# SB04 Semantic Invariants

## Runtime Invariants

- Compose must require `IPFS_PASS` from the caller environment.
- The Engine container must persist `IPFS_PATH=/data/ipfs` through the `ipfs-node-data` named volume.
- The NodeControl container must use the compose node endpoint `http://ipfs-node:5001/`.
- NodeControl persistence paths must resolve under `/data/node-control`, not transient container user-profile storage.
- NodeControl and Engine containers must expose health checks that can become healthy through `docker compose up --wait`.

## Persistence Invariants Proven

- CID `QmVj1xyP5jyhYsQkjGbj91eNrK1Tfg2zjtuJd3FiFJtpK5` is present in the node pin list after initial write, after restart, and after rebuild/recreate.
- Peer ID `QmSTEVhYuLAc6SVjuxdgndLnFTGmVRrGjy7NPqPUHba5FK` remains stable across restart and rebuild/recreate, proving the IPFS repo volume was reused.
- Remote pin request `sb04-46adc2a7a15b4d19accdbed51b7b01d7` remains stored in `/data/node-control/remote-pin/remote-pin-requests.json` after restart and rebuild/recreate.
- NodeControl readiness reports the configured persistence paths and `remotePinRequestCount` equals `1` after the durable request is written.

## Documentation Invariants

- Root README must tell users that `docker compose down` preserves volumes and `docker compose down --volumes` deletes persisted data.
- Root README must document the temporary local-package requirement for `CanDoItAll.Components.*` until a public/shared feed exists.
- Bundle execution report must point SB04 downstream consumers to the restart and rebuild transcripts.
