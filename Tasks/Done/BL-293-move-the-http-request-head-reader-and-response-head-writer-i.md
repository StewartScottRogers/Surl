---
id: BL-293
title: Move the HTTP request-head reader and response-head writer into Surl.HttpMessage
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-292]
touches: [Surl.HttpMessage.UnitLibrary, Surl.HttpMessage.UnitTests, Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests, Documentation/Wiki/Glossary.md, Documentation/Product/Product-Overview.md]
requirement: FR-048
created: 2026-09-30
completed: 2026-09-30
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

- [x] The types the ADR lists are in `Surl.HttpMessage.UnitLibrary` and no longer in
      `Surl.Protocol.Http.UnitLibrary`; `Surl.Protocol.Http` references the library.
- [x] Every test that existed in `Surl.Protocol.Http.UnitTests` before the move passes, in its old
      project or moved beside its type; the new generalisation tests pass.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for `Surl.HttpMessage.UnitLibrary`
      and `Surl.Protocol.Http.UnitLibrary`.
- [x] The glossary rows name the types where they now are.

## Notes

- Plan: ADR-0070 decision 2 is the plan, type by type, so no separate architect stage was run;
  the move was made directly and verified by the gates below.
- Moved (with `git mv`, namespace `Surl.HttpMessage`): `HttpConnectionReader`,
  `HttpRequestHeadLineReader`, `HttpRequestLineParser`, `HttpFieldLineParser`, `HttpSyntax`,
  `HttpRequestField`, `HttpRequestHead`, `HttpRequestHeadReadOutcome`, `HttpRequestHeadReadResult`,
  `HttpStatus`, `HttpResponseHead`, `HttpRequestBodyFraming`, `HttpRequestBodyFramingKind`.
  `HttpStatus`, `HttpResponseHead` and the body-framing pair became public, as the ADR says. New:
  `HttpMessageProtocol` (`Http11`, `Rtsp10`). `HttpProtocolServer.ReadNextHeadAsync` and its timer
  clamp moved to `HttpConnectionReader.ReadNextRequestHeadAsync`; the `WWW-Authenticate` loop in
  `HttpRequestResponder` is now `HttpResponseHead.AddChallengeFields`.
- Choice: `HttpConnectionReader`, `HttpRequestHead` and `HttpResponseHead` keep their old
  constructors, which mean `HttpMessageProtocol.Http11`, beside the new ones that take a protocol.
  Why: the HTTP server's code and every existing test stay unchanged (the ADR's evidence that no
  byte changed), and the WebSocket and RTSP servers still name their protocol explicitly.
- Choice: the internal `HttpRequestHeadLineReader` and `HttpRequestLineParser` take the protocol
  with no default, so their two moved test files gained `HttpMessageProtocol.Http11` in each call;
  no assertion changed.
- Choice: `Surl.HttpMessage.UnitTests` embeds the seven request-head fixtures its reader tests use
  by link from `Surl.Protocol.Http.UnitTests/Fixtures/`, rather than copying them, because
  `HttpProtocolServerTests` uses six of the same cases; the bytes are recorded once.
  `Surl.Protocol.Http.UnitTests` gets `Surl.HttpMessage` as a global using in its csproj, so none of
  its test files changed.
- Added `Documentation/Product/Product-Overview.md` to `touches`: its "Layers" and "Project layout"
  rows still called the library "to be created"; no task in Doing names it.
- Results: `Surl.HttpMessage.UnitTests` 189 passed (the moved tests plus 32 new test cases: RTSP accepted
  when given `Rtsp10`, refused when given `Http11` and vice versa, an `RTSP/1.0` status line,
  `AddChallengeFields`, and the head-timeout cases of `ReadNextRequestHeadAsync`);
  `Surl.Protocol.Http.UnitTests` 199 passed, unchanged. Measure-CodeQuality: both libraries 100%
  line and branch, worst CRAP 10, 0 failing members.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.HttpMessage holds the HTTP/1.x head reader, head timeout, parsers and response-head writer for HTTP/1.x and RTSP/1.0; Surl.Protocol.Http uses it with every byte unchanged
