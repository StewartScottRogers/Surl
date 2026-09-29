---
id: BL-072
title: Find and pin an upstream curl Windows build with SMB (smb, smbs)
priority: Normal
assignee: Stewart
pipeline: direct
depends-on: []
touches: [UpstreamCurlBuilds.json, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-072 — Find and pin an upstream curl Windows build with SMB (smb, smbs)

## Goal

Stewart decides how Surl's SMB server gets an upstream curl oracle on Windows: either he approves downloading a named upstream curl Windows build whose `curl --version` lists `smb` and `smbs`, or he accepts measuring SMB only on the Linux and macOS builds already pinned.

## Context

- ADR-0003 decision 5 (`Documentation/Planning/Decisions/ADR-0003-upstream-curl-is-surls-only-oracle.md`): the SMB server is not validated until an upstream curl build with SMB is pinned.
- The reference build, Git for Windows' curl 8.21.0, has no `smb`/`smbs` in `Protocols`.
- BL-026 pinned curl.se's `curl-8.22.0_2-win64-mingw.zip` as a supplementary win-x64 build (`Documentation/Planning/Decisions/ADR-0017-curl-se-8-22-0-windows-build-as-a-supplementary-build.md`, entry in `UpstreamCurlBuilds.json`). Its `curl --version` has `HTTP2` and `HTTP3` but no `smb`/`smbs` in `Protocols` and no `NTLM` in `Features`, so it cannot measure SMB either.
- The Linux and macOS stunnel/static-curl 8.21.0 builds pinned under ADR-0016 (`Documentation/Planning/Decisions/ADR-0016-the-linux-and-macos-upstream-curl-builds-and-how-ci-obtains-them.md`) list `smb smbs`, so SMB may already be measurable there with `Record-CurlExchange.ps1`, on the reference release.
- The only Windows build with SMB named so far is the WinGet curl 8.18.0 build mentioned in ADR-0003's context. It is not 8.21.0, so pinning it would compare SMB against a different release than the Curl port targets.
- Downloading an upstream curl build to pin is Stewart's call (root `CLAUDE.md`, "Decisions"), which is why this task is his.

Stewart's question: **approve downloading which Windows upstream curl build with SMB (name the source and version), or accept measuring SMB only on the Linux/macOS pinned 8.21.0 builds?**

## Acceptance criteria

- [ ] Stewart's answer is recorded as a line in this task's `Log`: either the Windows build he approves (source URL and curl version) or his acceptance that SMB is measured only on the Linux/macOS builds pinned under ADR-0016.
- [ ] If he approves a Windows build: a follow-up task, assigned to Claude, is filed on the board to download it, pin it in `UpstreamCurlBuilds.json` by path and SHA-256, and record an ADR under `Documentation/Planning/Decisions/`; its ID is noted in this task's `Log`.

## Notes

## Log

- 2026-09-28: Created.
