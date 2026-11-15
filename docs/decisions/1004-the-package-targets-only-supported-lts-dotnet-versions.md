# 1004. The package targets only supported LTS .NET versions

- **Status:** Accepted
- **Date:** 2026-11-14
- **Deciders:** NCode Group

## Context

A target framework that Microsoft no longer supports gets no security fixes, and its dependencies stop testing for it:
`Nerdbank.Streams` 2.14.411 brings `System.IO.Hashing` 11.0.0 into a `net8.0` build, which `System.IO.Hashing` 11.0.0
does not support ([#35](https://github.com/NCodeGroup/Buffers/issues/35)). Microsoft's
[.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core), read 2026-11-14, lists .NET 8
as out of support since 2026-11-10, and .NET 10, a Long Term Support release, as supported until 2028-11-14. Every
target framework is one more build to restore, test and validate.

## Decision

**NCode.Buffers targets only Long Term Support .NET versions that Microsoft supports: `net10.0` alone.**

1. `src/NCode.Buffers/NCode.Buffers.csproj` sets `<TargetFramework>net10.0</TargetFramework>`.
2. Removing a target framework is a breaking change: it ships in the next major version (ADR-1000), and its `PKV006`
   package validation error is recorded in `src/NCode.Buffers/CompatibilitySuppressions.xml` until the validation
   baseline reaches that major version (ADR-1001).
3. A Standard Term Support version, such as .NET 11, is not targeted.

## Options considered

- Keep `net8.0` and pin a `System.IO.Hashing` version that supports it. Rejected: the package keeps shipping for a
  runtime without security fixes, and a transitive dependency is pinned by hand.
- Keep `net8.0` on an older `Nerdbank.Streams`. Rejected: the same, and a dependency is held back.
- Target only supported LTS versions (chosen).

## Consequences

- Applications on .NET 8 stay on NCode.Buffers 4.x; NCode.Buffers 5.0 needs .NET 10.
- One target framework builds, tests and packs.
- Dropping .NET 10 once it leaves support is another major version.

## References

- [#35](https://github.com/NCodeGroup/Buffers/issues/35): `System.IO.Hashing` 11 reaches `net8.0` consumers.
- [ADR-1000](1000-package-versions-come-from-version-json-through-nerdbank-gitversioning.md): package versions come
  from version.json through Nerdbank.GitVersioning.
- [ADR-1001](1001-each-package-is-validated-against-the-last-released-package.md): each package is validated against
  the last released package.
- [ADR-0136](project-template/0136-packages-follow-semantic-versioning-and-the-assembly-version-moves-only-with-the-major.md):
  packages follow Semantic Versioning and the assembly version moves only with the major.
