# 1001. Each package is validated against the last released package

- **Status:** Accepted
- **Date:** 2026-11-14
- **Deciders:** NCode Group

## Context

Project-template ADR-0131 tracks the public API surface in `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`. A line
deleted from `PublicAPI.Shipped.txt` together with its member passes the build, and the files do not show a removed
target framework or other binary breaks.

## Decision

**`NCode.Buffers.csproj` turns on SDK package validation against the last version on nuget.org, so a pack that breaks a
consumer of that version fails.**

1. `EnablePackageValidation` is `true` and `PackageValidationBaselineVersion` is the last published version.
2. Validation runs on every `dotnet pack`, which the full gate, CI and `publish.yml` run, and not in `-Fast`, which does
   not pack.
3. The first change after a release reaches nuget.org raises the baseline, because a version not yet published cannot be
   downloaded.
4. An intended break in a new major version is recorded in `CompatibilitySuppressions.xml` and reviewed, and the file
   goes when the baseline reaches that major.

## Options considered

- The PublicAPI files only. Rejected: the files can be edited to match a break.
- Running ApiCompat by hand before a release. Rejected: it is forgotten, and a break is found late.
- Package validation in every pack (chosen).

## Consequences

- A breaking change fails CI before review.
- The baseline is one more line to raise after each release.
- Packing needs nuget.org reachable for the first restore.

## References

- [API compatibility](../api-compatibility.md)
- `NCode.Buffers/NCode.Buffers.csproj`
- [ADR-0131](project-template/0131-public-api-is-tracked-and-evolves-compatibly.md): the public API is tracked and
  evolves compatibly.
- [ADR-0136](project-template/0136-packages-follow-semantic-versioning-and-the-assembly-version-moves-only-with-the-major.md):
  packages follow Semantic Versioning and the assembly version moves only with the major.
- [Package validation overview](https://learn.microsoft.com/dotnet/fundamentals/apicompat/package-validation/overview)
