---
id: BL-315
title: Answer RTSP SETUP, PLAY, PAUSE, TEARDOWN and parameters with sessions and interleaved RTP in Surl.Protocol.Rtsp
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-314]
touches: [Surl.Protocol.Rtsp.UnitLibrary, Surl.Protocol.Rtsp.UnitTests]
requirement: FR-051
created: 2026-09-30
completed:
---
# BL-315 — Answer RTSP SETUP, PLAY, PAUSE, TEARDOWN and parameters with sessions and interleaved RTP in Surl.Protocol.Rtsp

## Goal

`RtspProtocolServer` answers `SETUP` (creating a session for the `Transport` BL-286's ADR accepts),
`PLAY` (streaming the ADR's media as interleaved RTP over the RTSP connection), `PAUSE`, `TEARDOWN`,
`GET_PARAMETER` and `SET_PARAMETER`, with session IDs, timeouts and bounds as the ADR decides.

## Context

- Decisions: BL-286's ADR (the accepted transports and `461` for the rest, session ID generation from
  an injected random source, the session timeout from the injected `TimeProvider`, the bound on
  sessions, what `PLAY` streams and at what pace, the `Range` and `RTP-Info` fields, parameter
  bodies, `454` for an unknown session, `455` for a method not valid in the state).
- Interleaved framing: RFC 2326 section 10.12 (`$`, one-byte channel, two-byte length, the RTP
  packet); RTP packets per RFC 3550 section 5.1 as the ADR describes. upstream curl compares every
  `Session` field it receives with the one it holds and fails with `CURLE_RTSP_SESSION_ERROR` (86) on
  a change (`lib/rtsp.c` at tag `curl-8_21_0`).
- Code: BL-313's server and BL-314's authentication; time through `TimeProvider`, never
  `Thread.Sleep`; ADR-0059 tells a limit from shutdown.
- Fixtures: the libcurl cases if BL-285's or BL-286's ADR pinned `libcurl-4.dll` and they were
  recorded by then; otherwise RFC 2326 section 14's examples.

## Acceptance criteria

- [ ] Tests in `Surl.Protocol.Rtsp.UnitTests` run a `SETUP`, `PLAY`, `PAUSE`, `PLAY`, `TEARDOWN`
      sequence over `InMemoryConnection` and check each response, the same `Session` value on each,
      and the interleaved frames `PLAY` sends; an unknown session, an unaccepted transport, a request
      invalid in the state, the session bound and the session timeout each answer as the ADR says; no
      test opens a socket.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Rtsp.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
