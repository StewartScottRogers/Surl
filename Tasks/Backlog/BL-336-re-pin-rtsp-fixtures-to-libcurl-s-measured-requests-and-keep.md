---
id: BL-336
title: Re-pin RTSP fixtures to libcurl's measured requests and keep a torn-down Session usable
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-315, BL-316, BL-333]
touches: [Surl.Protocol.Rtsp.UnitLibrary, Surl.Protocol.Rtsp.UnitTests]
requirement: FR-051
created: 2026-09-30
completed:
---
# BL-336 — Re-pin RTSP fixtures to libcurl's measured requests and keep a torn-down Session usable

## Goal

`Surl.Protocol.Rtsp`'s tests use the requests the pinned `libcurl-4.dll` was measured sending
(ADR-0074 Amendment 1) in place of RFC 2326 section 14's examples, and the server serves a
torn-down session's ID as Amendment 1's amended decision 5 says, so a libcurl client can
`TEARDOWN` and carry on on the same handle.

## Context

- ADR-0074 Amendment 1: the request table (byte for byte), the measured cases and the amended
  decision 5. Record the fixtures with the `Record-CurlExchange.ps1 -LibcurlRtsp` command it gives,
  on Windows, into `Surl.Protocol.Rtsp.UnitTests/Fixtures`.
- BL-313 and BL-314 used `Fixtures/rfc2326-describe` (RFC 2326's `DESCRIBE` example); BL-315 built
  decision 5 as first written (a torn-down ID answered `454`). This task replaces the one and amends
  the other.
- Amended decision 5: after `TEARDOWN` the connection remembers the ID; a request naming it is served
  as session-less (`OPTIONS`, `DESCRIBE`, `ANNOUNCE`, `GET_PARAMETER`, `SET_PARAMETER`, no `Session`
  on the answer), `PLAY`, `PAUSE`, `RECORD` and `TEARDOWN` naming it are `454`, and a `SETUP` naming
  it makes the new session under the same ID. An ID ended by timeout or never held stays `454`.

## Acceptance criteria

- [ ] `Surl.Protocol.Rtsp.UnitTests/Fixtures` holds the libcurl `DESCRIBE`, `SETUP`, `PLAY`,
      `TEARDOWN`, `ANNOUNCE` and `RECORD` requests recorded through the pinned `libcurl-4.dll`, its
      README names the command and SHA-256, and no test reads `rfc2326-describe` any more.
- [ ] A test pins that after `TEARDOWN`, `OPTIONS` naming the ended ID is `200` with no `Session`,
      `PLAY` naming it is `454`, and `SETUP` naming it is `200` with `Session: <the same ID>;timeout=60`.
- [ ] `dotnet build` is clean, the fast tests are green, and the library stays at 100% line and
      branch coverage.

## Notes

- Filed by BL-333 (lane 6, 2026-09-30) from its measurements.

## Log

- 2026-09-30: Created.
