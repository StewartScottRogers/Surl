---
id: BL-1727
title: Say in --aihelp that an HTTP chosen 401 carries the server's challenges
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1700]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1727 — Say in --aihelp that an HTTP chosen 401 carries the server's challenges

## Goal

`surl --aihelp testing` tells an agent that an HTTP chosen `401` is sent with the server's `WWW-Authenticate`
challenges.

## Context

- BL-1700 (ADR-0134 Amendment 1) made a fault rule's chosen `401` on the HTTP server carry the challenges the
  connection's own `401` would offer.
- `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`, the `testing` topic's `--fault` paragraph (about line 449),
  describes an HTTP `error` answer (status and `body=` text) and does not say this. Nothing there is false today.
- Facts to state, each checked against the code and ADR-0134 Amendment 1 as they are when this task runs:
  an `http` or `https` chosen `401` carries the challenges as `WWW-Authenticate` fields; when the server offers none
  on the connection it goes without one and the log says so; over HTTP/2 and HTTP/3, a `401` whose only challenges
  are NTLM or Negotiate is reset (`HTTP_1_1_REQUIRED`, `H3_VERSION_FALLBACK`), so the client does not receive it.
- RTSP's chosen `401` (BL-1724) and the proxy's chosen `407` (BL-1725): describe either only if its task is in `Done`
  (or its archive) and the code does it when this task runs; the proxy's `--fault` paragraph is in the `proxy`
  topic of the same file. Otherwise say nothing about them.
- A grep of `ManualText.cs` for `--fault` on 2026-11-25 found nothing, so the manual is expected to need no change.
- Stand-in: help text only; no store, event or fault behaviour changes.

## Acceptance criteria

- [ ] The `testing` topic's `--fault` text states the three facts about an HTTP chosen `401` listed in Context.
- [ ] A test `AiHelpTextTests.Answer_Testing_SaysAnHttpChosen401CarriesTheServersChallenges` passes.
- [ ] RTSP and proxy behaviour is mentioned only where the code does it, and the Log says which of BL-1724 and
      BL-1725 were done at the time.
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean and the fast tests, the `--aihelp`
      completeness tests included, are green.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
