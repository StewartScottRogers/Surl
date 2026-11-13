---
id: BL-1071
title: Find why upstream test 1297 times out against the http-proxy server half
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Documentation/Planning/Decisions, Conformance/Surl.Conformance.UnitTests, Documentation/Conformance, Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1071 — Find why upstream test 1297 times out against the http-proxy server half

## Goal

Upstream test case 1297 (tag `curl-8_21_0`) passes against Surl's `http-proxy` server half, or its skip or failure is
recorded with the reason the half cannot play it.

## Context

BL-556 composed the `http-proxy` and `https-proxy` server halves (`HttpProxyUpstreamTestCaseServer`). Of the 29
cases naming them, 1297 is the one that fails: curl stops after the 60-second case limit (`fail timeout`) on
the Windows reference pin. The run is `UpstreamTestCasesAgainstSurlTests`; the test log prints the exchange
notes of a failing case. Start by reading the case's `<client>`, `<reply>` and `<proxy>` parts and the notes.

## Acceptance criteria

- [x] The cause of the timeout is stated under Notes.
- [x] Case 1297 is listed in `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`, or Notes name the
      case part or `<servercmd>` word the proxy server half does not implement, with a skip reason for it.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line and
      branch coverage.

## Notes

- 2026-11-12 (lane 4): Published win-x64 native surl (`dotnet publish Application/Surl.Console -r win-x64`, commit e405b969, includes BL-1059) and ran with `SURL_EXECUTABLE` set. HTTP/3 classes (`FullyQualifiedName~Http3`, 26 tests): 13 of 14 runs 26/26 passed, no exit 45 or `Permission denied`; one run showed 1 failure whose text was not captured (not reproduced in the 12 runs after it). Full `Surl.Conformance` run: 1872 passed, 120 skipped, 1 failed in 11m46s; the one failure is `UpstreamCurlFetchesFromSurlOverHttpsTests`' Curl-port Phase 7 finding ("port defect Http.md does not list", `GetWithInsecure_SameFileTwice...`), HTTPS and the Curl port's behaviour, not HTTP/3. No HTTP/3 `Permission denied` failure, so BL-1059's retry holds. Not filed: the HTTPS finding belongs to the Curl-port phase.
- 2026-11-12 (Claude, lane 1): cause: not the proxy half. The tunnel's origin is the HTTP half, which since BL-1078
  held the connection open on 1297's empty `<data>`; upstream's `sws` closes after a zero-byte reply (sws.c
  `!count`), so curl never saw the close through the tunnel and waited out the 60 s. Fix in the HTTP half: close after
  an empty part on an HTTP/1.1 request, hold on HTTP/1.0 (test 1548's telnet probe) - ADR-0152. `touches` widened to
  `Surl.Protocol.Http.*` and the decisions folder; no task in Doing names them. Local run of 1297 + 1548: both pass.
  1297 added to the win-x64 passing list and row.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. 1297 passes: the HTTP half closes after an empty reply part on HTTP/1.1 (ADR-0152)
