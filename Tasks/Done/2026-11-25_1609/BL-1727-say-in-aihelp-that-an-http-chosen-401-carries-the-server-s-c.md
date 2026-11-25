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
completed: 2026-11-25
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

- [x] The `testing` topic's `--fault` text states the three facts about an HTTP chosen `401` listed in Context.
- [x] A test `AiHelpTextTests.Answer_Testing_SaysAnHttpChosen401CarriesTheServersChallenges` passes.
- [x] RTSP and proxy behaviour is mentioned only where the code does it, and the Log says which of BL-1724 and
      BL-1725 were done at the time.
- [x] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean and the fast tests, the `--aihelp`
      completeness tests included, are green.

## Notes

- Added one paragraph after the `--fault` paragraph of the `testing` topic in `AiHelpProse.cs`, rather than
  lengthening that paragraph.
- Facts checked against `HttpRequestAnswerer.Faults.cs` (`WwwAuthenticateValuesForChosenError`),
  `ConnectionBoundLogins.cs`, `Http2StreamAnswerWriter.cs`, `Http3StreamAnswerWriter.cs` and ADR-0134 Amendment 1.
  The paragraph also says one of the rule's `times` is spent on a reset `401`, as the amendment's Consequences say.
- RTSP and proxy behaviour is not mentioned: BL-1724 was in `Doing` and BL-1725 in `Backlog`. Whichever lands
  later adds its own help text if its task says so.
- `ManualText.cs` has no `--fault` text, so it is unchanged.
- Verify: solution build with `-warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests
  0 failed (`Surl.Cli.UnitTests` 1962 passed).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: BL-1724 (in Doing) and BL-1725 (in Backlog) were not done, so the help says nothing about RTSP or
  proxy challenges.
- 2026-11-25: Doing -> Done. surl --aihelp testing now says an HTTP chosen 401 carries the server's challenges, goes without when none are offered, and is reset over HTTP/2 and HTTP/3 when only NTLM or Negotiate
