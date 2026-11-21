# 1032. The site samples run one at a time

- **Status:** Accepted
- **Date:** 2026-11-20
- **Deciders:** NCode Group
- **Amends:** ADR-1010, item 1, for one file the site test project also holds.

## Context

Many site samples return a sensitive rental to the shared secure pool, then check that the memory they kept is zero.
xUnit runs test classes in parallel, so a sample in another class can rent that memory and write to it before the check
runs. `SiteSplitTextOverSensitiveBufferSample_Runs` failed that way once in `./build/gate.ps1 -Fast` while #124 was
prepared, and passed in eight reruns.

## Decision

**`tests/NCode.Buffers.Site.Tests` also holds `AssemblyInfo.cs`, whose
`[assembly: CollectionBehavior(DisableTestParallelization = true)]` runs the site samples one at a time.**

It holds no class and no sample, and ADR-1010's other rules stand.

## Options considered

- A serialized collection per class, as `NCode.Buffers.Tests` does. Rejected: most sample classes check returned memory,
  and a new page's class would race unless its author remembered the attribute.
- An `xunit.runner.json`. Rejected: it is still a file in the project, which the project file must also copy, and it
  sits farther from the reason.
- Leave them parallel. Rejected: a gate that fails at random.
- One assembly attribute (chosen).

## Consequences

- The samples take about a second instead of under one.
- A new sample needs no collection attribute.
- The serialized collections `WatchTheSecurePoolSamples` and `QuickStartSamples`' console collection already had stay
  and change nothing.

## References

- [ADR-1010](1010-site-samples-are-compiled-tests-in-a-project-of-their-own-included-by-key-when-the-site-is-built.md)
- [`tests/NCode.Buffers.Site.Tests/AssemblyInfo.cs`](../../tests/NCode.Buffers.Site.Tests/AssemblyInfo.cs)
- https://github.com/NCodeGroup/Buffers/pull/124
