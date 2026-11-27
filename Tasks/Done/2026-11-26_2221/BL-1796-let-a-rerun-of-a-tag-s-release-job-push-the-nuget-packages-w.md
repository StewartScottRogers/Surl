---
id: BL-1796
title: Let a rerun of a tag's release job push the NuGet packages when the GitHub release already exists
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/release.yml]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1796 — Let a rerun of a tag's release job push the NuGet packages when the GitHub release already exists

## Goal

Rerunning a tag's `release` job after the GitHub release was created reaches the nuget.org
login and push steps.

## Context

In `.github/workflows/release.yml` the `Release` step runs `gh release create`, which fails
when the release exists, so a rerun stops before `Log in to nuget.org`. The comment above that
step says `--skip-duplicate` lets a rerun succeed, which is not true of the job as written.
Found by BL-1790: the `v0.1.11` run (45923075059) created the release, then failed at the login.
A rerun uses the workflow as it is at the tag, so the change helps tags made after it.

## Acceptance criteria

- [x] The `Release` step succeeds without creating a second release when one named for the tag
      exists, and leaves that release's assets as they are.
- [x] The workflow's comments say what a rerun does.

## Notes

- The `Release` step runs `gh release view <tag> --json isDraft` first; when it succeeds, the
  step exits 0 without calling `gh release create` or uploading anything.
- Choice: a draft release named for the tag counts as existing (the first criterion makes no
  exception). The step warns that it is still a draft, because the job then pushes to nuget.org
  while the GitHub release may be unpublished or short of assets. Failing on a draft was
  rejected for the same criterion.
- Any other `gh release view` failure falls through to `gh release create`, which fails if the
  release exists, so nothing is pushed on a doubtful read.
- Verified by running the step's script under `bash -e` against a stubbed `gh`: no release
  creates one; published and draft releases are left alone (draft warns); all exit 0. The
  workflow itself has not run on GitHub: the `release` job runs only on a tag.
- `dotnet build` clean, fast tests 30128 passed, 3 skipped, `dotnet format` check exit 0.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. A rerun of a tag's release job leaves an existing GitHub release as it is and goes on to the nuget.org login and push
