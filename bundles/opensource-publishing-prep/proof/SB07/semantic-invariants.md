# SB07 Semantic Invariants

## Performance Invariants

- Production code should not construct a per-call `HttpClient` for network operations covered by existing factories or shared clients.
- Reusable JSON serializer options should be cached when response formatting uses the same options repeatedly.
- Awaitable workflow results should be awaited rather than read through `.Result`.
- HTTP response bodies that may be streamed should use streaming-friendly APIs and preserve cancellation.
- Broad static-scan hits must be treated as triage leads, not mechanical rewrite instructions.

## Behavioral Invariants Proven

- DoH still accepts an injected `HttpClient`; the shared fallback is only used when the caller does not set one.
- Known-node API connection resolution continues to use the NodeControl workflow and named-client policy.
- NodeControl composition resolves workflow aliases after the constructor change.
- Focused network workflow tests continue to pass.
- The full solution build remains green after performance hardening.

## Measured Invariants

- Production targeted scan reports zero production manual `HttpClient` construction.
- Production targeted scan reports zero production ad hoc `JsonSerializerOptions` construction.
- Production targeted scan reports zero `.Result`/`.Wait` candidates in `NodeNetworkWorkflowService`.
- Full scan manual `HttpClient` count is reduced from 4 to 3, with remaining hits in tests.
- Full scan `.Result`/`.Wait` count is reduced from 82 to 81.
