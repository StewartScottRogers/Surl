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
completed: 2026-11-25
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

- [x] The `logging` topic in `AiHelpProse.cs` states the exception and the listen URLs it applies to.
- [x] `ManualText.cs` says the same, and `ManualTextTests` pins it.
- [x] `AiHelpTextTests.Answer_Logging_SaysAMisplacedHttp2PrefaceWithholdsEveryLaterByte` passes.
- [x] Neither text still says without qualification that data bodies are logged as received.
- [x] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean and the fast tests, the `--aihelp`
      completeness tests included, are green.

## Notes

- Delivery: for a two-paragraph text change the planning, test and implementation stages were done in the session
  rather than by separate agents, to save tokens; `code-reviewer` gave the independent read. Both new pins failed
  before the text changed. Conformance skipped: no byte curl sends or receives changes.
- The reviewer found that data bodies have a second exception: a line sent through a SOCKS tunnel, or a `CONNECT`
  tunnel through the HTTP proxy, that reads as carrying a secret is masked. Both texts name it.
- The texts name no curl option for the `PROXY` line example: the `NamesOnlyOptionsThatExist` tests refuse
  `--haproxy-protocol`, and their exception list was left as it is.
- BL-1733 was still in Backlog, so the text says `DATA` frames of a `CONNECT` stream, tunnelled credentials included,
  are logged as received. BL-1745 (depends on BL-1733) updates it.
- BL-1736 was in `Doing` on another lane: until it lands, an `h2c` upgrade request declaring `Content-Length: 0` also
  has the bytes after its preface withheld, although the text calls a preface after an `h2c` upgrade request read as
  HTTP/2. The text states the rule that task restores.
- Evidence: `dotnet build -warnaserror` of the library and `dotnet build` clean, format check clean, fast tests 29250
  passed, 0 failed, 3 skipped.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. --aihelp logging and the manual say when a misplaced HTTP/2 preface, or a tunnelled secret line, makes the exchange log withhold bytes of a data body
