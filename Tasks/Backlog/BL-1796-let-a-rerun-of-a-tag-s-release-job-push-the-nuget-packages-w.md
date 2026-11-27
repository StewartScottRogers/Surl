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
completed:
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

- [ ] The `Release` step succeeds without creating a second release when one named for the tag
      exists, and leaves that release's assets as they are.
- [ ] The workflow's comments say what a rerun does.

## Notes

## Log

- 2026-11-26: Created.
