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
completed:
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

- [ ] Fast tests cover: a line split across reads; a line exactly at and one byte past
      `MaxLineBytes` (the second an outcome, with no byte read past the limit); a bare LF as the
      ADR says; the head timeout on a fake `TimeProvider`; a body with dot-stuffed lines and the
      terminator split across reads; a body past `--max-filesize`; the stuffing writer on a line
      starting with `.`; bytes pipelined after a `STARTTLS` line discarded; a base64 continuation,
      an invalid one and `*`.
- [ ] `Surl.LineProtocol.UnitLibrary.csproj` references only `Surl.Protocol.Abstractions`;
      `ProtocolIsolationTests` pass.
- [ ] `dotnet build Surl.LineProtocol.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.LineProtocol.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
