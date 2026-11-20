---
id: BL-1448
title: Answer If-Modified-Since and If-Unmodified-Since on the HTTP server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1447]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Application/Surl.Cli.UnitLibrary/AiHelpProse.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-1448 — Answer If-Modified-Since and If-Unmodified-Since on the HTTP server

## Goal

`curl -z "<date>" http://127.0.0.1:<port>/a.txt` gets `304 Not Modified` when `a.txt` has not changed
since, and `curl -z "-<date>"` gets `412 Precondition Failed` when it has, exactly as BL-1447's ADR and
BL-1446's recordings say.

## Context

- Surly bug report `2026-11-19T1942-Surl-bug-report.md` item 12.
- `Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpRequestAnswerer.cs`: the `GET`/`HEAD` answer builds
  `Last-Modified` at `:310` and reads `If-Range` at `:336-341`. Preconditions are evaluated after the
  login check (ADR-0180) and before the range selection (RFC 9110 section 13.2.2).
- The same answerer serves HTTP/1.1, HTTP/2 and HTTP/3, so one change covers all three.
- `--aihelp http` describes `GET` answers (`Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`), so it
  changes in the same diff (root `CLAUDE.md`).

## Acceptance criteria

- [ ] Named fast tests in `Protocols/Http/Surl.Protocol.Http.UnitTests` cover each row of BL-1447's table
      for `If-Modified-Since` and `If-Unmodified-Since`: unchanged gives `304` with the fields the ADR
      lists and no body; changed gives `200`; an invalid date is treated as the ADR says; `HEAD` behaves
      as `GET` without a body.
- [ ] The bytes sent for BL-1446's `-z` cases equal what the ADR pins (a replay test against the
      recorded requests).
- [ ] `-z "-<date>"` against a newer file gives `412` (named test).
- [ ] The `--aihelp http` paragraph states the date conditions, and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` stays green.
- [ ] `dotnet build Protocols/Http/Surl.Protocol.Http.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T1942-Surl-bug-report.md` item 12.
