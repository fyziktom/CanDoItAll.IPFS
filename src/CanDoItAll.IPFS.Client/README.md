# CanDoItAll.IPFS.Client

`CanDoItAll.IPFS.Client` is a typed, asynchronous HTTP client for interacting
with a CanDoItAll IPFS node. It targets .NET 8 and .NET 10 and does not bring
the embedded IPFS engine or ASP.NET runtime into your application.

## Install

```shell
dotnet add package CanDoItAll.IPFS.Client
```

## Connect in sixty seconds

```csharp
using Ipfs.Engine.Client;

using var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://127.0.0.1:5001/"),
    Timeout = TimeSpan.FromSeconds(30)
};

var ipfs = new IpfsNodeClient(httpClient);
var version = await ipfs.Generic.VersionAsync();
var localPeer = await ipfs.Generic.IdAsync();

var added = await ipfs.FileSystem.AddTextAsync("Hello from CanDoItAll IPFS");
var text = await ipfs.FileSystem.ReadAllTextAsync(added.Id.ToString());
```

The base address can instead be supplied through immutable client options:

```csharp
var ipfs = new IpfsNodeClient(
    httpClient,
    new IpfsNodeClientOptions
    {
        BaseAddress = new Uri("https://ipfs.example.com/"),
        ApiPath = "api/v0"
    });
```

`BaseAddress` must be an absolute HTTP or HTTPS address. `ApiPath` must be a
relative path without a query or fragment. Invalid configuration fails when
the client is constructed, before a request is sent.

Applications already using `IHttpClientFactory` can register the client as a
standard typed client:

```csharp
services.AddHttpClient<IpfsNodeClient>(http =>
{
    http.BaseAddress = new Uri("http://127.0.0.1:5001/");
    http.Timeout = TimeSpan.FromSeconds(30);
});
```

Reuse the `HttpClient` and `IpfsNodeClient` instances. The operation clients
are stateless and safe for concurrent requests.

## API groups

The entry point exposes focused clients for:

- node identity, version, name resolution, and shutdown
- bitswap, blocks, repository maintenance, and bootstrap peers
- configuration, DAG, DHT, and DNS operations
- files, keys, names, objects, and pins
- pub/sub, statistics, and swarm operations

The client implements the shared `ICoreApi` contract from
`CanDoItAll.IPFS.Core`. The HTTP surface is intentionally capability-based:
operations unavailable from the current server API throw
`NotSupportedException`. At version 0.42.0 this includes generic ping, remote
bitswap fetch, DHT provide/value-store operations, key import/export, binary
or stream pub/sub publishing, and persistent swarm address filters.

## Cancellation, errors, and streams

Pass a `CancellationToken` to any operation that accepts one. Caller
cancellation propagates as `OperationCanceledException`. An HTTP timeout is
reported as `IpfsTransportException`.

- `IpfsApiException` represents a non-success status returned by the node and
  includes the status, route, optional server code, details, and raw error body.
- `IpfsTransportException` represents connectivity and timeout failures.
- `IpfsSerializationException` represents an invalid or incomplete JSON
  response.

Transport exception messages identify the API route but intentionally omit request
query strings, which can contain configuration values or other caller data. Server
error bodies are preserved for diagnostics and can still contain sensitive details.

Methods returning a `Stream` transfer ownership to the caller. Dispose the
stream to release both its HTTP response and connection:

```csharp
await using var content = await ipfs.FileSystem.ReadFileAsync("/ipfs/<cid>");
await content.CopyToAsync(destination, cancellationToken);
```

Input streams passed to upload operations remain owned by the caller.

## Security

Prefer TLS and authenticate at a trusted reverse proxy. Do not expose the node
API directly to an untrusted network. Exception raw bodies can contain node or
request details, so avoid logging them without review and redaction.

The client intentionally contains no automatic retries: configure resilience,
authentication, telemetry, and headers through the `HttpClient` handler
pipeline so policy remains under application control.

Documentation and source are available in the
[CanDoItAll.IPFS repository](https://github.com/fyziktom/CanDoItAll.IPFS), and
more CanDoItAll projects are available at
[aicandoitall.com](https://aicandoitall.com).
The project is licensed under the
[CanDoItAll IPFS license](https://github.com/fyziktom/CanDoItAll.IPFS/blob/main/LICENSE).
