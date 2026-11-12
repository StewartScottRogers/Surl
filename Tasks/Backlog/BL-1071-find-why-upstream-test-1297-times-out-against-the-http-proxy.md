---
id: BL-1071
title: Find why upstream test 1297 times out against the http-proxy server half
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance, Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests]
requirement: none
created: 2026-11-12
completed:
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

- [ ] The cause of the timeout is stated under Notes.
- [ ] Case 1297 is listed in `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`, or Notes name the
      case part or `<servercmd>` word the proxy server half does not implement, with a skip reason for it.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-12: Created.
