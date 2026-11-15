# Roadmap

What NCode.Buffers intends to do over the next year. [Open issues](https://github.com/NCodeGroup/Buffers/issues) hold
the detail and the current state; this page is the direction. It is reviewed at least once a year and when a release
changes it.

## Scope

NCode.Buffers stays a small library: pinned, pooled buffers that zero themselves for secrets, fixed-size buffer writers,
sequence helpers and string splitting without substring copies. Features outside that, such as encryption, key storage
or serialization, belong in other packages.

## Next Twelve Months

- **Supported .NET versions.** Target each supported LTS .NET and drop one when it leaves support
  ([ADR-1004](docs/decisions/1004-the-package-targets-only-supported-lts-dotnet-versions.md)); the changelog records each
  change.
- **Compatibility.** Keep the public API compatible within a major version, checked on every pack against the last
  release ([ADR-1001](docs/decisions/1001-each-package-is-validated-against-the-last-released-package.md)).
- **Supply chain.** Signed provenance and an SBOM on every release, and an OpenSSF Scorecard score raised where the
  repository controls it; an approving review gate on every pull request
  ([#61](https://github.com/NCodeGroup/Buffers/issues/61)).
- **OpenSSF Best Practices.** Reach the silver level for project
  [16727](https://www.bestpractices.dev/projects/16727), which needs a second owner for continuity
  ([GOVERNANCE.md](GOVERNANCE.md#continuity)).
- **Quality.** Keep line and branch coverage at 98% or more, and property tests on the buffer types.

## Not Planned

- Locking memory into RAM (`mlock`, `VirtualLock`) or hiding it from a debugger or crash dump: README's
  [Security notes](README.md#security-notes) explain what the library does and doesn't protect.
- Support for .NET versions out of Microsoft's support.
