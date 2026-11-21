---
id: BL-1549
title: Prove pinned upstream curl's --aws-sigv4 over HTTP/2 logs in to surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1548]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1549 — Prove pinned upstream curl's --aws-sigv4 over HTTP/2 logs in to surl

## Goal

An `Integration` conformance test runs pinned upstream curl 8.21.0 with `--aws-sigv4` over HTTP/2 (ALPN on
`https://`) against `surl` and gets the file's bytes with exit 0, so the HTTP/2 Signature Version 4 login
BL-1548 fixes stays fixed.

## Context

- The bug (Surly cookbook report 2026-11-21, item 1) and its cause are in BL-1547 and BL-1548.
- Where: `Conformance/Surl.Conformance.UnitTests/UpstreamCurlFetchesFromSurlOverHttp2Tests.cs`, beside
  `GetWithBasic_HttpsByAlpn_IsServedOverHttp2` (it starts `surl` with `--self-signed --user-file` and runs
  `RunUpstreamCurlAsync(... "-w", HttpVersion, surl.UrlOf("hello.txt"))`, then `AssertServedOverHttp2`).
  `UpstreamCurlLogsInToSurlOverHttpTests.AwsSigV4OverHttp_Account_ExitsZeroWithTheFilesBytes` shows the
  `--aws-sigv4 aws:amz:us-east-1:s3 -u <user>:<secret>` arguments and the accounts file used over HTTP/1.1.
- Only pinned builds in `UpstreamCurlBuilds.json` run (ADR-0003); never the Curl port.
- Stand-in: no store, event or fault changes.

## Acceptance criteria

- [x] `UpstreamCurlFetchesFromSurlOverHttp2Tests` has `GetWithAwsSigV4_HttpsByAlpn_IsServedOverHttp2`, marked
  `TestCategory=Integration` as the class's other tests are, running
  `-sS -f -k --aws-sigv4 aws:amz:us-east-1:s3 -u <user>:<secret> -w <HttpVersion> <url>` and asserting
  exit 0, the file's bytes and HTTP version 2 with `AssertServedOverHttp2`.
- [x] A second test, `GetWithAwsSigV4WrongSecret_HttpsByAlpn_ExitsHttpReturnedError`, runs the same with a wrong
  secret and asserts curl's exit 22 and the 401 surl answers over HTTP/2.
- [x] Both pass with `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamCurlFetchesFromSurlOverHttp2Tests"`
  on the lane machine's pinned Windows build.
- [x] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean, and the fast tests
  (`TestCategory!=Integration`) are unchanged.

## Notes

Added both tests beside the Basic-over-HTTP/2 test; 17 of 17 in the class pass on the Windows static 8.21.0 build, 0 skipped. The change is Integration-only, so the fast tests are untouched.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. Pinned curl --aws-sigv4 over HTTP/2 logs in to surl (and a wrong secret gets 401, exit 22), proven by two Integration tests
