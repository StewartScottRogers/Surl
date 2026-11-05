---
id: BL-690
title: Make CI's OpenLDAP upstream curl build select its Alpine packages again
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/actions/upstream-curl]
requirement: none
created: 2026-11-04
completed:
---
# BL-690 — Make CI's OpenLDAP upstream curl build select its Alpine packages again

## Goal

CI's `Build and test (ubuntu-latest)` leg obtains the OpenLDAP build of upstream curl 8.21.0 again.

## Context

- CI run https://github.com/StewartScottRogers/Surl/actions/runs/41980108989 (commit `b1a280aa`,
  2026-11-05 UTC): step `Obtain the pinned upstream curl` fails five times with
  `ERROR: unable to select packages: musl-1.2.5-r12: breaks: musl-dev-1.2.5-r13`, the Alpine
  container's installed musl older than the repository's musl-dev. A red Linux leg blocks the
  dark factory's merge to `master`.
- Likely fix: `apk upgrade` (or `apk add --upgrade musl`) before installing the build packages, or
  pin the Alpine image; whatever builds the same pinned SHA-256 (ADR-0076, ADR-0078). The built
  file must still hash to the `UpstreamCurlBuilds.json` pin.

## Acceptance criteria

- [ ] A CI run on a branch holding the fix passes `Obtain the pinned upstream curl` on ubuntu-latest; its URL is in Notes.
- [ ] The OpenLDAP build it produces hashes to the pinned SHA-256.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
