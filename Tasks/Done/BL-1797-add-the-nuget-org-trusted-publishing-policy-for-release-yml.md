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
completed: 2026-11-27
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

- [x] https://api.nuget.org/v3-flatcontainer/surly.surl.api/index.json and
      .../surly.surl.mcp/index.json each list a version.
- [x] The documents' "not on nuget.org yet" statements (README, DOWNLOAD.md, Product-Overview,
      Roadmap) are filed for correction or corrected.

## Notes

- 2026-11-27: The policy already existed; its creator is the nuget.org user StewartRogers86,
  not the GitHub name. Commit c65ad0895 set `user: StewartRogers86` in release.yml, and the
  v0.1.12 release (tag commit 097e1d8e0, run 46064723943) pushed both packages; both index
  documents list 0.1.12. The documents' corrections are filed as BL-1977.

## Log

- 2026-11-26: Created.
- 2026-11-27: Backlog -> Done. Stewart closed it: nuget.org lists Surly.Surl.Api and Surly.Surl.Mcp 0.1.12 after release.yml logged in as StewartRogers86 (c65ad0895); docs corrections filed as BL-1977.
