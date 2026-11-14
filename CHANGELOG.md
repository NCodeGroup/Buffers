# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Public API tracking with `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, recording the surface released in 4.1.0.
- `CONTRIBUTING.md`, `SECURITY.md`, architecture decision records in `docs/decisions/` and a Definition of Done gate (`build/dod.ps1`).

### Changed

- `AssemblyVersion` now moves only with the major version; `FileVersion` and `InformationalVersion` keep the exact version. Earlier releases set `AssemblyVersion` to the full version, such as `4.1.0.0`; the 4.x line keeps `4.1.0.0`, so an application built against 4.1.0 still loads later 4.x releases.
- Internal analyzer clean-up (suppressions with justifications and a discarded `Gen2GcCallback` instance); behavior and public API are unchanged.
- Project updated from project-template v0.41.0 to v0.86.1 (build, CI and contribution tooling).

### Removed

- The `Commit attribution` CI check and the `.githooks/commit-msg` hook: agent attribution is avoided, not enforced ([ADR-0018](docs/decisions/project-template/0018-agent-attribution-is-avoided-not-enforced.md)).

## [4.1.0] - 2026-01-21

### Added

- `CreateArrayBufferWriter` methods on `BufferFactory` for simple, non-pooled buffer writers.

## [4.0.0] - 2026-01-16

### Added

- First release after consolidating several projects into NCode.Buffers.

[Unreleased]: https://github.com/NCodeGroup/Buffers/compare/v4.1.0...HEAD
[4.1.0]: https://github.com/NCodeGroup/Buffers/compare/v4.0.0...v4.1.0
[4.0.0]: https://github.com/NCodeGroup/Buffers/releases/tag/v4.0.0
