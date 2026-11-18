# 1022. JetBrains.Annotations is a build-time reference, not a package dependency

- **Status:** Accepted
- **Date:** 2026-11-17
- **Deciders:** NCode Group
- **Amends:** 1017

## Context

`src/NCode.Buffers/NCode.Buffers.csproj` references JetBrains.Annotations for the `[PublicAPI]` attribute on the
public types. Its attributes are declared `[Conditional("JETBRAINS_ANNOTATIONS")]`, and the build does not define that
symbol, so none of them reaches `NCode.Buffers.dll`: the assembly built from the 5.1.2 source names no JetBrains type
(read 2026-11-18). Without `PrivateAssets`, `dotnet pack` lists JetBrains.Annotations as a dependency in the nuspec,
and every consumer restores a package the library never loads.
[ADR-1017](1017-the-pooled-buffer-writers-return-nerdbank-streams-sequence.md) records it as the package's one other
dependency.

## Decision

**JetBrains.Annotations is referenced with `PrivateAssets="all"`: the library's own build and its IDE analysis use
it, and the package lists only the dependencies its assembly loads.** A reference that the assembly does not load at
run time, such as an analyzer or an annotation package, is private too.

## Options considered

- Keep the dependency and document it. Rejected: it costs every consumer a restore and a line in their dependency
  graph, and gives them nothing.
- Remove JetBrains.Annotations and the `[PublicAPI]` attributes. Rejected: the attributes keep the IDE from reporting
  the public API as unused, and `PrivateAssets` already keeps them out of the package.
- Keep the reference private (chosen).

## Consequences

- The nuspec lists one dependency, Nerdbank.Streams, which brings dependencies of its own. The public API and the
  assembly do not change, so package validation passes against the baseline.
- A consumer that used JetBrains.Annotations only through this package references it directly.
- ADR-1017's status reads "Accepted (amended by 1022)", and the documents that count the dependencies say one.

## References

- [ADR-1017](1017-the-pooled-buffer-writers-return-nerdbank-streams-sequence.md): the record whose statement of the
  package's dependencies this one amends.
- [JetBrains.Annotations](https://www.jetbrains.com/help/resharper/Code_Analysis__Code_Annotations.html)
- [Controlling dependency assets](https://learn.microsoft.com/nuget/consume-packages/package-references-in-project-files#controlling-dependency-assets)
