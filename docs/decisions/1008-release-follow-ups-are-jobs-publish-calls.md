# 1008. Release follow-ups are jobs that publish.yml calls

- **Status:** Superseded by 0151, [the project template's record](project-template/0151-release-follow-ups-are-jobs-publish-calls.md)
- **Date:** 2026-11-15
- **Deciders:** NCode Group
- **Amends:** ADR-1007, items 1 and 5; project-template ADR-0139, for this repository until
  [NCodeGroup/project-template#381](https://github.com/NCodeGroup/project-template/issues/381) changes the template's.

## Context

Two workflows act on what a Publish packages run produced: `release-provenance.yml` attests the packages and attaches
them, a signed provenance file and an SBOM to the GitHub release (project-template ADR-0139), and `raise-baseline.yml`
opens the pull request that raises the package validation baseline (ADR-1007). Both are started by a `workflow_run`
event on Publish packages.

`tag-release.yml` publishes each release with the workflow's `GITHUB_TOKEN` and starts `publish.yml` through
`workflow_dispatch` (project-template ADR-0137). GitHub creates no workflow run for an event caused by `GITHUB_TOKEN`,
apart from `workflow_dispatch` and `repository_dispatch`, and that includes the `workflow_run` event of a run the token
started. So neither follow-up runs for a release that `tag-release.yml` makes. Publish packages run 43737880017, for
v5.0.2, ran as `github-actions[bot]` and started neither, and releases v5.0.0 to v5.0.2 have no assets.

## Decision

**`publish.yml` calls `release-provenance.yml` and `raise-baseline.yml` as reusable workflows, in jobs that need the
`publish` job, and neither listens for `workflow_run`.**

1. Job `provenance` calls `release-provenance.yml` with the tag. The called job downloads the run's own `packages`
   artifact, checks every package carries the tag's version, attests, attaches and verifies.
2. Job `baseline` calls `raise-baseline.yml` with the tag, for a stable version only.
3. Each caller job grants exactly the permissions its called job declares; `pack` and `publish` keep theirs.
4. `release-provenance.yml` also runs by hand with a tag and the id of a successful Publish packages run, to backfill a
   release while that run's artifact is kept. It refuses an id that isn't a successful run of `publish.yml` in this
   repository.
5. `raise-baseline.yml` opens its pull request as a draft, as every pull request here opens as a draft
   ([#61](https://github.com/NCodeGroup/Buffers/issues/61)); a person marks it ready and merges it.

## Options considered

- `workflow_run`, as before. Rejected: it never fires for the runs `tag-release.yml` starts.
- A final job in `publish.yml` that starts each follow-up with `workflow_dispatch`. Rejected: it needs `actions: write`
  for a hop the call doesn't need, and a run a person started would still need `workflow_run`, so there are two paths.
- Steps inside the `publish` job. Rejected: that job runs in the `nuget` environment with only `id-token: write`, and
  would then also hold release and pull request write access.
- Reusable workflows that `publish.yml` calls (chosen).

## Consequences

- The follow-ups run whoever started Publish packages, and only after its push succeeded.
- The attestation of a release published through `publish.yml` names the tag's ref and commit, because the call runs in
  the Publish run. A backfilled one names the commit the backfill was started on.
- A rerun of a failed follow-up job reruns only that job.
- The Publish run lasts as long as `raise-baseline.yml`'s wait for nuget.org, up to 45 minutes.
- When project-template#381 changes the template's `release-provenance.yml`, the update's merge of this file and of
  `publish.yml` needs a look.

## References

- `.github/workflows/publish.yml`, `.github/workflows/release-provenance.yml`, `.github/workflows/raise-baseline.yml`
- [ADR-1007](1007-a-workflow-raises-the-package-validation-baseline-after-each-release.md)
- [Project-template ADR-0139](project-template/0139-releases-carry-build-provenance.md),
  [ADR-0137](project-template/0137-the-release-workflow-starts-package-publishing.md)
- [#60](https://github.com/NCodeGroup/Buffers/issues/60)
