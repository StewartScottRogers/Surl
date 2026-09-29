---
id: BL-052
title: Enforce the head timeout and selector limit and hide directories and dot-files in Surl.Protocol.Gopher
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-034, BL-046, BL-047]
touches: [Surl.Protocol.Gopher.UnitLibrary, Surl.Protocol.Gopher.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-052 — Enforce the head timeout and selector limit and hide directories and dot-files in Surl.Protocol.Gopher

## Goal

The Gopher server (BL-034) closes with no bytes when a selector line is not complete
within the head timeout or runs past 8 KiB, answers a directory as absent unless
directory listings are enabled, and hides dot-files - all through `ExchangeContext.Limits`
and `Surl.Content`'s exposure rules - with every reply one pinned upstream curl 8.21.0
was fed and recorded.

## Context

- Specification: `Documentation/Planning/Decisions/ADR-0006-hardening-for-internet-facing-use.md`,
  sections 1 (head timeout, "Maximum command line", "Sizes"), 2 (directory listings off
  by default, dot-files hidden, "answered as absent"), 3 (fixed error text) and 5 (the
  "Gopher, TELNET, TFTP, MQTT" column: close with no bytes for both limits, and for a
  connection refusal). Contract types come from BL-046; exposure rules from BL-047.
- Head timeout: `HeadTimeout` on `ExchangeContext.TimeProvider`, starting when
  `ServeAsync` starts (Gopher has one request per connection). When it fires, complete
  writes with no bytes.
- Selector limit: `MaxLineBytes` (default 8192), from the first byte through `CRLF`
  inclusive. Past it, complete writes with no bytes, never reading past the limit.
- No `IConnectionRefusalWriter`: ADR-0006 section 5 answers a Gopher refusal with a bare
  close, which is what `Surl.Core` does for a server that does not implement it. State
  that in the server's XML doc.
- Exposure: the server asks `Surl.Content` and never re-implements the rules. With
  default options, a directory selector and a dot-file selector get exactly the reply a
  missing selector gets (BL-034's type-`3` error item), and a menu, when listings are on,
  leaves dot-files out because BL-047's listing does.
- Error text: the type-`3` item's text is the server's fixed text, never a path or
  exception message; a selector echoed back is rendered safely or not echoed.
- Measurement (ADR-0003): record with `Record-CurlExchange.ps1 -Raw` (BL-029) against
  the pinned build: a directory selector with listings off (fed the missing-selector
  reply), and a connection closed with no bytes after the selector; commit each under
  `Surl.Protocol.Gopher.UnitTests/Fixtures/<case>/` as `EmbeddedResource` with the command
  line and build SHA-256 in its `README.md`, recording curl's exit code and stderr as the
  build reported them.

## Acceptance criteria

- [ ] `HeadTimeoutTests.PartialSelector_AfterHeadTimeout_ClosesWithNoBytes` passes.
- [ ] `SelectorLimitTests.SelectorOfExactly8192Bytes_IsAnswered` and
      `SelectorLimitTests.SelectorOf8193Bytes_ClosesWithNoBytes` pass.
- [ ] `ExposureTests.DirectorySelector_WithListingsOff_AnswersExactlyAsMissing` and
      `ExposureTests.DotFileSelector_AnswersExactlyAsMissing` compare the reply bytes with
      a missing selector's byte for byte.
- [ ] `ExposureTests.DirectoryMenu_WithListingsOn_OmitsDotFiles` passes.
- [ ] Recordings for the cases above are committed; each test's expected bytes equal the
      bytes that recording fed to pinned upstream curl 8.21.0.
- [ ] `dotnet build Surl.Protocol.Gopher.UnitLibrary -warnaserror` is clean, the fast
      tests are green with no `Integration` test in `Surl.Protocol.Gopher.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Gopher.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
