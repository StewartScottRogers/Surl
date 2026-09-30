---
id: BL-139
title: Generate the --aihelp Markdown overview, topics, all and unknown-topic answer in Surl.Cli
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-138]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: FR-035
created: 2026-09-29
completed: 2026-09-29
---
# BL-139 — Generate the --aihelp Markdown overview, topics, all and unknown-topic answer in Surl.Cli

## Goal

`Surl.Cli.UnitLibrary` has the `--aihelp` generator BL-137's ADR names (for example
`AiHelpText.Answer(string? topic)` returning `HelpAnswer`), producing the overview, every
topic page, `all` and the unknown-topic answer in the ADR's one Markdown structure, with the
option tables and the `exit-codes` topic generated from BL-138's facts. Nothing calls it
from the command line yet (BL-141).

## Context

- Decision: BL-137's ADR (expected ADR-0046), decisions 3 (topics and matching), 4 (the
  Markdown structure), 5 (table columns), 8 (unknown topic) and 9 (completeness tests).
- Sources, all in `Surl.Cli.UnitLibrary`: `CommandLineOptions.All`, `HelpCategories.All`,
  BL-138's option facts and exit-code guidance table. `HelpText.cs` is the model for how
  `--help` builds its pages from the same table; share, do not copy, anything both need
  (for example the option's left side). `HelpAnswer` (`HelpAnswer.cs`) is the result type
  unless the ADR decides another.
- Each hand-written section (the topic's prose and examples) is written by BL-140. Until
  then each such section holds exactly the text the ADR prescribes for a section with nothing
  in it, so the structure is already final and identical on every topic.
- Markdown only: no ANSI escape, `Environment.NewLine` after every line, no trailing space,
  table cells escaped as the ADR says, so a value written with `|` (such as
  `none|error|info|verbose|trace`) or a backtick never splits or breaks a table row.

## Acceptance criteria

- [x] The generator named by the ADR exists and answers: no or empty topic (the overview),
      every topic the ADR lists, `all`, and an unknown topic, each exactly as the ADR says
      (stream, text, `SurlExitCode.Ok`).
- [x] Tests in `Surl.Cli.UnitTests` (in a new `AiHelpTextTests.cs` or the file the ADR names)
      fail when: an option in `CommandLineOptions.All` is missing from `all` or from any topic
      its categories name; a topic the ADR lists (every `HelpCategories.All` name plus
      `exit-codes` and the ADR's extra topics) is missing from the overview's topic list or
      from `all`; a `SurlExitCode` member is missing from the `exit-codes` topic.
- [x] A test asserts every topic page has the ADR's heading skeleton - the same `##`
      headings in the same order - by comparing the heading lines of every page with each
      other and with the ADR's list.
- [x] A test asserts no output contains `\u001b`, a TAB, or a line with a trailing space, and
      every table row of every page has the same number of cells as its header.
- [x] Tests pin the full output of the `exit-codes` topic and of one protocol topic
      (`telnet`, the shortest) byte for byte, built with `Environment.NewLine`.
- [x] Topic matching follows the ADR (case-insensitive as ADR-0034 decision 3 point 3, unless
      the ADR says otherwise), pinned by a test.
- [x] `dotnet build Surl.Cli.UnitLibrary -warnaserror` is clean (every method at cyclomatic
      complexity 10 or less); `dotnet test Surl.Cli.UnitTests --filter
      "TestCategory!=Integration"` passes; `Surl.Cli.UnitLibrary` stays at 100% line and
      branch coverage; the `--help` pages in `HelpTextTests` are unchanged.

## Notes

- Delivered in the session against ADR-0046 as written: its decisions 3 to 9 are already the
  plan (types, file names, table columns, section skeleton), so no separate plan stage was run.
- New: `AiHelpTopic.cs`, `AiHelpTopics.cs` (help categories plus `exit-codes` and
  `listen-urls`, ordinal order, `TryFind` case-insensitive), `AiHelpText.cs`
  (`Answer(string?)` returning `HelpAnswer`, `Error` always empty). `HelpText.LeftSide` went
  from private to internal so both help systems write one left side (shared, not copied).
- Choice: `AiHelpText.Row` is internal so a test can pin the `|` -> `\|` cell escape with
  `none|error|info|verbose|trace`; no fact holds a `|` today (BL-138's facts test), so no
  generated page could reach it otherwise.
- Choice: every hand-written section (overview `About`, `Command line`, `Conventions`,
  `Examples`; each topic's `About` and `Examples`) is `Nothing for this topic.` until BL-140,
  as ADR-0046's consequences for BL-139 say.
- Tests: `AiHelpTextTests.cs`; Surl.Cli.UnitTests 612 pass; whole-solution fast run
  green; `Surl.Cli.UnitLibrary` 100% line and branch, worst CRAP 10. `HelpTextTests` untouched.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. AiHelpText.Answer writes the --aihelp overview, all sixteen topic pages, all and the unknown-topic answer in ADR-0046's Markdown structure, tables generated from the option and exit-code facts
