# Third-Party Notices

## Makaretu .NET IPFS foundation

- Upstream: [Richard Schneider's .NET IPFS implementation](https://github.com/richardschneider/net-ipfs)
- Use: portions of the IPFS implementation originated in that upstream foundation and
  are maintained here.
- License: MIT
- Copyright: Copyright (c) 2018 Richard Schneider
- License text: the repository [MIT License](LICENSE), which retains the upstream
  copyright line.

## Microsoft Roslyn ConcurrentSet

- Upstream:
  [Roslyn ConcurrentSet](https://github.com/dotnet/roslyn/blob/master/src/Compilers/Core/Portable/InternalUtilities/ConcurrentSet.cs)
- Use: copied into
  `src/CanDoItAll.IPFS.Engine/Base/net-dns/Resolving/ConcurrentSet.cs`; the source header
  records that it was taken on 18 July 2018.
- License: Apache-2.0
- Copyright: Copyright (c) Microsoft
- License text: `Apache-2.0.txt`

The test suite also contains copied Common.Logging console adapter sources under the same
Apache-2.0 license; their source headers and copyright notices are retained.
