# Public API compatibility

NCode.Buffers is a published library: once a release ships a `public` type or member, it is a contract every consumer
depends on. The rules are project-template
[ADR-0131](decisions/project-template/0131-public-api-is-tracked-and-evolves-compatibly.md) and
[ADR-0136](decisions/project-template/0136-packages-follow-semantic-versioning-and-the-assembly-version-moves-only-with-the-major.md):

- A shipped public surface evolves additively. A new type, member or overload is additive; changing an existing
  signature is breaking, so add an overload instead.
- Required inputs are constructor parameters; optional inputs are `init` properties.
- Deprecate with `[Obsolete]` in a minor version and remove in the next major version.
- Nullability is part of the signature: changing `string` to `string?` on the public surface is a tracked, potentially
  breaking change.

## What checks it

1. The PublicAPI analyzer (`RS0016` for a symbol that is not listed, `RS0017` for a listed symbol that is gone) compares
   the compiled public symbols against `src/NCode.Buffers/PublicAPI.Shipped.txt` and `src/NCode.Buffers/PublicAPI.Unshipped.txt`
   on every build.
2. Package validation (`CP` diagnostics) compares the package with the one `PackageValidationBaselineVersion` names, on
   every `dotnet pack`: `./build/gate.ps1`, CI and `publish.yml`
   ([ADR-1001](decisions/1001-each-package-is-validated-against-the-last-released-package.md)).

## Changing the public API

1. Run `./build/Update-PublicApi.ps1`, and review the `PublicAPI.Unshipped.txt` diff in the pull request.
2. A removal is a `*REMOVED*` line in `PublicAPI.Unshipped.txt`, allowed only in a new major version.
3. Raise `version` in `version.json`
   ([ADR-1000](decisions/1000-package-versions-come-from-version-json-through-nerdbank-gitversioning.md)): the next
   minor version for an addition, the next major version for a break.

## Releasing

The release change runs `./build/Update-PublicApi.ps1 -Release` and sets `version.json` to the release version. Once the
release is on nuget.org, `.github/workflows/raise-baseline.yml` opens a pull request that sets `PackageValidationBaselineVersion` in
`src/NCode.Buffers/NCode.Buffers.csproj` to it ([ADR-1007](decisions/1007-a-workflow-raises-the-package-validation-baseline-after-each-release.md)); `./build/Update-Baseline.ps1 -Version <version>` does the same by hand.

An intended break in a new major version is recorded with
`dotnet pack src/NCode.Buffers -p:ApiCompatGenerateSuppressionFile=true`, which writes
`src/NCode.Buffers/CompatibilitySuppressions.xml`. That file is reviewed like the API diff and deleted when the baseline moves
to the new major's first release.

## Hand-editing PublicAPI files

Prefer the code fix the IDE offers; when editing the `.txt` by hand, the non-obvious cases are:

- **Interface variance is not written:** list `IFoo<TJob>`, never `IFoo<in TJob>` (otherwise `RS0016` for the real
  symbol **and** `RS0017` for the annotated line).
- **`record class` (non-positional):** list the type and each `.Prop.get` / `.Prop.init`; do **not** list a
  `Type.Type() -> void` constructor line (the analyzer excludes the implicit parameterless ctor — `RS0017` if present).
- **`readonly record struct` (non-positional):** **do** list `Type.Type() -> void`, plus the properties. Compiler-synthesized
  record members (`Equals`/`GetHashCode`/`ToString`/`Deconstruct`/`PrintMembers`/`with`/operators) are never listed.
- **Extension blocks** (C# `extension(Receiver)` members) need three lines: the class, the block marker
  `Ns.Class.extension(Receiver!)` (easy to forget → `RS0016`), then the member line.
- **Fastest reliable path:** write the best guess, build, and let `RS0016` (missing) / `RS0017` (extra or wrong)
  dictate the exact text.
