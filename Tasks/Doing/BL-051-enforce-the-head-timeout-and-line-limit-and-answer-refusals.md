---
id: BL-051
title: Enforce the head timeout and line limit and answer refusals in Surl.Protocol.Dict
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-033, BL-046]
touches: [Surl.Protocol.Dict.UnitLibrary, Surl.Protocol.Dict.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-051 — Enforce the head timeout and line limit and answer refusals in Surl.Protocol.Dict

## Goal

The DICT server (BL-033) enforces `ExchangeContext.Limits` - `420` then close when a
command line is not complete within the head timeout, `500` then close for a line over
8 KiB - implements `IConnectionRefusalWriter` with a `420`, and sends only fixed error
text and a banner with no version; every new reply is one pinned upstream curl 8.21.0
was fed and recorded.

## Context

- Specification: `Documentation/Planning/Decisions/ADR-0006-hardening-for-internet-facing-use.md`,
  sections 1 (head timeout, "Maximum command line", "Sizes"), 3 (fixed error text, no
  version in a banner) and 5 (the DICT column, and "Every hit is a graceful close"). The
  contract types come from BL-046.
- Head timeout: `HeadTimeout` on `ExchangeContext.TimeProvider`. For the first command it
  starts when `ServeAsync` starts; between commands it starts at the first byte of the
  next line, and until then only `Surl.Core`'s idle timeout applies. When it fires:
  `420`, then close.
- Line limit: `MaxLineBytes` (default 8192), counted from the first byte through the
  `CRLF` inclusive. Past it: `500`, then close, never reading past the limit. If BL-033
  chose a limit of its own, replace it with `MaxLineBytes`.
- Refusal: implement `IConnectionRefusalWriter`; both `ConnectionRefusal` values write a
  `420` reply, then the connection completes its writes. RFC 2229 section 3.1 and its
  status-code list define 420 ("Server temporarily unavailable") and 500 ("Syntax error,
  command not recognized"); the exact reply text is the server's own fixed text.
- Every refusal is written with a one-second write deadline, then writes are completed;
  never `Abort` for a limit.
- Error text and banner: every reply's text comes from the server's own table, never a
  path, exception text or OS error; the `220` banner names no surl, .NET or OS version.
- Measurement (ADR-0003): feed each new reply - the 420 refusal as the first reply, the
  420 after a timeout, and the 500 after an over-long line - to the pinned build with
  `Record-CurlExchange.ps1 -Raw` (BL-029), commit each recording under
  `Surl.Protocol.Dict.UnitTests/Fixtures/<case>/` as `EmbeddedResource` with the command
  line and build SHA-256 in its `README.md`, and record curl's exit code and stderr as
  the build reported them.
- Tests use a hand-written `TimeProvider` and BL-005's `InMemoryConnection`.

## Acceptance criteria

- [ ] `HeadTimeoutTests.PartialLine_AfterHeadTimeout_Answers420AndCloses` and
      `HeadTimeoutTests.NoCommand_AfterHeadTimeout_Answers420AndCloses` pass, and a test
      proves the wait between commands is not cut off by `HeadTimeout`.
- [ ] `LineLimitTests.LineOfExactly8192Bytes_IsAnswered` and
      `LineLimitTests.LineOf8193Bytes_Answers500AndCloses` pass, and a test proves
      `MaxLineBytes = 0` accepts a 16 KiB line.
- [ ] `ConnectionRefusalTests` prove both `ConnectionRefusal` values write the exact 420
      bytes and complete writes without `Abort`.
- [ ] A test proves the banner holds no digit-dot-digit version string, and a test with a
      failing definitions source proves the reply contains neither a path nor an
      exception message.
- [ ] Recordings for each case above are committed; each test's expected bytes equal the
      bytes that recording fed to pinned upstream curl 8.21.0.
- [ ] `dotnet build Surl.Protocol.Dict.UnitLibrary -warnaserror` is clean, the fast tests
      are green with no `Integration` test in `Surl.Protocol.Dict.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Dict.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
