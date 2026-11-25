---
id: BL-1735
title: Say in --aihelp logging and the manual that a misplaced HTTP/2 preface withholds a connection's later bytes
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1713]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1735 — Say in --aihelp logging and the manual that a misplaced HTTP/2 preface withholds a connection's later bytes

## Goal

`surl --aihelp logging` and the manual say that, on the listeners BL-1713 changed, an HTTP/2 connection preface
appearing where no request may start makes every later byte of that connection log as `****`.

## Context

- The `logging` paragraph in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` says data bodies are logged as
  received. BL-1713 added an exception. The manual's matching text is in
  `Application/Surl.Cli.UnitLibrary/ManualText.cs`, pinned by `ManualTextTests`.
- The behaviour: on a `--proxy` listen URL, and on the origin `http` and `https` listen URLs when `--proxy` is named,
  an HTTP/2 connection preface that does not start where a request may start (an upload body that holds it included) is
  followed by one `****` and nothing else the client sends. See `HttpProxyConnectionBytesRedactor`,
  `HttpProxyWithheldBytesRedactor` and the doc comment of `ProxyRoutingProtocolServer.CreateRedactor`.
- A preface that starts where a request may start is read as HTTP/2 instead, and is not the exception.
- Write what the code does when this task runs. If the separate task filed from BL-1713 that withholds `DATA` payloads
  on HTTP/2 `CONNECT` streams is done, the statement about data bodies allows for it too.
- Stand-in: help text only; a test that needs the withheld bytes reads the event stream, as the text already says.

## Acceptance criteria

- [ ] The `logging` topic in `AiHelpProse.cs` states the exception and the listen URLs it applies to.
- [ ] `ManualText.cs` says the same, and `ManualTextTests` pins it.
- [ ] `AiHelpTextTests.Answer_Logging_SaysAMisplacedHttp2PrefaceWithholdsEveryLaterByte` passes.
- [ ] Neither text still says without qualification that data bodies are logged as received.
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean and the fast tests, the `--aihelp`
      completeness tests included, are green.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
