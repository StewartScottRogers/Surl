---
id: BL-1315
title: Pin the libtest programs by SHA-256 as kind libtest and verify them in CI (ADR-0174)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1310]
touches: [UpstreamCurlBuilds.json, .github/actions/upstream-curl]
requirement: none
created: 2026-11-16
completed:
---
# BL-1315 — Pin the libtest programs by SHA-256 as kind libtest and verify them in CI (ADR-0174)

## Goal

UpstreamCurlBuilds.json pins each libtest program by SHA-256 as kind libtest, and CI verifies them.

## Context

ADR-0174 decision 2 and 4; follows the Debug pin of BL-1310 and the .github/actions/upstream-curl debug input.

## Acceptance criteria

- [ ] UpstreamCurlBuilds.json has a linux-x64 entry kind libtest, role supplementary, with a programs map of name to SHA-256.
- [ ] The upstream-curl action fails when any program hash differs from the pin, and a test pins the schema.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
