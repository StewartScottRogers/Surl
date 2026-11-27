---
id: BL-1797
title: Add the nuget.org trusted publishing policy for release.yml so a release can push Surly.Surl.Api and Surly.Surl.Mcp
priority: Normal
assignee: Stewart
pipeline: direct
depends-on: []
touches: [.github/workflows/release.yml]
requirement: none
created: 2026-11-26
completed:
---
# BL-1797 — Add the nuget.org trusted publishing policy for release.yml so a release can push Surly.Surl.Api and Surly.Surl.Mcp

## Goal

nuget.org accepts the `release` job's trusted publishing login, so both packages get pushed.

## Context

The `v0.1.11` release run (45923075059, 2026-11-27 UTC) failed at `Log in to nuget.org`:
HTTP 401, "Make sure you are using the username of the policy creator, not the policy owner:
No matching trust policy owned by user 'StewartScottRogers' was found." On 2026-11-26 nuget.org
held no version of either package. Only Stewart can change the nuget.org account: add (or
correct) a Trusted Publishing policy for repository `StewartScottRogers/Surl`, workflow
`release.yml`, or give the nuget.org username of the policy's creator for the workflow's
`user:` line. `v0.1.11`'s `.nupkg` files are on its GitHub release and can be pushed by hand.
A rerun of that job fails earlier until BL-1796's change is in a tag.

## Acceptance criteria

- [ ] https://api.nuget.org/v3-flatcontainer/surly.surl.api/index.json and
      .../surly.surl.mcp/index.json each list a version.
- [ ] The documents' "not on nuget.org yet" statements (README, DOWNLOAD.md, Product-Overview,
      Roadmap) are filed for correction or corrected.

## Notes

## Log

- 2026-11-26: Created.
