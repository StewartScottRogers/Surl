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

### State on 2026-11-26 (lane 1, first run)

- Criteria 1 to 4 were written and checked, but left **uncommitted** because the task is waiting on
  a run (lane rule 6): the shift stashes them. Only the task board change is committed. Recover them
  from the stash, or redo them:
  1. `release.yml`, release job: after the `surl-*` download, a second
     `actions/download-artifact@v4` with `name: nuget-packages`, `path: dist`. Checksums `run:`
     becomes three lines: `version="${GITHUB_REF_NAME#v}"`,
     `sha256sum surl-* "Surly.Surl.Api.$version.nupkg" "Surly.Surl.Mcp.$version.nupkg" > SHA256SUMS`,
     `cat SHA256SUMS`. The explicit names fail the release when a package is missing at the tag's
     version.
  2. `release.yml` comments: the header (lines 5 to 11) and the `pack` job's comment say a tag's
     GitHub release carries both packages, `SHA256SUMS` lists all fourteen files, and nothing is
     pushed to a feed.
  3. ADR-0099: new last section "Amendment 2026-11-26: the GitHub release carries the package
     (BL-1789)". ADR-0103: new "Amendment 4 — the GitHub release carries the NuGet package
     (BL-1789)". Both: decided by Claude under Stewart's delegation; on a `v*` tag the release job
     attaches the `.nupkg` and lists it in `SHA256SUMS`; why: the Surly cookbook re-pins from the
     release's assets (Stewart, 2026-11-26, relayed by the Surly session); not a feed push, so
     BL-643 stands.
- Checked locally with placeholder files: the checksum step lists archives and both packages, and
  under `bash -e` (Actions' default) exits 1 when a package is missing.
- Criterion 5: dispatched run 45836201806 (Release, `factory/phase-1` at `3debf484d`). That commit
  does **not** hold this edit, which a lane cannot push before the task is Done. The edit changes
  only comments and the tag-gated release job, which no dry run executes, so the run's `pack` job is
  the one this edit ships with. The edited file gets its own dry run from the `pull_request` trigger
  on the shift's merge pull request (`release.yml` is in its paths).
- **Next run:** do not dispatch again. If run 45836201806 passed and its `nuget-packages` artifact
  holds both `.nupkg` files, restore the edit, tick all five boxes and move to Done.
- The dispatch script exited 1 after dispatching (filed as BL-1791), so the run ID was read from
  the URL `gh` printed and confirmed with `gh run view`.
- Documents outside `touches` that describe releases are filed as BL-1790.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Blocked. Waiting for run:45836201806 (release)
