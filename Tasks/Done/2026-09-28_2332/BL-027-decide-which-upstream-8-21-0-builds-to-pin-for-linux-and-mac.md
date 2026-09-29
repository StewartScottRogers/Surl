---
id: BL-027
title: Decide which upstream 8.21.0 builds to pin for Linux and macOS and how CI obtains them
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-027 — Decide which upstream 8.21.0 builds to pin for Linux and macOS and how CI obtains them

## Goal

An accepted ADR names the upstream curl 8.21.0 builds Surl pins for `linux-x64` and
`osx-arm64` (and any other runner platform CI uses), where each comes from, and how the
`CI` workflow obtains and verifies them. BL-028 can then pin them and run the conformance
tests on Linux and macOS.

## Context

- Product overview, open question 2. Stewart approved downloading these builds on
  2026-09-28 (recorded by BL-023).
- ADR-0003: builds are pinned by file SHA-256. The recorder refuses `wsl.exe`, because
  a curl inside WSL cannot be checksummed from Windows. Linux builds are measured on
  Linux.
- Upstream curl only, version 8.21.0 only, from the curl project or a build of tag
  `curl-8_21_0` of https://github.com/curl/curl whose provenance the ADR can state.
  Candidates to weigh: building `curl-8_21_0` from source in CI with pinned options,
  versus a third party's prebuilt 8.21.0. A distribution package that patches curl is
  not upstream.
- `.github/workflows/ci.yml` runs `dotnet test … --filter "TestCategory!=Integration"`
  on Windows, Linux and macOS today. The conformance tests are `Integration`, and BL-020
  makes them `Assert.Inconclusive` where no pinned build exists.
- Lanes run on Windows only, so the Linux and macOS SHA-256 values must come from a CI
  run or a reproducible build. The ADR says which.
- This is a decision delegated to Claude (root `CLAUDE.md`, "Decisions"). The download
  itself is approved.

## Acceptance criteria

- [x] A new ADR, numbered with the next free number, exists in
      `Documentation/Planning/Decisions/`, marked "Decided by Claude under Stewart's
      delegation" and citing Stewart's 2026-09-28 approval, and is indexed in the
      folder's `README.md`.
- [x] For each platform the ADR names the source, the exact build procedure or download
      URL, the TLS backend, and how the `protocols` and `features` it will have are
      known.
- [x] The ADR states how CI obtains the build (download or build, and caching), how it
      verifies the SHA-256 against `UpstreamCurlBuilds.json` before running it, and
      which CI job runs the `Integration`-category conformance tests.
- [x] The ADR states what happens when a runner image changes and a pinned hash no
      longer matches: the job fails loudly. It never silently re-pins.

## Notes

- Decided in ADR-0016: stunnel/static-curl's static 8.21.0 builds of the unpatched tag
  `curl-8_21_0` (`linux-x64` musl, `osx-arm64`), OpenSSL 4.0.1, at
  `/opt/upstream-curl/8.21.0/curl`; downloaded and cached in CI, SHA-256 checked by a
  `pwsh` step before anything runs them; conformance tests in the existing `test` job
  on Linux and macOS. Building from source was rejected because its hash would follow
  every runner-image update.
- Numbered 0016, not 0015: another lane's ADR-0015 (content store exposure options)
  already exists in its checkout.
- The curl project's download page, the static-curl release page and both of its build
  scripts were read on 2026-09-28; the facts quoted in the ADR come from them. Nothing was
  downloaded; BL-028 does the download and pins the hashes.
- Docs-only task, no `.cs` or project file changed, so `verify` was not needed; the build
  and fast tests were run anyway.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ADR-0016 pins stunnel/static-curl's static 8.21.0 builds for linux-x64 and osx-arm64 and fixes how CI downloads, caches, verifies and runs them
