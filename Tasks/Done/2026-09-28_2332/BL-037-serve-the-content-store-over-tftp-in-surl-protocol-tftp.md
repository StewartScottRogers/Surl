---
id: BL-037
title: Serve the content store over TFTP in Surl.Protocol.Tftp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-009, BL-030]
touches: [Surl.Protocol.Tftp.UnitLibrary, Surl.Protocol.Tftp.UnitTests, Documentation/Planning/Decisions/ADR-0013-how-the-tftp-server-answers.md, Documentation/Planning/Decisions/README.md, Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-037 — Serve the content store over TFTP in Surl.Protocol.Tftp

## Goal

`Surl.Protocol.Tftp.UnitLibrary` contains a TFTP protocol server for the `tftp` scheme,
working on the datagram-flow type. It serves reads from the content store, with the
option negotiation the pinned upstream curl 8.21.0 build performs, proven by datagram
scripts recorded from that build.

## Context

- TFTP is RFC 1350, with options in RFC 2347, `blksize` in RFC 2348 and `tsize` in
  RFC 2349, and the reply from a new transfer-identifier port in RFC 1350 section 4.
  What upstream curl sends by default, with `--tftp-blksize 1024` and with
  `--tftp-no-options`, is measured with `Record-CurlExchange.ps1 -Tftp` (BL-030) against
  the pinned build, never assumed.
- Files come from the content store (BL-008, BL-009). Add a `ProjectReference` to
  `Surl.Content.UnitLibrary`, which ADR-0002 allows. A missing or refused file gets the
  RFC 1350 ERROR the plan decides (code 1, file not found, or code 2, access violation),
  stated in the XML doc.
- Writes (`curl -T`): uploads are a hardening question (BL-024 decides whether they
  default to off). This task answers a write request with the TFTP ERROR the plan
  decides, and files a follow-up for writes.
- Retransmission and timeouts use the exchange context's `TimeProvider`, never
  `Thread.Sleep`. The plan picks the timeout and retry count and states them.
- The server works on the datagram-flow type from the listener-seam ADR (BL-000,
  BL-005). Tests drive it with an in-memory datagram flow. If BL-005 did not provide
  one, write a hand-written fake in `Surl.Protocol.Tftp.UnitTests`.
- Fixtures: as in BL-017, under `Surl.Protocol.Tftp.UnitTests/Fixtures/<case>/`,
  embedded, with a `README.md`.

## Acceptance criteria

- [x] Recordings exist for a default read, a read with `--tftp-blksize 1024`, a read with
      `--tftp-no-options`, a missing file, and a file of exactly 512 bytes (which ends
      with an empty final DATA packet, RFC 1350 section 6). The pinned build accepts
      Surl's intended datagrams in each, with exit code and stdout recorded.
- [x] Fast tests replay each recording's datagrams and assert Surl's datagrams equal the
      accepted ones, including an OACK when options were requested.
- [x] Fast tests cover a lost ACK (retransmission after the timeout on a hand-written
      `TimeProvider`), giving up after the retry count, a duplicate ACK, and a write
      request refused.
- [x] `ProtocolIsolationTests` pass with the `Surl.Content.UnitLibrary` reference.
      `dotnet build Surl.Protocol.Tftp.UnitLibrary -warnaserror` is clean, the fast tests
      are green with no `Integration` test in `Surl.Protocol.Tftp.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Tftp.UnitLibrary`.

## Notes

Wiring `tftp` into `surl` and the live conformance run are BL-043.

- Decisions are in ADR-0013 (decided by Claude under Stewart's delegation): options
  `blksize` (8 up, capped 65464), `timeout` (1-255 s) and `tsize` (file length) accepted
  into an OACK in the client's order; `octet` and `netascii` both send bytes unchanged,
  because pinned curl writes DATA bytes unconverted in either mode (measured);
  retransmission timeout = negotiated `timeout`, else 5 s, 5 retransmissions then silent
  abandonment; duplicate ACKs ignored; ERROR 1 for missing, refused, directory and
  vanished; ERROR 2 for WRQ; ERROR 4 for a malformed request or an illegal packet
  mid-transfer; ERROR 0 when the file shrinks mid-send.
- Six recordings under `Surl.Protocol.Tftp.UnitTests/Fixtures/` (the five named, plus
  `write-refused`, exit 69). Tests replay `transcript.txt` both ways.
- BL-005 provided no in-memory datagram flow, so the tests use a hand-written
  `ScriptedDatagramFlow` with a `ManualTimeProvider`.
- Touches widened (no task in Doing names them): ADR-0013 and the Decisions README index
  record the decisions; `Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md` said the
  in-memory datagram flow was "still to come" in BL-037, which is no longer true.
- Write support is already filed as BL-054; no new follow-up was needed.
- `Measure-CodeQuality.ps1 -Library Surl.Protocol.Tftp.UnitLibrary`: 100% line, 100%
  branch, 35 members, 0 failing, worst CRAP 10. 74 TFTP tests.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Surl.Protocol.Tftp serves content-store reads with OACK negotiation, retransmission and ERROR answers, replayed from six pinned-curl recordings
