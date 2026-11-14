# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Public API tracking with `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, recording the surface released in 4.1.0.
- `CONTRIBUTING.md`, `SECURITY.md`, architecture decision records in `docs/decisions/` and a Definition of Done gate (`build/dod.ps1`).
- Publishing to nuget.org through trusted publishing: `.github/workflows/publish.yml` tests and packs the tagged commit without any credential, then pushes the packages and symbol packages from the protected `nuget` environment with a short-lived key, and `.github/workflows/tag-release.yml` starts it when it publishes a release ([#25](https://github.com/NCodeGroup/Buffers/issues/25), [ADR-0135](docs/decisions/project-template/0135-packages-are-published-from-a-github-release-through-trusted-publishing.md)).
- Changelog sections for the 4.0.0 and 4.1.0 releases ([#25](https://github.com/NCodeGroup/Buffers/issues/25)).

### Changed

- `AssemblyVersion` now moves only with the major version; `FileVersion` and `InformationalVersion` keep the exact version. Earlier releases set `AssemblyVersion` to the full version, such as `4.1.0.0`; the 4.x line keeps `4.1.0.0`, so an application built against 4.1.0 still loads later 4.x releases.
- Internal analyzer clean-up (suppressions with justifications and a discarded `Gen2GcCallback` instance); behavior and public API are unchanged.
- Project updated from project-template v0.41.0 to v0.86.1 (build, CI and contribution tooling) ([#26](https://github.com/NCodeGroup/Buffers/issues/26)).
- Releases are tagged and published as their pull request merges (`release: tag+release` in `AGENTS.md`), instead of by hand ([#25](https://github.com/NCodeGroup/Buffers/issues/25), [ADR-0016](docs/decisions/project-template/0016-releases-are-tagged-and-published-when-their-pull-request-merges.md)).
- `SECURITY.md`, `CONTRIBUTING.md`, `AGENTS.md` and the README describe the new release path ([#25](https://github.com/NCodeGroup/Buffers/issues/25)).

### Removed

- The `Commit attribution` CI check and the `.githooks/commit-msg` hook: agent attribution is avoided, not enforced ([ADR-0018](docs/decisions/project-template/0018-agent-attribution-is-avoided-not-enforced.md)).
- `.github/workflows/main.yml`: its build and test duplicated `ci.yml`, and its publish job and the `NUGET_APIKEY` secret it used are replaced by `publish.yml`; delete the secret from the repository settings ([#25](https://github.com/NCodeGroup/Buffers/issues/25)).

## [4.1.0] - 2026-01-21

### Added

- `CreateArrayBufferWriter` methods on `BufferFactory` for simple, non-pooled buffer writers.

## [4.0.0] - 2026-01-16

### Added

- First release after consolidating several projects into NCode.Buffers.

[Unreleased]: https://github.com/NCodeGroup/Buffers/compare/v4.1.0...HEAD
[4.1.0]: https://github.com/NCodeGroup/Buffers/compare/v4.0.0...v4.1.0
[4.0.0]: https://github.com/NCodeGroup/Buffers/releases/tag/v4.0.0
