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
completed: 2026-09-30
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

- [x] Tests in `Surl.Protocol.Rtsp.UnitTests` run a `SETUP`, `PLAY`, `PAUSE`, `PLAY`, `TEARDOWN`
      sequence over `InMemoryConnection` and check each response, the same `Session` value on each,
      and the interleaved frames `PLAY` sends; an unknown session, an unaccepted transport, a request
      invalid in the state, the session bound and the session timeout each answer as the ADR says; no
      test opens a socket.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Rtsp.UnitLibrary`.

## Notes

**Built** (ADR-0074 decision 5): `RtspSession`, `RtspTransport`, `RtspInterleavedFrame`, and
`RtspRequestResponder.Sessions.cs`; `RtspProtocolServer` takes an injected `RandomNumberGenerator`
(a new three-argument constructor; the two-argument one uses the system generator, as POP3's does)
and, before reading each head, lets a playing session stream until a request's first byte arrives
(`HttpConnectionReader.WaitForBytesAsync` left pending between frames). Tests:
`RtspSessionTests` (server-level) and `RtspTransportTests`; the RTSP project now has 163
tests, coverage 100% line and branch, 0 failing members, worst CRAP 10.

**The sequence test's connection.** `InMemoryConnection` hands over every queued request at
once, so a `PAUSE` queued behind `PLAY` always arrives before the first frame and nothing would
be streamed. The `SETUP`-`PLAY`-`PAUSE`-`PLAY`-`TEARDOWN` test therefore uses `ScriptedConnection`,
an in-memory `IConnection` in the test project that releases each request once the server's
output reaches a chosen point (after the first frame, after the `PAUSE` answer, after the RTCP
report); `InMemoryConnection` itself drives the whole-file `PLAY`, re-`SETUP`, shrink and
read-failure tests. No test opens a socket.

**Choices where decision 5 is silent** (decided by Claude under Stewart's delegation; the ADR
folder was held by BL-322, so BL-338 folds these into ADR-0074):

- A client that half-closes while its session plays is streamed the rest of the presentation, and
  the connection then closes: a half-close says no more requests, not "stop reading".
- `SETUP` naming the held session with another presentation's URL is `455`: one session holds
  one presentation (decision 4's single stream).
- `PLAY` while playing answers `200` with the next packet's `RTP-Info` and carries on.
- Every answer to a request that named the live session names it back, refusals (`455`, `457`,
  `404`, `451`) included; `454` names none.
- The 60-second timeout is checked when a request names the session (and before a `SETUP` that
  names none), which is what a client can observe; the end is noted `ended: timeout` then.
- The sender report's RTP timestamp is the last packet's; its packet and octet counts are the
  session's since `SETUP`, across every `PLAY`.
- A file that shrinks while playing, or can no longer be read, ends with an empty marked packet
  where its bytes ran out; one gone before `PLAY` is `404`.
- Until BL-316, `SETUP` for `mode=record` is `501 Not Implemented` (after the write's login), so
  the `Transport` answer never carries `;mode=record` yet.
- Client `$` frames (decision 6's discard of frames outside a recording) are BL-316's.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. RTSP SETUP, PLAY (interleaved RTP and RTCP), PAUSE, TEARDOWN, GET_PARAMETER and SET_PARAMETER answer with one session per connection, its timeout and ADR-0074's 454/455/457/461
