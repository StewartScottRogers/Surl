---
id: BL-1790
title: Say in the README, download page, product overview and roadmap that releases are published and carry the NuGet packages
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1789]
touches: [README.md, DOWNLOAD.md, Documentation/Product/Product-Overview.md, Documentation/Planning/Roadmap.md, Application/Surl.Api/Surl.Api.csproj, Application/Surl.Mcp/Surl.Mcp.csproj]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1790 — Say in the README, download page, product overview and roadmap that releases are published and carry the NuGet packages

## Goal

The four documents say what is true of releases now: which are published, that each carries the
binary archives, the two NuGet packages (from v0.1.10, the first release after BL-1789) and `SHA256SUMS`,
and that from the first release after BL-1795 the release job also pushes both packages to nuget.org.

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

- [x] None of the four files says no release is published or tagged while a release exists.
- [x] `DOWNLOAD.md` lists the two `.nupkg` files as release assets covered by `SHA256SUMS`, naming
      the first version that carries them as the first release after BL-1789.
- [x] Each statement about the packages on a package feed matches what the feed holds when the task
      runs.

## Notes

Facts measured 2026-11-26 (UTC dates from GitHub may read 2026-11-27):

- GitHub API: `v0.1.6` to `v0.1.11` published, none draft or prerelease; tags `v0.1.0` to
  `v0.1.11` exist. `v0.1.11` has 15 assets, both `.nupkg` files among them.
- `v0.1.10` is the first tag containing BL-1789 (650b18d08); its release run 45885594047
  succeeded. `v0.1.11` is the first containing BL-1795 (3c94aeed4).
- `v0.1.11` release run 45923075059 failed at `Log in to nuget.org` (HTTP 401, no matching trust
  policy). nuget.org's flat-container index returned 404 for both package IDs. So the documents
  say the job is written to push them and that no version is on nuget.org yet.
- `gh release` and `git tag` are denied to a lane run, so releases were read from the public
  GitHub API and tags from `git log` decorations; `gh run list`/`view` read the runs.

Checks: a search of the four files for "no release", "release is tagged", "not yet published",
"published yet" and "first release tag" found nothing.

Follow-ups filed: BL-1796 (a rerun of the release job fails before the push), BL-1797 (Stewart:
nuget.org trusted publishing policy), BL-1798 (installers, requirements, `Surl.Mcp` README),
BL-1799 (stale non-release status text in the README and product overview).

## Log

- 2026-11-26: Created.
- 2026-11-26: Goal amended: BL-1795 (Stewart, 2026-11-26) pushes the packages to nuget.org, so the documents say so once the first such release is published. Check nuget.org for `Surly.Surl.Api` before writing that claim; also correct the csproj comments in `Application/Surl.Api/Surl.Api.csproj:11` and `Application/Surl.Mcp/Surl.Mcp.csproj:22`.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. README, download page, product overview, roadmap and the two csproj comments say releases are published, carry the .nupkg files from v0.1.10, and that nuget.org holds neither package yet.
