---
id: BL-022
title: Approve downloading curl.se's upstream 8.21.0 Windows build with SMB, HTTP/2 and HTTP/3
priority: Normal
assignee: Stewart
pipeline: direct
depends-on: []
touches: [UpstreamCurlBuilds.json]
requirement: none
created: 2026-09-28
completed:
---
# BL-022 — Approve downloading curl.se's upstream 8.21.0 Windows build with SMB, HTTP/2 and HTTP/3

## Goal

Stewart approves or declines downloading the curl project's own Windows build of
upstream curl 8.21.0 from https://curl.se/windows/, to be pinned as a second reference
build. His answer is recorded here.

## Context

- Product overview, open question 1, and ADR-0003, decision 5. The pinned Git for
  Windows 8.21.0 build lacks `smb`, `smbs`, HTTP/2 and HTTP/3 (its `protocols` and
  `features` lines in `UpstreamCurlBuilds.json`). Until a build with them is pinned,
  `Surl.Protocol.Smb` and Surl's HTTP/2 and HTTP/3 cannot be validated (Phases 5 and 6).
- The curl.se Windows build is the candidate the product overview names. That it
  includes SMB, HTTP/2 and HTTP/3 is not verified yet. Checking its `curl --version`
  after download is part of pinning it.
- Downloading an upstream curl build is Stewart's (root `CLAUDE.md`, "Decisions").
  Pinning it afterwards is a decision Claude records in an ADR.
- Nothing in Phase 1's first tasks waits on this. It is filed now because Phases 5 and 6
  do, and approval takes time.

## Acceptance criteria

- [x] The Log below records Stewart's answer, approved or declined, with the date.
- [x] If approved, a follow-up task is filed (by `task-planner`) to download the build,
      verify its `curl --version` protocols and features, record the pin in an ADR, and
      add it to `UpstreamCurlBuilds.json`. Its ID is recorded in the Log.

## Notes

The same approval will later be needed for Linux and macOS builds (product overview,
open question 2). That is not part of this task.

## Log

- 2026-09-28: Created.
- 2026-09-28: Approved by Stewart in the /task-plan session ("1. yes"). He also approved downloading Linux and macOS builds ("4, download them").
- 2026-09-28: Follow-up filed: BL-026 downloads, verifies and pins the Windows build; BL-027 and BL-028 cover Linux and macOS. Ready to move to Done; the planner does not move tasks.
- 2026-09-28: Stewart chose option A: take curl.se's latest Windows build (8.22.0_2, built 2026-09-02, checked on curl.se 2026-09-28) as a supplementary build for SMB, HTTP/2 and HTTP/3 only; 8.21.0 stays the reference release. BL-026 retargeted accordingly.
