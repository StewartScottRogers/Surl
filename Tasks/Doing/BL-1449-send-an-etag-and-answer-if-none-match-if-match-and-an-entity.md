---
id: BL-1449
title: Send an ETag and answer If-None-Match, If-Match and an entity-tag If-Range on the HTTP server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1448]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Application/Surl.Cli.UnitLibrary/AiHelpProse.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-1449 — Send an ETag and answer If-None-Match, If-Match and an entity-tag If-Range on the HTTP server

## Goal

The HTTP server sends the `ETag` BL-1447's ADR fixes. `curl --etag-save e.txt` then saves it,
`curl --etag-compare e.txt` gets `304` while the file is unchanged, and an entity-tag `If-Range` returns
the range when it matches.

## Context

- Surly bug report `2026-11-19T1942-Surl-bug-report.md` item 12.
- `Protocols/Http/Surl.Protocol.Http.UnitLibrary/HttpRequestAnswerer.cs:336-341` today sends no `ETag`,
  so an entity-tag `If-Range` never matches (ADR-0133, which BL-1447 amends).
- Builds on BL-1448's precondition evaluation in the same answerer; RFC 9110 section 13.2.2 puts
  `If-Match`/`If-None-Match` before the date conditions, and the ADR's table is the order.
- If BL-1447 makes HTTP share `ObjectMetadataIndex`'s ETags, it files a `Surl.Content` task first and
  adds it to this task's `depends-on`.
- `--aihelp http` changes in the same diff (`Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`).

## Acceptance criteria

- [ ] Every `200`, `206` and `304` for a file carries the ADR's `ETag`; named tests show it is stable for
      an unchanged file and changes after a write through the content store.
- [ ] Named fast tests in `Protocols/Http/Surl.Protocol.Http.UnitTests` cover the ADR's rows for
      `If-None-Match` (match gives `304`, `*`, a list, weak comparison) and `If-Match` (a mismatch gives
      `412`), each with the fields and body the ADR lists.
- [ ] An entity-tag `If-Range` that matches returns `206`, and one that does not returns the whole file
      `200` (named tests).
- [ ] BL-1446's `--etag-compare` and `--etag-save` recordings replay green against the server's answer
      bytes.
- [ ] The `--aihelp http` paragraph states the ETag rules;
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` stays green.
- [ ] `dotnet build Protocols/Http/Surl.Protocol.Http.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T1942-Surl-bug-report.md` item 12.
- 2026-11-20: Backlog -> Doing.
