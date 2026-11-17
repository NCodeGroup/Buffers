# 1007. A workflow raises the package validation baseline after each release

- **Status:** Superseded by 0150, [the project template's record](project-template/0150-a-workflow-raises-the-package-validation-baseline-after-each-release.md)
- **Date:** 2026-11-15
- **Deciders:** NCode Group
- **Amends:** ADR-1001, item 3 and its consequence "the baseline is one more line to raise after each release".

## Context

ADR-1001 validates every pack against the last published package, so after each release someone must raise
`PackageValidationBaselineVersion`, and only once nuget.org lists the version, which takes minutes after the publish
succeeds. Done by hand, it is a follow-up of every release plan that blocks the plan from finishing.

## Decision

**`.github/workflows/raise-baseline.yml` opens a pull request that raises the baseline after each successful publish,
and a person merges it.**

1. It runs when `Publish packages` succeeds, or by hand for a tag, and calls `build/Update-Baseline.ps1`.
2. The script waits up to 30 minutes for nuget.org to list the version, never lowers the baseline, and adds a
   `### Changed` entry under `## [Unreleased]`.
3. The workflow pushes `baseline-<version>`, opens the pull request and starts `ci.yml` on that branch, because a pull
   request opened with the workflow's token starts no workflow.
4. A release plan lists no baseline follow-up.
5. The pull request opens ready for review, not as a draft: it holds one line a person merges after CI passes, and a draft would add a step to every release.

## Options considered

- A manual follow-up (ADR-1001). Rejected: it recurs and blocks plans.
- Reading the latest version from nuget.org during the build. Rejected: a build would depend on the network and change
  without a commit.
- Pushing straight to `main`. Rejected: the default branch takes changes only through pull requests (ADR-0005).
- A workflow that opens the pull request (chosen).

## Consequences

- Settings > Actions > General must allow GitHub Actions to create pull requests.
- Every release produces one small pull request to merge.
- `ci.yml` runs on the branch from a `workflow_dispatch`, so its check reports on the pull request's head commit.

## References

- `.github/workflows/raise-baseline.yml`, `build/Update-Baseline.ps1`, `build/Test-UpdateBaseline.ps1`
- [ADR-1001](1001-each-package-is-validated-against-the-last-released-package.md)
