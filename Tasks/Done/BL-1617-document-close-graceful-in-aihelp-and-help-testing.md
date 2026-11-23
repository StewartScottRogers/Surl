---
id: BL-1617
title: Document close=graceful in --aihelp and --help testing
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1608, BL-1612]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: FR-062
created: 2026-11-22
completed: 2026-11-22
---
# BL-1617 — Document close=graceful in --aihelp and --help testing

## Goal

--aihelp testing and --help testing describe close.

## Context

From BL-1608; decided in ADR-0188 (Documentation/Planning/Decisions). ADR-0188 decision 4, Help. Files: Application/Surl.Cli.UnitLibrary AiHelpProse.cs, AiHelpExamples, the help text, and the AiHelp tests that pin them.

## Acceptance criteria

- [x] The key table lists `close` with `reset` (default) and `graceful`, applying to `drop` with `after-bytes`.
- [x] An example `drop;after-bytes=4000000;close=graceful` exists and `RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` passes.
- [x] Documentation/Wiki/Surl-As-A-Stand-In.md says the same.

## Notes

Example uses `--fault` so it also pins the one-rule start warning. Help prose, --help explanation, example and wiki updated; tests re-pinned.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Done. close=graceful documented in --aihelp, --help testing and the stand-in wiki, with an example
