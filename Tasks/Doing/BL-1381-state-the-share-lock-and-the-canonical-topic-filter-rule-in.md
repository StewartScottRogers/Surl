---
id: BL-1381
title: State the share lock and the canonical topic filter rule in surl --aihelp
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1379, BL-1380]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-18
completed:
---
# BL-1381 — State the share lock and the canonical topic filter rule in surl --aihelp

## Goal

`surl --aihelp content` and `surl --aihelp exit-codes` say that a writable `--share` folder is locked like a
`--directory`, and `surl --aihelp messages` says which topic filters `--topic` and `--topic-access` accept, as
BL-1379's ADR decided.

## Context

- Help text only (`Application/Surl.Cli.UnitLibrary`), after the behaviour exists: BL-1380 locks writable shares;
  BL-1379 decides the topic filter rule and gives the sentence to add (or, if it decided to refuse `#` mid-level,
  a code task it filed must be in this task's `depends-on` before it starts).
- Where: `AiHelpProse.cs:84` (the `content` topic's lock paragraph: "While it runs, surl holds a lock on
  `<path>/.surl/lock` ..."), `AiHelpProse.cs:88` (the `--share` paragraph), `ExitCodeGuidanceTable.cs:70-74` (the
  `DataDirectoryInUse` row: "Another surl holds the data directory"), and `AiHelpProse.cs:247` (the `messages`
  topic's `--topic` paragraph, which today ends "Starts exit 2 for a filter that is not a valid topic filter"
  without saying what a valid one is).
- From the Surly cookbook bug report of 2026-11-18, findings 3 and 4.
- Stand-in: no store, event or fault changes; the text tells a test author which folders two `surl` processes may
  not share and how to write a filter that AMQP and MQTT clients both reach.

## Acceptance criteria

- [ ] `surl --aihelp content` states that a share without `;readonly` takes `<path>/.surl/lock` too, so a second
      `surl` given that folder as `--directory` or as a writable share exits 124, and that a `;readonly` share takes
      no lock.
- [ ] The `DataDirectoryInUse` row of `ExitCodeGuidanceTable` names a writable share's folder beside the data
      directory, and `surl --aihelp exit-codes` shows it.
- [ ] `surl --aihelp messages` states the filter rule BL-1379's ADR decided, with one filter that is accepted and
      one that exits 2.
- [ ] A test in `Surl.Cli.UnitTests` (`AiHelpTextTests` or `AiHelpFactsTests`) pins each new sentence, and
      `dotnet test Application/Surl.Cli.UnitTests` is green.
- [ ] `dotnet build -warnaserror` is clean and `Surl.Cli.UnitLibrary` stays at 100% line and branch coverage.

## Notes

- `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` lives in
  `Surl.Console.UnitTests`; add no example, so it needs no change.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing.
