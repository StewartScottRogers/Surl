---
id: BL-1620
title: Make the HTTPS reset cut give upstream curl one exit code on OpenSSL builds, or record why it cannot
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests/UpstreamCurlMeetsASurlCutDownloadTests.cs, Documentation/Planning/Decisions, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-23
completed:
---
# BL-1620 — Make the HTTPS reset cut give upstream curl one exit code on OpenSSL builds, or record why it cannot

## Goal

`DownloadOverHttps_CutAfterBytes_OnLinux_ExitsAsMeasured("reset", ...)` and `..._OnMacOs_...("reset", ...)` pass
every run. Either surl's reset over TLS gives upstream curl one exit code every time, or ADR-0188 records, with
measurements, that OpenSSL curl reports either of two codes by timing, and the test asserts exactly that recorded
set.

## Context

- Upstream curl's exit code for a `drop;after-bytes=N` (`close=reset`) cut over HTTPS differs between runs on the
  OpenSSL builds:
  - PR #142, CI run 45155542608: Linux and macOS both exited 55, "Recv failure: Connection reset by peer".
  - BL-1619 then pinned 55 for them (ADR-0188 amendment 2).
  - PR #143 (shift 20261123-064807), the next CI run: Linux and macOS both exited **56**, same stderr, ~11,853
    bytes kept.
- So on OpenSSL, the same reset is sometimes reported as a send error (55) and sometimes as a receive error (56),
  depending on whether curl is inside a TLS write or a TLS read when the RST lands. Windows (Schannel) gave 56 both
  times. The graceful cut passed on every platform in PR #143.
- Stewart wants exact assertions, not tolerances: see BL-1413, where a "7 or 28" tolerance was tightened after
  measurement. A two-code assertion is acceptable only if measurement shows it is the client's own nondeterminism
  and nothing surl does can fix it.

## Acceptance criteria

- [ ] The test runs the HTTPS reset cut at least 30 times on Linux and on macOS in one dispatched `ci` run (a
      temporary repeat test; park as Blocked "Waiting for run:<id>"), and the Log tallies the exit codes per
      platform.
- [ ] The Log names why curl is sometimes writing (for example, TLS 1.3 post-handshake messages, or curl's own
      close_notify on its read path) and whether surl can avoid it. For example: reset only after the client's
      pending TLS records are read, or wait for the request to be fully read before counting toward after-bytes.
- [ ] If surl can make it one code, it is fixed in `Surl.Core` with a named fast test, and a second repeat run shows
      one code on each platform, which the test then pins exactly.
- [ ] If it cannot, an ADR-0188 amendment records the measured set with the run id and the reason. The OpenSSL https
      reset test asserts that set (55 or 56) and no wider, and Windows keeps its exact 56.
- [ ] The temporary repeat test is removed; `dotnet build` clean, fast tests green, and a `ci` run passes
      "Build and test" on ubuntu-latest and macos-latest.

## Notes

- The http, ftp and ftps cases and the graceful https case are stable; leave them as they are.

## Log

- 2026-11-23: Created.
- 2026-11-23: Backlog -> Doing.
- 2026-11-23: Measurement plan: lanes cannot push, so no temporary repeat test can reach CI. Dispatched `ci` with attempts=20 on factory/phase-1 (run 45211169210) instead: the existing reset test asserts 55 on Linux and macOS, and its failure message reports the actual exit code and stderr, so the failed legs tally the 56s. Next run reads the tally, decides fix vs ADR-0188 amendment 3.
- 2026-11-23: Doing -> Blocked. Waiting for run:45211169210 (ci attempts=20, tally of HTTPS reset exit codes)
