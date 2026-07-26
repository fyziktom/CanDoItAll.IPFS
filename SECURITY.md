# Security Policy

## Supported Versions

Security fixes are provided for the latest published package and application release.
Older releases are unsupported unless the maintainer announces a specific exception.

| Version | Supported |
|---|---|
| Latest published release | Yes |
| Older releases | No |
| Unreleased `main` branch | Best effort |

## Reporting A Vulnerability

Report vulnerabilities through the repository's
[private GitHub security advisory form](https://github.com/fyziktom/CanDoItAll.IPFS/security/advisories/new).
Do not publish exploit details, credentials, private data, node identities, repository
contents, or sensitive proof in a public issue.

If the private advisory form is unavailable, contact the `fyziktom` account on LinkedIn
and request a private reporting channel without including vulnerability details in the
initial message.

Include the affected package or application, version or commit, reproduction steps,
expected impact, and any safe mitigation already tested.

## Scope

The policy covers:

- `CanDoItAll.IPFS.Client`, `CanDoItAll.IPFS.Core`, and
  `CanDoItAll.IPFS.Engine`;
- the NodeControl application and its HTTP endpoints;
- repository-owned release archives and container images.

Third-party services, the public IPFS network, and vulnerabilities in upstream
dependencies are outside direct repository ownership, but reports that demonstrate an
impact on this software are welcome through the same private channel.
