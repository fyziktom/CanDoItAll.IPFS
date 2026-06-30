# SB03 Semantic Invariants

## Invariant 1: Contracts Are UI-Independent

- Claim: `CanDoItAll.IPFS.NodeControl.Abstractions` does not depend on Blazor components, desktop host code, Windows Forms, or `CanDoItAll.Components`.
- Negative proof: `bundle://proof/SB03/transcripts/failing-first-boundary-missing.txt` shows this boundary did not exist before SB03.
- Positive proof: `bundle://proof/SB03/transcripts/project-reference-graph.txt` shows the abstractions project references only `CanDoItAll.IPFS.Client`.
- Source proof: `bundle://proof/SB03/transcripts/abstractions-forbidden-dependency-scan.txt` found no forbidden UI/Desktop/component dependencies.
- Test proof: `NodeControlLayeringTests.AbstractionsAssembly_DoesNotReference_NodeControlWebOrUiAssemblies` passed in `bundle://proof/SB03/transcripts/focused-nodecontrol-layering-tests.txt`.

## Invariant 2: The Existing UI Composition Still Resolves

- Claim: NodeControl can still resolve the concrete service graph after models/contracts move out of the Web project.
- Positive proof: `bundle://proof/SB03/transcripts/build-after-abstractions.txt` passed for the full solution.
- Test proof: `NodeControlCompositionTests.AddIpfsNodeControlApplication_Resolves_Compatibility_Service_Graph_And_Interface_Aliases` passed and asserts `NodeOperatorService` and `INodeOperator` resolve to the same scoped instance.

## Invariant 3: Future CLI Work Has A Real Node Workflow Contract

- Claim: reusable callers can compile against `INodeOperator`, NodeControl DTOs, persistence contracts, and connection contracts without referencing the Blazor/Web project.
- Negative proof: `bundle://proof/SB03/transcripts/failing-first-boundary-missing.txt` shows there was no `INodeOperator` before SB03.
- Positive proof: `repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions/INodeOperator.cs` exposes file, pin, content, network, config, and repository operations using UI-neutral types.
- Boundary note: `UploadBrowserFileAsync(IBrowserFile, ...)` remains outside `INodeOperator` by design because it is a browser-specific UI entry point.

## Invariant 4: SB05 Can Decompose Services Without Reopening Project Boundaries

- Claim: downstream decomposition can split concrete workflows inside or behind the existing NodeControl layer without re-moving DTOs/interfaces.
- Positive proof: model and persistence contract namespaces remain stable while physical files moved to the abstractions project.
- Positive proof: `NodeOperatorService` implements `INodeOperator`, so narrower SB05 services can be introduced behind the same public contract.
