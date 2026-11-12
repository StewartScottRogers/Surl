---
id: BL-604
title: Stop conformance tests against a published win-x64 surl timing out before the listening line under parallel load
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1035, BL-1059]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-604 — Stop conformance tests against a published win-x64 surl timing out before the listening line under parallel load

## Goal

The whole `Surl.Conformance` suite passes against a published `win-x64` surl (`SURL_EXECUTABLE`) on
Stewart's machine, as ADR-0085 decision 2 requires before a release tag, with no test failing on a
start timeout.

## Context

- 2026-11-04, BL-360, on the dark factory machine with six lanes building: a native `win-x64`
  publish passed `Test-PublishedSurl.ps1`, but three full runs of
  `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~Surl.Conformance"`
  with `SURL_EXECUTABLE` set failed 5, 16 and a similar number of tests (of 1050), each a different
  set, each a `System.TimeoutException` after 30 s: `SurlOnLoopback.StartTimeout` expiring in
  `PublishedSurlProcess.WaitUntilListeningAsync` before the `Listening on` line. WebSocket and RTSP
  tests and `SurlOnLoopbackTests.StartPublishedAsync_Process_ListensAndStopsOnDispose` were among
  them. The same tests pass when run alone, in-process and against the binary.
- Find the cause before changing a number: a pipe not drained (surl blocking on a full standard
  error), process start cost under parallel test workers, or antivirus scanning each launch. Raising
  the timeout alone hides a deadlock if that is what it is.

## Acceptance criteria

- [x] Notes name the cause, with the evidence for it.
- [ ] Two consecutive full runs of the conformance suite with `SURL_EXECUTABLE` naming a `win-x64`
      publish have no failure, and Notes record their counts.
- [ ] The fast tests stay green.

## Notes

- 2026-11-11, lane 3: no cause found yet. Read so far: `PublishedSurlProcess` drains both pipes with `BeginOutputReadLine`/`BeginErrorReadLine`, so a full standard error cannot block surl; MSTest runs at `Workers = 0` (one per core, 32 here), method level, so up to 32 surl and curl processes start at once. Could not publish win-x64 to reproduce: `dotnet publish` fails, the pinned SDK 10.0.401 is not installed (BL-1035).


- 2026-11-11 (lane 1): `FaultReplyShapes.IsWebSocketClose` now accepts only 1000-1003, 1007-1014 and 3000-4999,
  with an optional non-empty reason of at most 123 UTF-8 bytes (`Encoding.UTF8.GetByteCount`), the rule
  `WebSocketCloseCodes.IsAllowedOnTheWire` applies, written here since `Surl.Cli` may not reference `Surl.WebSocket`.
- Added both ADR files to `touches` (no task in Doing names them): ADR-0124's `ws`, `wss` row now repeats
  ADR-0134 decision 4, and ADR-0134 decision 4's "that ADR-0124's reply table lets through" became "let through
  until BL-903" so it stays true.
- Tests also cover 1005, 1015 (wss), 5000, 1003 and 1007 at the reserved range's edges, and a reason of exactly
  123 UTF-8 bytes (61 two-byte characters and one ASCII one).
- 2026-11-11, lane 5, cause found: thread-pool starvation in the test host, not surl. BL-1035's SDK worry is gone (10.0.404 rolls forward), so the win-x64 publish worked. One surl starts in ~100 ms and 32 at once all print `Listening on` within 0.5 s, so process start cost and antivirus are ruled out; both pipes are drained, so no deadlock. With 32 method-level workers blocking threads, the pool's callbacks that read surl's output ran late: the timeout message (now carrying stdout, stderr and process state) showed `Listening on ...` already in stdout, or nothing, while surl was alive. Raising the pool's minimum threads (`ThreadPoolSetup`, assembly initialize) took the FTP and DICT classes from 28 failures in 65 s to 0 in 9 s, and the full suite from 31 failures to 3-5.
- The remaining 3-5 failures per full run are all HTTP/3 `Could not bind https://127.0.0.1:<port>/: Permission denied` (exit 45): the TCP port surl draws on port 0 sits in a Windows UDP excluded range (`netsh int ipv4 show excludedportrange protocol=udp`). A surl defect outside this task's `touches`, filed as BL-1059; this task depends on it for the two clean runs. Runs after the fix: 5 and 3 failures of 1916, all HTTP/3, none a start timeout.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so surl cannot be published or built to reproduce the timeouts
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Cause fixed (thread-pool starvation); the clean full runs wait on BL-1059, the HTTP/3 UDP bind on a Windows-excluded port
