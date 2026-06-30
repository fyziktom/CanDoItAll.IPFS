# SB05 Semantic Invariants

## Architecture Invariants

- Workflow interfaces must live in `CanDoItAll.IPFS.NodeControl.Abstractions`.
- Workflow interfaces must not reference Blazor UI types such as `IBrowserFile`.
- `NodeOperatorService` must remain a facade and must not directly depend on `IpfsClientFactory` or `IExplorerIndexStore`.
- Browser-file upload may remain on the concrete facade/file workflow because it is UI-bound and intentionally outside `INodeOperator`.
- Pages may continue using `NodeOperatorService` until SB06 migrates them to narrower workflow dependencies.

## Behavioral Invariants Proven

- File upload, preview, pin, unpin, and explorer cache behavior are preserved by existing NodeOperator workflow tests.
- Content workflows for block, object, DAG, IPNS, and keys are preserved by existing NodeOperator content tests.
- Network workflows for swarm, bootstrap, address filters, DHT, PubSub, and cross-node share/fetch are preserved by existing NodeOperator network tests.
- Maintenance workflows for config and repository operations are preserved by existing NodeOperator maintenance tests.
- Home, Files, Content, Network, and Settings components render through the decomposed service graph.

## Measured Invariants

- `NodeOperatorService.cs` line count is 134 after the split.
- Workflow services are registered in DI with same-scope aliases for their UI-neutral interfaces.
- `INodeOperator` still resolves to the same scoped `NodeOperatorService` compatibility facade.
