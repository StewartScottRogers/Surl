---
id: BL-293
title: Move the HTTP request-head reader and response-head writer into Surl.HttpMessage
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-292]
touches: [Surl.HttpMessage.UnitLibrary, Surl.HttpMessage.UnitTests, Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests, Documentation/Wiki/Glossary.md]
requirement: FR-048
created: 2026-09-30
completed:
---
# BL-293 — Move the HTTP request-head reader and response-head writer into Surl.HttpMessage

## Goal

The bounded request-head reader, the request-line and field-line parsers, the response-head writer
and the challenge-field writing live in `Surl.HttpMessage.UnitLibrary`, generalised to the protocol
name and version they are given (`HTTP/1.0`, `HTTP/1.1`, `RTSP/1.0`), and `Surl.Protocol.Http`
uses them with no change in any byte it sends.

## Context

- Decision: BL-281's ADR - which types move, their public surface, and what stays in
  `Surl.Protocol.Http` (method dispatch, content responses, chunked bodies, 100-continue per
  ADR-0027, persistence). Candidates today in `Surl.Protocol.Http.UnitLibrary`:
  `HttpConnectionReader`, `HttpRequestHeadLineReader`, `HttpRequestHeadReadOutcome`,
  `HttpRequestHeadReadResult`, `HttpRequestLineParser`, `HttpFieldLineParser`, `HttpRequestField`,
  `HttpRequestHead`, `HttpResponseHead`, `HttpSyntax`, `HttpStatus`, and the `WWW-Authenticate`
  writing in `HttpRequestResponder` (lines near "One WWW-Authenticate field per value").
- A move, not a rewrite: tests move with their types into `Surl.HttpMessage.UnitTests`; new tests
  cover what generalising added (a request line with `RTSP/1.0` accepted when the caller names it,
  refused when it does not; a response head written with `RTSP/1.0`). Every existing
  `Surl.Protocol.Http.UnitTests` test keeps passing unchanged -
  that is the evidence no byte changed.
- Limits keep their meaning: `--max-request-head` and the head timeout (ADR-0006, ADR-0019) are
  enforced by the moved reader as before; `431`/`408` choices stay the HTTP server's.
- `Surl.Protocol.Http.UnitLibrary/Surl.Protocol.Http.UnitLibrary.csproj` gains the reference;
  `Surl.Protocol.Http.UnitLibrary/CLAUDE.md` names it.
- `Documentation/Wiki/Glossary.md`: the "request head", "request line" and "field line" rows' code
  column names the moved types in their new home.
- If BL-281's ADR decided against a shared library, this task goes to `Deferred` with the ADR as the
  reason.

## Acceptance criteria

- [ ] The types the ADR lists are in `Surl.HttpMessage.UnitLibrary` and no longer in
      `Surl.Protocol.Http.UnitLibrary`; `Surl.Protocol.Http` references the library.
- [ ] Every test that existed in `Surl.Protocol.Http.UnitTests` before the move passes, in its old
      project or moved beside its type; the new generalisation tests pass.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for `Surl.HttpMessage.UnitLibrary`
      and `Surl.Protocol.Http.UnitLibrary`.
- [ ] The glossary rows name the types where they now are.

## Notes

## Log

- 2026-09-30: Created.
