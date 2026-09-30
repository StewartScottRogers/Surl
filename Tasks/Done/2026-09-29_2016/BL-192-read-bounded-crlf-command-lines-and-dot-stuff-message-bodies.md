---
id: BL-192
title: Read bounded CRLF command lines and dot-stuff message bodies in Surl.LineProtocol
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-189]
touches: [Surl.LineProtocol.UnitLibrary, Surl.LineProtocol.UnitTests]
requirement: FR-043
created: 2026-09-29
completed: 2026-09-29
---
# BL-192 — Read bounded CRLF command lines and dot-stuff message bodies in Surl.LineProtocol

## Goal

`Surl.LineProtocol` gives the SMTP, IMAP and POP3 servers the line machinery BL-184's ADR
decides: a reader of CRLF command lines from an `IConnection` bounded by `--max-line` and the
head timeout, a dot-unstuffing body reader bounded by `--max-filesize`, a dot-stuffing writer,
the discard of bytes read past a `STARTTLS`/`STLS` line, and base64 SASL continuation lines.

## Context

- Decisions: BL-184's ADR (what the library holds, bare-LF policy, outcomes); ADR-0006
  sections 1 and 5 (never read more than the limit; a too-long line is an outcome the server
  answers in its own words, not an exception); ADR-0010 (bytes read beyond the end of the
  upgrade command line are discarded before `UpgradeToTlsAsync` and never run as commands);
  RFC 5321 section 4.5.2 and RFC 1939 section 3 (dot-stuffing; the body ends at `CRLF.CRLF`);
  RFC 4954 section 4 and RFC 3501 section 6.2.2 (base64 continuations, `*` cancels).
- Everything reads through `IConnection.ReadAsync` with the exchange's cancellation and
  `ExchangeLimits`; no socket, no disk (ADR-0004). Tests use `InMemoryConnection`, splitting
  input at every byte boundary that matters (inside CRLF, inside `.CRLF`).
- Existing line readers to learn from (never to reference - protocol servers are not referenced):
  `Surl.Protocol.Dict.UnitLibrary` and `Surl.Protocol.Gopher.UnitLibrary`.

## Acceptance criteria

- [x] Fast tests cover: a line split across reads; a line exactly at and one byte past
      `MaxLineBytes` (the second an outcome, with no byte read past the limit); a bare LF as the
      ADR says; the head timeout on a fake `TimeProvider`; a body with dot-stuffed lines and the
      terminator split across reads; a body past `--max-filesize`; the stuffing writer on a line
      starting with `.`; bytes pipelined after a `STARTTLS` line discarded; a base64 continuation,
      an invalid one and `*`.
- [x] `Surl.LineProtocol.UnitLibrary.csproj` references only `Surl.Protocol.Abstractions`;
      `ProtocolIsolationTests` pass.
- [x] `dotnet build Surl.LineProtocol.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.LineProtocol.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- Built to ADR-0050 decision 8, following `DictLineReader`'s buffer and head-timeout shape
  (copied, not referenced). Public surface: `CrlfLineReader` (`ReadLineAsync`,
  `ReadCountedRunAsync`, `ReadDotStuffedBodyAsync`, `ReadSaslContinuationAsync`,
  `DiscardBuffered`), `DotStuffedBodyWriter`, `ReplyLineWriter`, `SaslContinuationLine`, and
  the `CrlfLineReadOutcome`, `DotStuffedBodyReadOutcome` and `SaslContinuationOutcome` values.
- Named the reader `CrlfLineReader`, not "command-line reader", so it cannot be taken for
  Surl's own command-line parsing, and because CRLF is the only line end it accepts.
- Lines are returned as bytes, not strings: the ADR leaves a bare CR/LF and 8-bit bytes to
  each server's parser, so decoding is theirs.
- Counted runs and bodies read outside the head timeout's clock (the engine's idle timeout
  and maximum duration still cancel them); the head timeout bounds lines only, as ADR-0006
  section 1 bounds a head.
- `BodyTooLarge` writes nothing of the piece that would cross `MaxUploadBytes`; the server
  discards the pending body anyway. `MaxUploadBytes` of 0 means no limit (ADR-0006).
- Default taken: an empty message is written by `DotStuffedBodyWriter` as `.` CRLF alone -
  the ADR's "adds a CRLF when the bytes do not end with one" read as applying to a non-empty
  message, since RFC 1939 section 3 makes a zero-line response the terminator alone and an
  added CRLF would give the client one empty line that is not in the message. BL-205 measures
  pinned upstream curl on it when POP3 lands.
- `SaslContinuationLine.Classify` refuses space, tab, CR and LF before decoding, because
  `Convert.TryFromBase64String` would silently skip them.
- Quality: 72 tests, `Measure-CodeQuality.ps1 -Library Surl.LineProtocol.UnitLibrary` 100%
  line, 100% branch, 0 failing members (`DotUnstuffer.Step` was split to keep complexity
  under 10).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Surl.LineProtocol reads bounded CRLF lines, counted runs, dot-stuffed bodies and SASL continuations, and writes dot-stuffed bodies and reply lines, at 100% coverage
