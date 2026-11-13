---
id: BL-1106
title: Fix flaky GetWithHttp3_File_IsServedOverHttp3NotTheTcpFallback (curl falls back to HTTP/2)
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests, Protocols/Http/Surl.Http3.UnitLibrary]
requirement: none
created: 2026-11-13
completed:
---
# BL-1106 — Fix flaky GetWithHttp3_File_IsServedOverHttp3NotTheTcpFallback (curl falls back to HTTP/2)

## Goal

`GetWithHttp3_File_IsServedOverHttp3NotTheTcpFallback` passes every time on Linux and macOS CI, because upstream curl completes the exchange over HTTP/3 instead of falling back to HTTP/2 over TCP.

## Context

Flaky on CI. Found by the repeated-legs run https://github.com/StewartScottRogers/Surl/actions/runs/43436178802 (`gh workflow run CI --ref factory/phase-1 -f attempts=5`, BL-1104/BL-1105). It failed in macOS attempts 1 and 5 and Ubuntu attempts 2 and 5 (4 of 10 Unix legs). Message each time: `CollectionAssert.AreEqual failed. Element at index 16 do not match. Expected: 51 Actual: 50` - the `-w` http_version byte was `2` (0x32), not `3` (0x33). Earlier: macOS in CI run 43422998980 (PR #31), curl fell back after about 10 s.

Test: `Conformance/Surl.Conformance.UnitTests/UpstreamCurlFetchesFromSurlOverHttp3Tests.cs`. Suspect the QUIC/HTTP/3 listener in `Surl.Http3.UnitLibrary` being slow or lossy to answer curl's first Initial, so curl's happy-eyeballs HTTP/3-vs-TCP race is lost. Reproduce with repeated runs; measure upstream curl, never the Curl port.

## Acceptance criteria

- [ ] The cause is stated under Notes, with evidence from a pinned upstream curl run or the CI logs.
- [ ] The test (or the server) is changed so the race cannot go to TCP, without weakening the assertion that HTTP/3 was used.
- [ ] A repeated CI run (`gh workflow run CI --ref <branch> -f attempts=5`) shows no failure of this test, or its Log says why that cannot be run.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

Filed by BL-1105 on 2026-11-13.

## Log

- 2026-11-13: Created.
