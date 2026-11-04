# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Public API tracking with `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, recording the surface released in 4.1.0.
- `CONTRIBUTING.md`, `SECURITY.md`, architecture decision records in `docs/decisions/` and a Definition of Done gate (`build/dod.ps1`).

### Changed

- Updated the project from project-template v0.41.0 to v0.61.2: workflow instructions, build settings and analyzers, CI and Dependabot configuration.
- Narrowly suppressed analyzer rules that flag the shipped public API (CA1000, CA1708, CA1710), and made two `Gen2GcCallback.Register` calls explicit about discarding the self-registering instance. Behavior and public API are unchanged.

[Unreleased]: https://github.com/NCodeGroup/Buffers/compare/v4.1.0...HEAD
