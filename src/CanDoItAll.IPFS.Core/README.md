# CanDoItAll.IPFS.Core

`CanDoItAll.IPFS.Core` contains the protocol types shared by CanDoItAll IPFS
implementations and targets .NET 10:

- CID, multihash, multiaddress, DAG, peer, and key value types
- multibase, multicodec, and hashing support
- `ICoreApi` and the operation contracts implemented by the HTTP client and
  embedded engine

Most applications should install
[`CanDoItAll.IPFS.Client`](https://www.nuget.org/packages/CanDoItAll.IPFS.Client)
to communicate with a node over HTTP, or
[`CanDoItAll.IPFS.Engine`](https://www.nuget.org/packages/CanDoItAll.IPFS.Engine)
to host an embedded node. Install this package directly when implementing an
adapter or sharing IPFS protocol values without an engine dependency.

```shell
dotnet add package CanDoItAll.IPFS.Core
```

This package intentionally contains no HTTP transport, node runtime, ASP.NET
host, or user interface.

Documentation and source are available in the
[CanDoItAll.IPFS repository](https://github.com/fyziktom/CanDoItAll.IPFS), and
more CanDoItAll projects are available at
[aicandoitall.com](https://aicandoitall.com).
The project is licensed under the
[CanDoItAll IPFS license](https://github.com/fyziktom/CanDoItAll.IPFS/blob/main/LICENSE).

## Acknowledgements

This package is developed and maintained by
[`fyziktom`](https://github.com/fyziktom). It builds on Richard Schneider's original
[`net-ipfs-core`](https://github.com/richardschneider/net-ipfs-core). Many thanks to
Richard Schneider and the upstream contributors for providing the foundation that this
substantially extended CanDoItAll implementation continues from.
