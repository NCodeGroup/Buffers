# 1025. The documentation site is published to GitHub Pages from the default branch

- **Status:** Accepted
- **Date:** 2026-11-18
- **Deciders:** NCode Group
- **Amends:** ADR-1009, for where the site is published and for the API reference it leaves to #56.

## Context

ADR-1009 keeps the site's content in `docs/site/` and its build in the shared toolchain, NCodeGroup/docs, whose
reusable workflow builds a library's site and publishes it to that library's GitHub Pages when its `deploy` input is
true (the toolchain's `README.md` at v0.13.0, read 2026-11-18). It leaves publishing, and an API reference generated
from the XML documentation, to [#56](https://github.com/NCodeGroup/Buffers/issues/56). #56 is closed as not planned:
the site has hand-written reference pages, one of them for every public member, and a generated reference, if one is
ever wanted, belongs in the toolchain, which builds the site of every NCode library.

The toolchain's one-time setup is done: the repository's Pages source is GitHub Actions, with HTTPS enforced, at
`https://ncodegroup.github.io/Buffers/`, and the `github-pages` environment takes deployments from `main` only, with
no required reviewer and no wait timer (the GitHub API, read 2026-11-18).

## Decision

**`.github/workflows/docs.yml` publishes the site to GitHub Pages, at https://ncodegroup.github.io/Buffers/, from
every run on the default branch, and the site has no generated API reference.**

1. The workflow passes `deploy: ${{ github.ref == format('refs/heads/{0}', github.event.repository.default_branch) }}`
   to the toolchain, as the toolchain's `README.md` shows: a push to `main` builds and deploys the site, and a pull
   request only builds it.
2. The reference pages are written by hand, as ADR-1009's first rule says. A generated API reference is a change to
   the toolchain, not to this repository.
3. `README.md` and the package README link to the site, the package README with an absolute URL because nuget.org
   shows it, and the repository's homepage is the site.

## Options considered

- **Leave the site unpublished.** Rejected: a reader sees it only by downloading a workflow run's artifact.
- **Publish only from a release tag.** Rejected: the toolchain deploys from the default branch only, and the
  `github-pages` environment accepts no other ref. A change merged into `main` is released as it merges
  (`release: tag+release` in `AGENTS.md`'s Landing section), so the site and the newest package rarely differ.
- **An API reference generated with DocFX, as #56 proposes.** Rejected: DocFX 2.81.0 lists no members for the C# 14
  extension blocks that hold a large part of the public API (ADR-1009), and the hand-written reference pages cover
  every public member.
- **Publish from the default branch, with hand-written reference pages** (chosen).

## Consequences

- The site follows `main`: a page merged there is live once the workflow's deploy job finishes.
- A deployment runs the toolchain's deploy job with the `pages: write` and `id-token: write` permissions `docs.yml`
  grants; that job runs none of this repository's code.
- The site's address comes from the Pages settings, so a custom domain needs no change here.
- ADR-1009's statements that publishing is part of #56 and that the API reference remains open there give way to
  this record.

## References

- [ADR-1009](1009-the-documentation-site-is-narrative-guides-built-by-a-shared-toolchain-outside-this-repository.md)
- `.github/workflows/docs.yml`, `docs/site/site.config.json`
- [NCodeGroup/docs](https://github.com/NCodeGroup/docs): its `README.md`, sections Workflow and One-time setup
- [#56](https://github.com/NCodeGroup/Buffers/issues/56) and [#99](https://github.com/NCodeGroup/Buffers/issues/99)
