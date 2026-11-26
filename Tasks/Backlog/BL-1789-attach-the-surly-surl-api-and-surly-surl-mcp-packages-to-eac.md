---
id: BL-1789
title: Attach the Surly.Surl.Api and Surly.Surl.Mcp packages to each GitHub release beside the binaries
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/release.yml, Documentation/Planning/Decisions/ADR-0099-one-server-composition-for-surl-and-surl-api-and-surl-apis-programming-interface.md, Documentation/Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md]
requirement: none
created: 2026-11-26
completed:
---
# BL-1789 — Attach the Surly.Surl.Api and Surly.Surl.Mcp packages to each GitHub release beside the binaries

## Goal

A tagged release's GitHub release carries `Surly.Surl.Api.<version>.nupkg` and
`Surly.Surl.Mcp.<version>.nupkg` beside the `surl-*` and `surl-mcp-*` archives, listed in
`SHA256SUMS`.

## Context

The Surly cookbook re-pins automatically to every Surl release using the release's assets
(Stewart, 2026-11-26, relayed by the Surly session) and today packs `Surly.Surl.Api` from
source because the release has no package. `.github/workflows/release.yml` already packs
both packages on every run as the `nuget-packages` artifact (line ~316, job "Pack the NuGet
packages"); the release job (line ~457-479) downloads only the binaries, writes
`SHA256SUMS` from `surl-*`, and runs `gh release create "$GITHUB_REF_NAME" dist/*`.

BL-643 decided nothing pushes the packages to a feed. Attaching them to the GitHub release
is not a feed push and leaves that decision standing; say so in an amendment to ADR-0099
decision 10 and ADR-0103 decision 7 (decided by Claude under Stewart's delegation).

## Acceptance criteria

- [ ] The release job downloads the `nuget-packages` artifact into `dist/`, so both
      `.nupkg` files at the tag's version are release assets.
- [ ] `SHA256SUMS` lists the two `.nupkg` files as well as every archive.
- [ ] The header comment of `release.yml` (lines ~5-9) says the packages are attached to
      the release and still not pushed to a feed.
- [ ] ADR-0099 and ADR-0103 each carry an amendment saying so.
- [ ] A dry run of the Release workflow on the shift branch (dispatch through
      `dispatch-workflow.ps1`) passes, and its pack job's artifact holds both packages.

## Notes

## Log

- 2026-11-26: Created.
