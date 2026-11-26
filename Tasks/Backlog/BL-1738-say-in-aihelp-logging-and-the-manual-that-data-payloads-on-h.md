---
id: BL-1738
title: Say in --aihelp logging and the manual that DATA payloads on HTTP/2 CONNECT streams are withheld
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1733]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1738 — Say in --aihelp logging and the manual that DATA payloads on HTTP/2 CONNECT streams are withheld

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

- [ ] The `logging` topic and the manual say which `DATA` payloads are withheld, and neither says a credential
      tunnelled on an HTTP/2 `CONNECT` stream is logged as received.
- [ ] `ManualTextTests` and `AiHelpTextTests` pin the new sentences.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
