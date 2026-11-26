---
id: BL-1790
title: Say in the README, download page, product overview and roadmap that releases are published and carry the NuGet packages
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1789]
touches: [README.md, DOWNLOAD.md, Documentation/Product/Product-Overview.md, Documentation/Planning/Roadmap.md]
requirement: none
created: 2026-11-26
completed:
---
# BL-1790 — Say in the README, download page, product overview and roadmap that releases are published and carry the NuGet packages

## Goal

The four documents say what is true of releases now: which are published, that each carries the
binary archives, the two NuGet packages (from the first release after BL-1789) and `SHA256SUMS`,
and that nothing in `release.yml` pushes a package to a feed.

## Context

BL-1789 attaches `Surly.Surl.Api` and `Surly.Surl.Mcp` to each tag's GitHub release (ADR-0099 and
ADR-0103 amendments, 2026-11-26). Statements found stale on 2026-11-26: `README.md` "Download" and
the `Surl.Api` and `Surl.Mcp` paragraphs ("No release has been published yet", "not yet
published"); `DOWNLOAD.md` opening lines ("No release of Surl has been published yet", Phase 1, HTTP
only) and the release asset list; `Product-Overview.md` "Publishing" ("no release is tagged");
`Roadmap.md` hosts phase ("packed but not yet published"). The git history and ADR-0187 show
releases already tagged; read the published releases and whether the packages are on a feed before
writing either claim.

## Acceptance criteria

- [ ] None of the four files says no release is published or tagged while a release exists.
- [ ] `DOWNLOAD.md` lists the two `.nupkg` files as release assets covered by `SHA256SUMS`, naming
      the first version that carries them as the first release after BL-1789.
- [ ] Each statement about the packages on a package feed matches what the feed holds when the task
      runs.

## Notes

## Log

- 2026-11-26: Created.
