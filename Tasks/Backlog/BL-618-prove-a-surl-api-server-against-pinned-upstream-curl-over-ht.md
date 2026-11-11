---
id: BL-618
title: Prove a Surl.Api server against pinned upstream curl over HTTP and FTP
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-617, BL-1035]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-618 — Prove a Surl.Api server against pinned upstream curl over HTTP and FTP

## Goal

`Integration` tests start servers through `Surl.Api` on real loopback sockets, run a pinned upstream curl 8.21.0
build against them, and show that the exchange completes exactly as it does against the `surl` executable, and that
the API's exchange record holds the bytes curl sent.

## Context

- Oracle: upstream curl only (ADR-0003); the Curl port is not used. The pinned builds are run through
  `Conformance/Surl.Conformance.UnitTests/PinnedUpstreamCurl.cs` (`PinnedUpstreamCurl.RunAsync`), which refuses an
  unpinned binary; tests are inconclusive where the pin is absent, as the existing `UpstreamCurlFetchesFromSurl*`
  classes are.
- The expected results already exist for the executable: `UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_File_ExitsZeroWithTheFilesBytes`
  and `UpstreamCurlFetchesFromSurlOverFtpTests.Download_EachDataConnectionMode_WritesTheFile` start `surl` through
  `SurlOnLoopback`. This task repeats each with the server started by `SurlServer.StartAsync` on `127.0.0.1` port 0,
  using the bound URL it reports, and asserts what those tests assert.
- Add a project reference from `Surl.Conformance.UnitTests` to `Application/Surl.Api` (test projects may reference
  any production project).
- `[TestCategory("Integration")]` on every test; they open sockets and run a process.

## Acceptance criteria

- [ ] A test class `UpstreamCurlFetchesFromSurlApiServerTests` exists with an HTTP/1.1 `GET` test and an FTP
      `RETR` test, each passing on Windows with the pinned reference build present.
- [ ] Each test asserts what its `surl` counterpart asserts (curl's exit code 0 and the served file's bytes), and
      that the `Surl.Api` exchange record's received bytes for the exchange begin with the request curl sent:
      `GET /<file> HTTP/1.1` for HTTP, and `USER <account>` as the first command for FTP.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-11-11 (lane 4): Wrote `UpstreamCurlFetchesFromSurlApiServerTests` (HTTP `GET` and the four FTP data-connection modes, each
  started through `SurlServer.StartAsync` with `RecordExchanges` on, asserting exit code 0, the bytes, and the first
  recorded exchange's `Received` starting `GET /hello.txt HTTP/1.1` or `USER anonymous`) and the project reference to
  `Surl.Api`. Not built: `dotnet build` fails with "A compatible .NET SDK was not found" (10.0.401 pinned, not
  installed), so the work waits on BL-1035; the code is left uncommitted for the shift to stash.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: .NET SDK 10.0.401 that global.json pins is not installed, so nothing builds
