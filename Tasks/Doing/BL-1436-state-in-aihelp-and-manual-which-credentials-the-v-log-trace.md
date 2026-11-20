---
id: BL-1436
title: State in --aihelp and --manual which credentials the -v log, trace dumps and --log-file withhold
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1420, BL-1422, BL-1423, BL-1424, BL-1425, BL-1426, BL-1427, BL-1429, BL-1430, BL-1431, BL-1432, BL-1433, BL-1434, BL-1435]
touches: [Application/Surl.Cli.UnitLibrary/AiHelpProse.cs, Application/Surl.Cli.UnitLibrary/ManualText.cs, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1436 — State in --aihelp and --manual which credentials the -v log, trace dumps and --log-file withhold

## Goal

`surl --aihelp security` (and the logging topic that describes `-v`, `--trace`, `--trace-ascii` and
`--log-file`) and `surl --manual` say which credentials surl's log withholds and how they appear, as
BL-1417's ADR decides, so a cookbook can keep a log without filtering it.

## Context

- Bug report item: the cookbook's keep-the-log recipes now filter `PASS` and `Authorization` lines out
  and say why; once the redaction tasks land, the help is where an agent learns it no longer needs to
  (root `CLAUDE.md`, "`surl --aihelp` is how an agent learns the command line").
- `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` holds the topic paragraphs and
  `Application/Surl.Cli.UnitLibrary/ManualText.cs` the manual; `ManualTextTests` in
  `Application/Surl.Cli.UnitTests` pins the manual's lines.
- If the ADR leaves the event stream raw (`Surl.Api`'s `Exchanges`, surl-mcp's `get_exchanges`), the
  text says so.

## Acceptance criteria

- [ ] The `--aihelp` paragraph that describes `-v` and the trace options states which fields are
      redacted and shows one redacted line (e.g. `PASS ****`) exactly as surl writes it.
- [ ] The security topic says the log withholds credentials and names any option that turns that off.
- [ ] `--manual`'s `-v`, `--trace` and `--trace-ascii` entries say the same; `ManualTextTests` is updated
      and green.
- [ ] `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` and every
      `AiHelpTextTests` test stay green.
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean and the fast tests are green.

## Notes

- Depends on every original-protocol redaction task, so the text names only what is built. Extended
  servers the ADR files tasks for are added to the text by those tasks.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
