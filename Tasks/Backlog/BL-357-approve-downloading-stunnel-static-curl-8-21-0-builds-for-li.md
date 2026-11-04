---
id: BL-357
title: Approve downloading stunnel/static-curl 8.21.0 builds for linux-arm64, win-arm64 and osx-x64
priority: Normal
assignee: Stewart
pipeline: direct
depends-on: []
touches: [UpstreamCurlBuilds.json]
requirement: none
created: 2026-11-04
completed:
---
# BL-357 — Approve downloading stunnel/static-curl 8.21.0 builds for linux-arm64, win-arm64 and osx-x64

## Goal

Stewart approves or declines downloading three more upstream curl 8.21.0 builds from
stunnel/static-curl, for `linux-arm64`, `win-arm64` and `osx-x64`, so that the native `surl`
published for those runtime identifiers can be proven against pinned upstream curl. His answer is
recorded here.

## Context

- `.github/workflows/release.yml` publishes native `surl` for six runtime identifiers: `win-x64`,
  `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`. `UpstreamCurlBuilds.json` pins
  upstream curl for only three of them: `win-x64`, `linux-x64` and `osx-arm64`.
- ADR-0016 chose stunnel/static-curl's 8.21.0 release for Linux and macOS, and ADR-0030 chose its
  Windows x64 build. As of 2026-11-04 the same release
  (https://github.com/stunnel/static-curl/releases/tag/8.21.0) also offers
  `curl-linux-aarch64-musl-8.21.0.tar.xz`, `curl-windows-aarch64-8.21.0.tar.xz` and
  `curl-macos-x86_64-8.21.0.tar.xz`. They come from the same upstream tag and the same builder as the
  pinned builds.
- Without them, BL-360 can show that the Arm64 and `osx-x64` binaries start and serve. It cannot show
  that pinned upstream curl completes an exchange with them.
- Downloading an upstream curl build to pin is Stewart's call (root `CLAUDE.md`, "Decisions"). BL-358
  decides what proof runs on each runtime identifier either way. BL-361 does the pinning if he
  approves.

Stewart's question: **do you approve downloading those three stunnel/static-curl 8.21.0 assets, to be
verified and pinned by SHA-256 in BL-361? Or do you accept that `linux-arm64`, `win-arm64` and
`osx-x64` are proven only by BL-360's smoke test, with no upstream curl?**

## Acceptance criteria

- [ ] The Log records Stewart's answer, approved or declined, with the date.
- [ ] If he declines, BL-361 is moved to `Deferred` with his answer as the reason.

## Notes

## Log

- 2026-11-04: Created.
