---
id: BL-826
title: Say in --aihelp http and ADR-0090 that HTTP/2 resets NTLM and Negotiate with HTTP_1_1_REQUIRED
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-602]
touches: [Application/Surl.Cli.UnitLibrary, Documentation/Planning/Decisions/ADR-0090-how-the-http-2-prior-knowledge-connection-reads-ends-and-answers.md, Documentation/Product/Requirements.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-826 — Say in --aihelp http and ADR-0090 that HTTP/2 resets NTLM and Negotiate with HTTP_1_1_REQUIRED

## Goal

`surl --aihelp http` and ADR-0090 say what BL-602 made true: an HTTP/2 stream carrying NTLM or Negotiate, or answered only with their challenges, is reset `HTTP_1_1_REQUIRED`.

## Context

- BL-602 added the reset to `Http2StreamAnswerWriter` (`Protocols/Http/Surl.Protocol.Http.UnitLibrary`). It could not edit these files: `Surl.Cli.UnitLibrary` was held by BL-653 and `Documentation/Planning/Decisions` by BL-569 at the time.
- `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`, the `http` topic: its last sentence "NTLM and Negotiate logins over HTTP/2 are not refused with `HTTP_1_1_REQUIRED` yet." is now false.
- ADR-0090 decision 6 says NTLM and Negotiate "are not yet answered `HTTP_1_1_REQUIRED` (decision 7; BL-602)"; add a dated note that BL-602 built it (ADR-0081 decision 7 is unchanged).

## Acceptance criteria

- [ ] `AiHelpProse.cs`'s `http` topic says an HTTP/2 stream whose `authorization` names NTLM or Negotiate, or whose `401` would offer only them, is reset `HTTP_1_1_REQUIRED` and curl retries over HTTP/1.1; the "not refused ... yet" sentence is gone.
- [ ] ADR-0090 decision 6 records that BL-602 built the `HTTP_1_1_REQUIRED` reset.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-05: Created.
