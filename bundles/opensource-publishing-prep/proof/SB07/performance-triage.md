# SB07 Performance Triage

## Scan Checklist

The `analyzing-dotnet-performance` recipes were run across source and tests, excluding `bin`, `obj`, and bundle artifacts.

| Category | Before | After | Decision |
| --- | ---: | ---: | --- |
| `IndexOf` string literal without `StringComparison` | 0 | 0 | No-op. |
| `Substring` allocations | 19 | 19 | Deferred: mostly parser/protocol/test paths; no safe hot-path rewrite without targeted benchmarks. |
| `StartsWith`/`EndsWith` string literal without `StringComparison` | 0 | 0 | No-op. |
| `Contains` string literal without `StringComparison` | 0 | 0 | No-op. |
| `async void` | 18 | 18 | Deferred: UI/event-handler audit remains broader than SB07's selected fixes. |
| `.Result`/`.Wait` candidates | 82 | 81 | Fixed the NodeControl workflow candidate; remaining hits are tests and inherited DNS/MDNS sync entry points. |
| Manual `HttpClient` construction | 4 | 3 | Fixed production DoH/NodeControl candidates; remaining hits are test harness clients. |
| Ad hoc `JsonSerializerOptions` construction | 1 | 0 | Fixed by caching health-check response options. |
| `ToLower`/`ToUpper` without culture | 0 | 0 | No-op. |
| Chained `.Replace` x3 | 0 | 0 | No-op. |
| `params` signatures | 11 | 11 | Deferred: public API shape, no hot-path evidence. |
| Char LINQ `All`/`Any` | 0 | 0 | No-op. |
| Static readonly `Dictionary` | 2 | 2 | Deferred: not on selected release-risk paths. |
| Static readonly `FrozenDictionary` | 0 | 0 | Deferred until dictionary use is profiled. |
| `new List` | 117 | 117 | Deferred: broad allocation signal, not a safe mechanical rewrite. |
| `new Dictionary` | 60 | 60 | Deferred: broad allocation signal, not a safe mechanical rewrite. |
| `StringComparer.CurrentCulture` | 0 | 0 | No-op. |
| LINQ chain candidates | 345 | 345 | Deferred: broad signal, no hot-path proof for blanket rewrites. |
| Regex compiled/generated/new | 0/0/0 | 0/0/0 | No-op. |
| Class declarations / sealed classes | 353 / 209 | 353 / 209 | Deferred: sealing sweep is too broad for SB07 without inheritance audit. |

## Fixed

- `DohClient` now uses a shared fallback `HttpClient` with `SocketsHttpHandler.PooledConnectionLifetime`, while still allowing callers to inject their own client.
- `DohClient.QueryAsync` now disposes linked timeout tokens and HTTP responses, sends requests with `HttpCompletionOption.ResponseHeadersRead`, and passes cancellation through stream reading.
- `NodeNetworkWorkflowService.GetNetworkSnapshotAsync` no longer reads completed task results through `.Result`.
- `NodeNetworkWorkflowService.ConnectByKnownNodeApiAsync` uses `IHttpClientFactory` with the existing NodeControl named-client policy instead of constructing a per-call `HttpClient`.
- `NodeControlHealthCheckResponseWriter` now reuses a cached `JsonSerializerOptions` instance.

## Deferred With Rationale

- DNS `NameServer` `.Result` calls wrap `FindAnswerAsync`, which currently completes synchronously through `Task.FromResult`. Rewriting that inherited resolver shape requires targeted DNS behavior tests and is deferred.
- `ServiceDiscovery` sync waits are in synchronous event/announcement APIs. Converting them to async changes API and event semantics, so they are deferred.
- Test-suite `.Result`/`.Wait` usage is still noisy but not production runtime risk for this publishing pass.
- LINQ, `List`, `Dictionary`, `Substring`, `params`, and sealing findings remain scan leads. They should be optimized only with profiling or a focused hot-path story.

## Proof

- Build: `bundle://proof/SB07/transcripts/build-after-sb07-performance-fixes.txt`
- Focused tests: `bundle://proof/SB07/transcripts/focused-performance-tests.txt` passed 16 tests.
- Production targeted scan: `bundle://proof/SB07/transcripts/production-targeted-scan-after-sb07.txt` shows zero production manual `HttpClient`, zero production ad hoc JSON options, and zero `.Result`/`.Wait` candidates in `NodeNetworkWorkflowService`.
