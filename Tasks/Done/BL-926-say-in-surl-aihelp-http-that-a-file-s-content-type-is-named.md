---
id: BL-926
title: Say in surl --aihelp http that a file's Content-Type is named from its extension
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-686]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-11
---
# BL-926 — Say in surl --aihelp http that a file's Content-Type is named from its extension

## Goal

`surl --aihelp http` says a served file's `Content-Type` is the media type its extension names
(ADR-0142), else `application/octet-stream`, instead of saying every file is `application/octet-stream`.

## Context

- BL-686 made `HttpRequestAnswerer` answer `Content-Type` from `Surl.Content`'s `ContentMediaTypes.Of`
  (ADR-0142). `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` (the HTTP topic's first paragraph) still says
  "A file is answered 200 OK with `Content-Type: application/octet-stream`", which is now false.
- BL-686 could not change it: `Surl.Cli.UnitLibrary` was held by BL-560 on another lane.

## Acceptance criteria

- [x] `AiHelpProse.TopicAbout` for `http` says a file's `Content-Type` is named from its extension
      (ADR-0142), else `application/octet-stream`, and no longer says every file is `application/octet-stream`.
- [x] `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` and the other
      `--aihelp` completeness tests pass.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- The paragraph now names two examples from ADR-0142's table (`.html`, `.png`) so an agent sees the rule at work; no `AiHelpExamples` entry or `ManualText` section mentioned octet-stream, so nothing else changed. Build clean with -warnaserror; fast tests green (Surl.Cli.UnitTests 1393 passed, Surl.Console.UnitTests 460 passed).

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. surl --aihelp http says a file's Content-Type is named from its extension (ADR-0142), else application/octet-stream
