# 1009. The documentation site is narrative guides, built by a shared toolchain outside this repository

- **Status:** Accepted
- **Date:** 2026-11-15
- **Deciders:** NCode Group

## Context

[#56](https://github.com/NCodeGroup/Buffers/issues/56) asks for a hosted site with two parts: an API reference
generated from the XML documentation, and guides on the secure-memory model and on choosing a buffer type. It is
deferred because DocFX 2.81.0 lists no members for the C# 14 extension blocks that hold a large part of the public API
(the issue, read 2026-11-15). The guides do not depend on DocFX.

A site generator for the guides is a Node.js project. Where its package manifest and lockfile live matters here:

- Each release carries an SBOM that `.github/workflows/release-provenance.yml` exports from GitHub's dependency graph
  of this repository. The graph lists the packages of the repository's manifests and lockfiles, so a documentation
  toolchain's packages would be in the SBOM of a library whose package has two dependencies.
- OpenSSF Scorecard scores the repository (project-template ADR-0020). Its Vulnerabilities check scans the repository
  recursively for lockfiles (`Recursive: true` in `clients/osv.go`) and takes one point off for each open advisory
  (`score := checker.MaxResultScore - numVulnsFound` in `checks/evaluation/vulnerabilities.go`), in Scorecard 5.6.0
  (read 2026-11-15).
- A lockfile for Astro Starlight holds several hundred packages: 394 in the toolchain's own (counted 2026-11-15).

## Decision

**The site is narrative guides written by hand, built with Astro Starlight by the shared toolchain in
[NCodeGroup/docs](https://github.com/NCodeGroup/docs), and this repository holds the site's content and samples
only.**

1. The site has guides, concept pages and hand-written reference pages. It has no generated API reference.
2. The toolchain repository holds everything that builds a site: the Starlight project, the theme, the plugins, the
   package manifest and its lockfile, and the reusable workflow that builds and publishes a library's site. Its
   `CONTRACT.md` defines what a library holds and what stops a build.
3. This repository holds `docs/site/`: the pages, as `.mdx` files, `site.config.json`, and the site's own components
   and images in `_components/` and `_assets/`. The samples the pages show are compiled tests
   ([ADR-1010](1010-site-samples-are-compiled-tests-in-a-project-of-their-own-included-by-key-when-the-site-is-built.md)).
4. This repository has no `package.json`, no lockfile, no Node.js code and no Astro or Starlight configuration. A page
   needs nothing the toolchain does not provide; a need for more is a change to the toolchain.
5. The site is built from a checkout of the toolchain beside this repository, which writes nothing here.

This answers the documentation half of #56: the guides. The API reference stays deferred there, on the condition the
issue names.

## Options considered

- **DocFX for the whole site, as #56 proposes.** Rejected for the guides: it ties them to the blocked API reference.
- **Astro Starlight in a folder of this repository.** Rejected: the lockfile puts the toolchain's packages in each
  release's SBOM and each of their advisories in the Scorecard result, whether or not the advisory applies to a
  static site.
- **Fumadocs, a Next.js static export.** Rejected: in prototypes built on 2026-11-15 it sent 309 to 344 kB of
  compressed JavaScript to a documentation page where Starlight sent 31 kB, and it makes the site a React application
  to maintain.
- **VitePress.** Not chosen; it was assessed less than the other two, with no prototype. Its stable line is 1.6.4 and
  2.0 is a pre-release (npm, read 2026-11-15).
- **A hosted documentation product.** Rejected: the site is published to GitHub Pages from this organization's
  repositories.
- **The pages in the toolchain repository too.** Rejected: a page changes in the same pull request as the code it
  describes ([docs/README.md](../README.md)), and its samples are tests of this repository.
- **Astro Starlight in a shared toolchain repository, with the content here** (chosen).

## Consequences

- The npm dependency graph, its advisories and its updates are in one repository for every NCode library.
- A change to the site's look, components or build is made in the toolchain and reaches this site when the pinned
  toolchain commit moves.
- A contributor who previews the site needs Node.js and a clone of the toolchain. A contributor who changes only the
  library needs neither.
- The workflow that calls the toolchain's reusable workflow at a pinned commit, and the one-time GitHub Pages setup,
  are part of #56 and not of this record. Without that workflow, no CI job builds the site and nothing publishes it.
- The API reference remains open in #56.

## References

- `docs/site/`, `.github/workflows/release-provenance.yml`
- [#56](https://github.com/NCodeGroup/Buffers/issues/56)
- [NCodeGroup/docs](https://github.com/NCodeGroup/docs): the toolchain, its `README.md` and `CONTRACT.md`
- [Project-template ADR-0020](project-template/0020-github-projects-are-scored-by-openssf-scorecard.md)
- [OpenSSF Scorecard 5.6.0, `checks/evaluation/vulnerabilities.go`](https://github.com/ossf/scorecard/blob/v5.6.0/checks/evaluation/vulnerabilities.go)
- [Astro Starlight](https://starlight.astro.build)
