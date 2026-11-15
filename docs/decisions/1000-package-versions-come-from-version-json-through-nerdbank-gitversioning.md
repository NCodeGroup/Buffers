# 1000. Package versions come from version.json through Nerdbank.GitVersioning

- **Status:** Accepted
- **Date:** 2026-11-14
- **Deciders:** NCode Group
- **Supersedes:** project-template ADR-0136, for where the version number comes from; its assembly-version and
  compatibility rules still hold.

## Context

Project-template ADR-0136 takes the version from the release tag, passed to `dotnet pack` as `-p:Version`. Every other
build then packs `1.0.0`, and the package does not say which version or commit it is. The NCode identity repository
versions from a `version.json` file, so the NCode repositories can share one tool and one file format.

## Decision

**`version` in the root `version.json` is the version the next release ships, and Nerdbank.GitVersioning stamps it into
every assembly and package.**

1. The version has three parts and an optional prerelease suffix, with no `{height}`. The release tag is `v<version>`.
2. A `v*` tag build, or one with `-p:PublicRelease=true`, packs exactly that version; any other build packs
   `<version>-g<commit>`.
3. `publish.yml` packs with `-p:PublicRelease=true` and fails when a package's version is not the tag's.
4. `assemblyVersion` is `4.1` with precision `minor`, because 4.1.0 shipped as `4.1.0.0`; the change that starts 5.0.0
   sets it to `{ "precision": "major" }`.
5. `cloudBuild.setVersionVariables` is `false`, because parallel MSBuild nodes tear lines in `GITHUB_ENV`.
6. Every checkout that builds fetches full history.
7. A change that adds public API raises `version` to the next minor version, a breaking change to the next major, and a
   plan with a `Release:` line sets it to that version.

## Options considered

- The version from the release tag only. Rejected: every other build is `1.0.0`.
- `{height}` in the version. Rejected: a version becomes a counter, and a tag can name it only after the commit exists.
- MinVer. Rejected: the NCode repositories share one tool and one file.
- `version.json` with Nerdbank.GitVersioning (chosen).

## Consequences

- A release changes `version.json`.
- A package that is not from a tag is visibly not a release.
- Builds need git history.
- `publish.yml` refuses a mismatched tag.

## References

- `version.json`, `build/ProjectTemplate.props`, `.github/workflows/publish.yml`
- [ADR-0136](project-template/0136-packages-follow-semantic-versioning-and-the-assembly-version-moves-only-with-the-major.md):
  packages follow Semantic Versioning and the assembly version moves only with the major.
- [ADR-0135](project-template/0135-packages-are-published-from-a-github-release-through-trusted-publishing.md):
  packages are published from a GitHub release through trusted publishing.
- [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)
