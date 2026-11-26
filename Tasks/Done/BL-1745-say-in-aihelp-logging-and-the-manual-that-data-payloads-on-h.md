---
id: BL-1745
title: Say in --aihelp logging and the manual that DATA payloads on HTTP/2 CONNECT streams are withheld
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1733]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1745 — Say in --aihelp logging and the manual that DATA payloads on HTTP/2 CONNECT streams are withheld

## Goal

`surl --aihelp logging` and the manual say what BL-1733 made the exchange log write for the `DATA` frames of an HTTP/2
`CONNECT` stream.

## Context

- BL-1735 wrote the text as the code was before BL-1733: the paragraph in
  `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` (`logging`) and the matching one in `ManualText.cs` say those frames,
  "any credential tunnelled in them included", are logged as received. BL-1733 makes that false.
- The text is pinned by `AiHelpTextTests.Answer_Logging_SaysAMisplacedHttp2PrefaceWithholdsEveryLaterByte` and
  `ManualTextTests`.
- Write what the code does when this task runs (ADR-0182 and its amendments): BL-1733's rule also covers every `DATA`
  frame once the HPACK decoder copy is lost, and every server that uses `Http2RequestBytesRedactor`.
- Stand-in: help text only; a test that needs the withheld bytes reads the event stream, as the text already says.

## Acceptance criteria

- [x] The `logging` topic and the manual say which `DATA` payloads are withheld, and neither says a credential
      tunnelled on an HTTP/2 `CONNECT` stream is logged as received.
- [x] `ManualTextTests` and `AiHelpTextTests` pin the new sentences.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Text written from `Http2RequestBytesRedactor` and ADR-0182 amendment 11, as a paragraph of its own after the preface
  paragraph: it holds for every server that reads a client's bytes as HTTP/2 for the log (HTTP and HTTPS, the HTTP
  proxy, gRPC, DNS over HTTPS, the SOCKS tunnel), not only for `--proxy` listen URLs.
- The "data bodies are logged as received, with two exceptions" sentence now counts three, since withheld `DATA`
  payloads are data bodies.
- New pin: `AiHelpTextTests.Answer_Logging_SaysWhichHttp2DataPayloadsAreWithheld` (seen failing before the text).
- The SOCKS tunnel paragraph is left as it was: updating its withheld-secret lists is BL-1723.
- Verified 2026-11-25: build `-warnaserror` 0 warnings, format check exit 0, fast tests 29421 passed, 0 failed,
  3 skipped. Text only; no new code branches.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. --aihelp logging and the manual say which HTTP/2 DATA payloads the exchange log withholds
