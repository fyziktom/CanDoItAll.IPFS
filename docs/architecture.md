# Architecture

## Repository ownership

`CanDoItAll.IPFS` owns the maintained .NET implementation of:

- IPFS protocol value types and Core API contracts;
- an embedded IPFS node engine and HTTP API host;
- a typed HTTP client for the node API;
- the NodeControl desktop-oriented operator application.

It does not own the CanDoItAll component libraries, the public NuGet service, container
orchestration outside this repository, or downstream applications that consume the
packages.

## Project roles

| Project | Role | Allowed dependency direction |
|---|---|---|
| `CanDoItAll.IPFS.Core` | Protocol value types, codecs, immutable data shapes, and Core API contracts shared by clients and engines. | Framework and protocol-support packages only; never the engine, HTTP client, NodeControl, or tests. |
| `CanDoItAll.IPFS.Client` | HTTP adapter that implements the Core API contracts for a remote node. | `Core` plus serialization/HTTP support; never `Engine` or NodeControl. |
| `CanDoItAll.IPFS.Engine` | Embedded node runtime and HTTP API implementation. | `Core` plus engine/runtime infrastructure. |
| `CanDoItAll.IPFS.NodeControl.Abstractions` | UI-neutral operator workflow contracts and projections. | No NodeControl implementation dependency. |
| `CanDoItAll.IPFS.NodeControl` | Composition root, local engine hosting, and Blazor operator UI. | May reference the public projects it composes. |
| `CanDoItAll.IPFS.Client.Tests` | Isolated HTTP client contract and transport tests. | Client and Core only; never Engine, ASP.NET, or NodeControl. |
| `CanDoItAll.IPFS.Tests` | Unit, contract, integration, and UI smoke tests. | May reference production projects for verification. |

The intended product dependency graph is:

```text
NodeControl -------> Client -------> Core
     |                                ^
     +-------------> Engine ----------+
     +-------------> NodeControl.Abstractions
```

No product-project cycle is permitted.

## ADR-001: Extract the IPFS protocol core from the engine

Status: Accepted
Date: 2026-07-26

### Context

Before this change, `CanDoItAll.IPFS.Client` referenced the executable
`CanDoItAll.IPFS.Engine` project because the engine project also compiled the IPFS value
types and `Ipfs.CoreApi` contracts. A consumer installing the HTTP client therefore
received the embedded engine and its runtime dependency graph.

CodeAnalytics snapshot `snap-20260726111403-8eefb44e` confirmed a direct
`Client -> Engine` project reference and 178 analyzed module-dependency edges. The client
transport, endpoint adapters, DTO mapping, and public facade are otherwise independently
usable.

### Decision

Move the existing `net-ipfs-core` responsibility into the independently packable
`CanDoItAll.IPFS.Core` project. Both the engine and client depend inward on that project.
The HTTP client remains an adapter implementing the stable `ICoreApi` contract, while
transport, request construction, wire DTOs, and mapping helpers remain internal to the
client implementation.

The extracted Core sources predate nullable reference types. Core therefore keeps
nullable analysis disabled rather than publishing inaccurate annotations; Client and
all new code remain nullable-enabled. A future Core contract revision may introduce
reviewed nullable annotations as an explicit API-compatibility change.

The `Core` boundary deliberately owns both protocol value types and Core API contracts.
Those contracts use `Cid`, `MultiHash`, `MultiAddress`, `Peer`, and related protocol
types directly, and all of them share the same compatibility lifecycle. Splitting a
second abstractions package would add consumer and versioning overhead without removing
an implementation dependency.

### Rejected options

- Keep the `Client -> Engine` reference: rejected because a remote-client consumer must
  not install an embedded-node runtime.
- Duplicate client-only string DTOs: rejected because it would create two competing
  public IPFS models and break the existing `ICoreApi` contract.
- Link core source files from the engine directory into another project: rejected
  because compilation would be separated while source ownership remained misleading.
- Introduce a builder or service-locator factory: rejected because client construction
  needs only an `HttpClient` and validated options; a builder would not reduce
  responsibility or improve the test seam.

### Acceptance criteria

- `CanDoItAll.IPFS.Client.csproj` has no project or package dependency on
  `CanDoItAll.IPFS.Engine`.
- `CanDoItAll.IPFS.Core` has no reference to Client, Engine, NodeControl, or tests.
- `IpfsNodeClient` implements `ICoreApi`; `IpfsEngineClient` remains an obsolete
  compatibility name.
- Invalid client base addresses and API paths fail before an HTTP request is sent.
- Package output contains Client, Core, and Engine packages with correct dependency
  direction, package README, source metadata, and repository-owned license.
- A refreshed CodeAnalytics dependency analysis reports no product-project cycle.

### Verification

CodeAnalytics snapshot `snap-20260726130239-d63d7bdd` loaded all seven repository
projects after the extraction. It reports `Client -> Core` and `Engine -> Core`, no
`Client -> Engine` reference, and no product-project cycle.

## Client extension policy

Endpoint-specific behavior belongs in a cohesive operation adapter. Wire DTOs, query
construction, multipart construction, response streaming, NDJSON parsing, and mapping
remain implementation details. Public API additions must start in the Core API contract
and be implemented by both the HTTP client and embedded engine, with a contract test that
does not require NodeControl.

Do not add partial files to `IpfsNodeClient` as new endpoints are introduced. The
facade remains a thin composition surface over operation adapters.
