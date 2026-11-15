# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [5.0.0] - 2026-11-14

### Added

- Public API tracking with `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, recording the surface released in 4.1.0.
- `CONTRIBUTING.md`, `SECURITY.md`, architecture decision records in `docs/decisions/` and a Definition of Done gate (`build/dod.ps1`).
- Publishing to nuget.org through trusted publishing: `.github/workflows/publish.yml` tests and packs the tagged commit without any credential, then pushes the packages and symbol packages from the protected `nuget` environment with a short-lived key, and runs when a GitHub release is published or when it is dispatched by hand with a tag ([#25](https://github.com/NCodeGroup/Buffers/issues/25), [ADR-0135](docs/decisions/project-template/0135-packages-are-published-from-a-github-release-through-trusted-publishing.md)).
- An [OpenSSF Scorecard](https://scorecard.dev/viewer/?uri=github.com/NCodeGroup/Buffers) workflow, `.github/workflows/scorecard.yml`, scores the repository's supply-chain practices on each push to `main` and every week, publishes the score that the README's badge shows, and uploads its findings to code scanning ([#41](https://github.com/NCodeGroup/Buffers/issues/41)).
- Changelog sections for the 4.0.0 and 4.1.0 releases ([#25](https://github.com/NCodeGroup/Buffers/issues/25)).
- Unit tests for every public member of `BufferFactory` and `CryptographicOperationsExtensions`, and for the internal `SecureMemory<T>` and `Gen2GcCallback`; and `build/Test-Coverage.ps1`, which CI runs to fail a pull request below 98% line or branch coverage.
- Package versions come from `version.json` through Nerdbank.GitVersioning: a release tag must name the version it holds, and a package built from any other commit carries `-g<commit>` in its version ([ADR-1000](docs/decisions/1000-package-versions-come-from-version-json-through-nerdbank-gitversioning.md)).
- Package validation compares every package with the published 4.1.0 package and fails the pack on a breaking change; `docs/api-compatibility.md` describes how the public API changes ([ADR-1001](docs/decisions/1001-each-package-is-validated-against-the-last-released-package.md)).
- CSharpier formats the code, and `./build/gate.ps1`, which CI and the local gate run, checks the formatting before the Definition of Done ([ADR-1002](docs/decisions/1002-csharpier-formats-the-code-and-every-csharp-file-starts-with-an-spdx-header.md)).
- `BannedSymbols.Encoding.txt` and `BannedSymbols.Buffers.txt` keep shipping code on `SecureEncoding` and `BufferFactory` ([ADR-1003](docs/decisions/1003-shipping-code-takes-buffers-and-encodings-through-bufferfactory-and-secureencoding.md)).
- A package README for nuget.org with installation, an example and links to the full documentation.
- Coverage on Codecov: on each push to `main`, CI uploads the Cobertura report that `build/Test-Coverage.ps1 -ReportDirectory` keeps to [Codecov](https://codecov.io/gh/NCodeGroup/Buffers) through OIDC, without a stored token, and both READMEs show its coverage badge ([#40](https://github.com/NCodeGroup/Buffers/issues/40)).
- The C# samples in both READMEs are tests in `tests/NCode.Buffers.Tests`, where each one compiles and runs, and [MarkdownSnippets](https://github.com/SimonCropp/MarkdownSnippets) copies them into the READMEs; `./build/gate.ps1`, which CI runs, fails when a README's sample differs from its test, and the quick start is a complete `Signatures` class with its `using` directives ([#42](https://github.com/NCodeGroup/Buffers/issues/42), [ADR-1005](docs/decisions/1005-readme-samples-are-compiled-tests-that-markdownsnippets-copies-into-the-readmes.md)).

### Changed

- `AssemblyVersion` now moves only with the major version; `FileVersion` and `InformationalVersion` keep the exact version. Earlier releases set `AssemblyVersion` to the full version, such as `4.1.0.0`; the 4.x line keeps `4.1.0.0`, so an application built against 4.1.0 still loads later 4.x releases.
- Internal analyzer clean-up (suppressions with justifications and a discarded `Gen2GcCallback` instance); behavior and public API are unchanged.
- Project updated from project-template v0.41.0 to v0.86.1 (build, CI and contribution tooling) ([#26](https://github.com/NCodeGroup/Buffers/issues/26)).
- A published GitHub release starts `publish.yml`, which pushes the packages to nuget.org; `AGENTS.md` sets `release: tag+release`, which has each release tagged and published as its pull request merges ([#25](https://github.com/NCodeGroup/Buffers/issues/25), [ADR-0016](docs/decisions/project-template/0016-releases-are-tagged-and-published-when-their-pull-request-merges.md)).
- `SECURITY.md`, `CONTRIBUTING.md`, `AGENTS.md` and the README describe the new release path ([#25](https://github.com/NCodeGroup/Buffers/issues/25)).
- `BufferFactory` is no longer excluded from code coverage, and `SequenceExtensions` no longer carries exception handlers that no input can reach; behavior and public API are unchanged.
- Every C# file starts with the two-line SPDX header instead of the Apache-2.0 preamble, and the build enforces it ([ADR-1002](docs/decisions/1002-csharpier-formats-the-code-and-every-csharp-file-starts-with-an-spdx-header.md)).
- The solution file is `NCode.Buffers.slnx`, and Source Link comes from the .NET SDK instead of the `Microsoft.SourceLink.GitHub` package.
- The library's source is in `src/NCode.Buffers` and its tests are in `tests/NCode.Buffers.Tests`, as the project template lays out .NET projects; the package and its public API are unchanged ([#34](https://github.com/NCodeGroup/Buffers/issues/34)).
- This release is 5.0.0, and its assemblies carry `AssemblyVersion` 5.0.0.0 ([#35](https://github.com/NCodeGroup/Buffers/issues/35), [ADR-1000](docs/decisions/1000-package-versions-come-from-version-json-through-nerdbank-gitversioning.md)).
- The README opens with installation, a quick start and a table for choosing a type, and adds security notes on what pinning and zeroing do and don't guarantee; the package description no longer calls string splitting zero-allocation, neither README nor the description names a .NET version (a badge reads the target framework from the project file), and the package icon is the current NCode mark.
- **Breaking:** `SecureMemoryPool<T>`, `SecureArrayLifetime<T>`, `SecureSpanLifetime<T>`, `CryptographicOperations.ZeroMemory<T>` and `FixedTimeEquals<T>`, `BufferFactory.Rent<T>`, `CreatePinnedArray<T>` and `CreatePooledBufferWriter<T>`, `Span<T>.GetSecureLifetime()` and the `Sequence<T>` extensions require `T : unmanaged` instead of `T : struct`, so a `T` with reference fields fails to compile instead of throwing `ArgumentException` at run time; code that passes such a `T` with `isSensitive: false` uses `MemoryPool<T>.Shared` or `new Sequence<T>()` directly ([#45](https://github.com/NCodeGroup/Buffers/issues/45), [ADR-1006](docs/decisions/1006-the-secure-apis-constrain-their-element-type-to-unmanaged.md)).

### Removed

- The `Commit attribution` CI check and the `.githooks/commit-msg` hook: agent attribution is avoided, not enforced ([ADR-0018](docs/decisions/project-template/0018-agent-attribution-is-avoided-not-enforced.md)).
- `.github/workflows/main.yml`: its build and test duplicated `ci.yml`, and its publish job and the `NUGET_APIKEY` secret it used are replaced by `publish.yml`; delete the secret from the repository settings ([#25](https://github.com/NCodeGroup/Buffers/issues/25)).
- **Breaking:** The `net8.0` target framework, because .NET 8 left Microsoft support on 2026-11-10: the package targets `net10.0` only, so applications on .NET 8 stay on 4.x and move to .NET 10 to take 5.0 ([#35](https://github.com/NCodeGroup/Buffers/issues/35), [ADR-1004](docs/decisions/1004-the-package-targets-only-supported-lts-dotnet-versions.md)).

### Fixed

- `BufferFactory.CreatePinnedArray`, `CreatePooledBufferWriter` and `CreateArrayBufferWriter` throw `ArgumentOutOfRangeException` for a negative length, a negative `minimumSpanLength` or a non-positive `initialCapacity`, as their documentation says; they threw `OverflowException` or `ArgumentException`, or accepted a negative `minimumSpanLength`.
- `SecureMemoryPool<T>` pools buffers of `PageSize` (4096) bytes for every `T`, as its documentation says. It pooled `PageSize` elements, so a `SecureMemoryPool<int>` pinned 16 KB per buffer, it measured `char` and `bool` by their marshaled size, and a request whose byte count overflowed `int` could get a pooled buffer smaller than it asked for.
- The package carries its description and tags again: `NCode.Buffers/project.props`, which held them, was no longer imported once the `NCode.ProjectSettings.Scaffold` package was removed.
- The README's samples for `FixedSpanBufferWriter<T>`, `FixedMemoryBufferWriter<T>`, `MemorySegment<T>` and `SecureEncoding` compile.
- The README's "Choosing a type" tables fit GitHub's README column instead of cutting off their last column.
- The XML documentation of `StringSegments` and the `SplitSegments` extensions no longer calls splitting allocation-free: it says each split allocates one segment object per substring and `foreach` allocates an enumerator, and shows how to walk the segments without one; the `SplitSegments` example prints each segment's text instead of its type name ([#44](https://github.com/NCodeGroup/Buffers/issues/44)).
- `SequenceExtensions.GetSpanLease` no longer disposes the sequence it reads when the data is in one segment: disposing the lease left a `Sequence<T>` or pooled buffer writer reset to length 0, as its documentation says it does not ([#43](https://github.com/NCodeGroup/Buffers/issues/43)).

## [4.1.0] - 2026-01-21

### Added

- `CreateArrayBufferWriter` methods on `BufferFactory` for simple, non-pooled buffer writers.

## [4.0.0] - 2026-01-16

### Added

- First release after consolidating several projects into NCode.Buffers.

[Unreleased]: https://github.com/NCodeGroup/Buffers/compare/v5.0.0...HEAD
[5.0.0]: https://github.com/NCodeGroup/Buffers/compare/v4.1.0...v5.0.0
[4.1.0]: https://github.com/NCodeGroup/Buffers/compare/v4.0.0...v4.1.0
[4.0.0]: https://github.com/NCodeGroup/Buffers/releases/tag/v4.0.0
