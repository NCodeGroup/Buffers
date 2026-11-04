# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Public API tracking with `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, recording the surface released in 4.1.0.
- `CONTRIBUTING.md`, `SECURITY.md`, architecture decision records in `docs/decisions/` and a Definition of Done gate (`build/dod.ps1`).

### Changed

- `AssemblyVersion` now moves only with the major version (`4.0.0.0` for every 4.x release); `FileVersion` and `InformationalVersion` keep the exact version. Earlier releases set `AssemblyVersion` to the full version, such as `4.1.0.0`.
- Internal analyzer clean-up (suppressions with justifications and a discarded `Gen2GcCallback` instance); behavior and public API are unchanged.
- Project updated from project-template v0.41.0 to v0.61.2 (build, CI and contribution tooling).

[Unreleased]: https://github.com/NCodeGroup/Buffers/compare/v4.1.0...HEAD
