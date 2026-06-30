# SB03 Proof Manifest

## Scope

- Extracted a UI-independent `CanDoItAll.IPFS.NodeControl.Abstractions` project.
- Moved NodeControl DTO/model contracts and existing persistence/connection interfaces into that project.
- Added `INodeOperator` as the UI-neutral node workflow facade and kept browser-only upload on the concrete `NodeOperatorService`.
- Registered `NodeOperatorService` as both concrete type and `INodeOperator`.
- Added focused tests that protect the dependency direction and DI composition.
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.json`.

## Semantic Adequacy Proof

- Failing-first transcript: `bundle://proof/SB03/transcripts/failing-first-boundary-missing.txt`.
- Passing transcript: `bundle://proof/SB03/transcripts/focused-nodecontrol-layering-tests.txt`.
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/abstractions-forbidden-dependency-scan.txt`.

## Proof Transcripts

| Evidence | Result |
| --- | --- |
| `bundle://proof/SB03/transcripts/failing-first-boundary-missing.txt` | Negative proof: before SB03 there was no extracted abstractions project and no `INodeOperator` contract. |
| `bundle://proof/SB03/transcripts/restore-after-abstractions.txt` | `dotnet restore CanDoItAll.IPFS.slnx` passed after adding the project. |
| `bundle://proof/SB03/transcripts/build-after-abstractions.txt` | `dotnet build CanDoItAll.IPFS.slnx --no-restore` passed with 32 pre-existing warning-style findings and 0 errors. |
| `bundle://proof/SB03/transcripts/focused-nodecontrol-layering-tests.txt` | `dotnet test ... --filter "FullyQualifiedName~NodeControlCompositionTests|FullyQualifiedName~NodeControlLayeringTests"` passed 3 tests. |
| `bundle://proof/SB03/transcripts/project-reference-graph.txt` | Abstractions references Client only; NodeControl references Abstractions, Client, and Engine. |
| `bundle://proof/SB03/transcripts/abstractions-forbidden-dependency-scan.txt` | No forbidden Blazor/Web/Desktop/component source dependencies found in the abstractions project. |

## Changed File Hashes

| File | SHA-256 |
| --- | --- |
| repo://CanDoItAll.IPFS.slnx | 71c435425bcfe792f509c41c41bb1119c1ea88f8ad3a2054c263019023909441 |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/CanDoItAll.IPFS.NodeControl.Abstractions.csproj | 6e12529a040613fb020cace3728fb696d8407a3b3a92a19bb61deb65a97e583e |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions/IApplicationLogStore.cs | 6034dc9fa725b57a9308da50b673f47c4bf439a40b63652223c403bd7c7be497 |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions/IExplorerIndexStore.cs | f12835b4cbe86337b1f453c723d97d32548f15aedd047b9c7fbc49fae94865c2 |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions/INodeConnectionDriver.cs | 0631a54a0992343739f5169405b28bf1c6c6a3204041f3b948f92fb641a87c0d |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions/INodeConnectionLeaseFactory.cs | 8bcc9384e2a8517e17f364d452647f5bb1c192efd51baa0cbef7d85bf37353ae |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions/INodeHostController.cs | c434a15ec2041a35e669b27a6e0b32234c31ae3bfff67c0d197a37f8103446f6 |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions/INodeOperator.cs | 77e239f81b2b94472fefe78f01ad440ffb95d85c28e6d4dbd61e5304f64d6a24 |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions/IpfsClientLease.cs | 9ece4fd521a926e87d967a882e8130dd765e9f42dc4f819b72d5f1613e107fa8 |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions/IRemotePinRequestStore.cs | 0efbdb17ed7c7cda226e6387140f3500bd8671a921060ce2ba4bd9766795022d |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions/IServerNodeSettingsStore.cs | e5d7e009875afeb7ea3bd66830a499f2e1dfc6da6d62366e69b45edcdcf97e29 |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Models/ApplicationLogModels.cs | e691c884d0a97930ed23ed99052ad039d9bef25eb83b9574ca962b660ec45a01 |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Models/ExplorerIndexModels.cs | a8fa95656feaf85925379a0c24865700979c0fa14e81903b50f819b38f1f6da8 |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Models/KnownRemotePinTarget.cs | 3eb197d0088546e175c2029812ea4c8d035d9630356ad1f9753f66c2b1cf4ef6 |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Models/NodeConnectionRequestCategory.cs | 74c05cf8685a0f329163b7b19bf19f17172ae46903d0ee7c6874f2d083fb872a |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Models/NodeConnectionSettings.cs | 0531f0642119c39dbb5912a682b6760b40311fc71a6644d95073603217bf0e22 |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Models/NodeOperatorModels.cs | 8a4b197d2b080b9ec36815b63540a71790397a442a179a909fb19b7c5bc1d874 |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Models/NodeSummarySnapshot.cs | 3d7141155350e53c357e80be2ed157763786359f8533b4c262c7571e34d1a93b |
| repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Models/RemotePinRequestModels.cs | ef131d8849874f9a148070f9ba2c7659f5fbe4f40fca568033551245e296b9d1 |
| repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj | 04e46c1e3f93932cd2274935e54f1110f128a0c56138fbdd5df900dbfb96ed5b |
| repo://src/CanDoItAll.IPFS.NodeControl/Components/_Imports.razor | 5cc652f699ef408a4b33d7354759b241e480c7cb1ddba56ddbf96849c3147d85 |
| repo://src/CanDoItAll.IPFS.NodeControl/Composition/NodeControlServiceCollectionExtensions.cs | 4340bb5faa36adac9d5b575b82caa8b5af5dc0e3675aba0f31e21e18f9fffd94 |
| repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeOperatorService.cs | e25ff932db45accf75b1bc6fbc2fcdced8be785b03e3ba1ef19acfab49631be6 |
| repo://tests/CanDoItAll.IPFS.Tests/CanDoItAll.IPFS.Tests.csproj | 1fccf6b5cde21d7e8c01527f68aa22d0af5f7fb060a24f9e0bf341aa1ab50168 |
| repo://tests/CanDoItAll.IPFS.Tests/NodeControl/NodeControlCompositionTests.cs | b22f21fa4676727415af52697243d3db3f71dec28d9c2759b3d2a64138c0cd0b |
| repo://tests/CanDoItAll.IPFS.Tests/NodeControl/NodeControlLayeringTests.cs | b9e3365424485010f55af6570304539ab6dd4c730118a126269a1b927de35b3a |

## Deleted Or Moved Source Paths

- Existing files from the former NodeControl Models folder moved to `repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Models` with their public namespaces preserved.
- Existing files from the former NodeControl Abstractions folder moved to `repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions`.
- The former NodeControl service lease file moved to `repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions/IpfsClientLease.cs` and its namespace changed to the abstractions namespace.

## Residual Work

- SB05 must split `NodeOperatorService` into narrower file/content/network/repository workflow services behind `INodeOperator`.
- SB06 must update pages to consume decomposed services where appropriate.
- The browser-upload method still belongs to the concrete UI-facing service because `IBrowserFile` is intentionally excluded from the CLI-safe contract.
