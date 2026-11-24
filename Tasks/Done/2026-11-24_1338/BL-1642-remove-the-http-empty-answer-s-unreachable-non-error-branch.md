---
id: BL-1642
title: Remove the HTTP empty answer's unreachable non-error branch and cover HTTP/2 ObserveSent
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1642 — Remove the HTTP empty answer's unreachable non-error branch and cover HTTP/2 ObserveSent

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Protocol.Http.UnitLibrary` reports 100% line and 100% branch with no failing
member, and no new coverage exclusion.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Surl.Protocol.Http 99.84% line,
  99.64% branch, 3 failing members.
- `HttpRequestAnswerer.EmptyAnswerFields(HttpStatus, string?, IReadOnlyList<string>)`,
  `Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpRequestAnswerer.cs:292`, 84.21% line, 75% branch. Lines 309-311
  (`else { fields.Add(new("Content-Length", "0")); }`) are uncovered. Line 304 `HttpErrorText.Carries(status)` is
  `status.Code >= 400` (`Surl.HttpServing.UnitLibrary/HttpErrorText.cs:22`).
- `HttpRequestAnswerer.EmptyAnswerBody(HttpRequest, HttpStatus)`, same file line 317, 50% branch: the
  `HttpAnswerBody.None` arm of line 318 is never taken.
- Both are **unreachable**. Every caller, `Refuse` (line 282) and `EmptyAnswer` (line 289), is called with 400, 401, 403,
  404, 405, 413, 501, or a fault-chosen status that `HttpCommandFaults.TryReadChosenStatus`
  (`Surl.HttpMessage.UnitLibrary/HttpCommandFaults.cs:125`) limits to 400-599. The branch dates from BL-1563 (commit
  60da60a0f) and ADR-0186.
- `HttpConnectionBytesRedactor.ObserveSent(ReadOnlySpan<byte>)`, `HttpConnectionBytesRedactor.cs:46`, 75% branch. Line
  48 (`!isHttp2 && sent.StartsWith(SwitchingProtocolsPrefix)`) is 3 of 4: `ObserveSent` is never called once the
  connection is HTTP/2. That happens with prior knowledge (`PRI *` preface first) or after an h2c upgrade's first
  received bytes. `Http2ExchangeRedactionTests.cs:70,88` call it only on HTTP/1.1.

## Acceptance criteria

- [x] `EmptyAnswerFields` and `EmptyAnswerBody` no longer branch on a status that every caller makes `>= 400`. The
      answers stay byte for byte the same: the existing ADR-0186 error-text tests and the BL-1563 body-check tests pass
      unchanged.
- [x] A named `Surl.Protocol.Http.UnitTests` test calls `ObserveSent` on an `HttpConnectionBytesRedactor` already serving
      HTTP/2 (prior-knowledge preface received first), with bytes that start `HTTP/1.1 101`. It asserts the redaction of
      the next received HTTP/2 header block is unchanged, so the bytes are not mistaken for an upgrade.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Protocol.Http.UnitLibrary` reports `100 | 100`
      with 0 failing members, without any new `[ExcludeFromCodeCoverage]`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Removed the 1xx-3xx arm from EmptyAnswerFields and EmptyAnswerBody; added ObserveSent_ConnectionAlreadyHttp2_IgnoresBytesThatStartAsAnUpgradeAnswer.
- No behaviour change on the wire. If a future caller needs a 1xx-3xx empty answer, it adds the branch back with its
  test.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. HTTP library at 100/100 coverage: dead empty-answer branch removed, HTTP/2 ObserveSent covered
