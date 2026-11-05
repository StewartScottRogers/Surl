---
id: BL-815
title: Add --control and --control-token-file with their refusals and the control --aihelp topic in Surl.Cli
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-785]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-815 — Add --control and --control-token-file with their refusals and the control --aihelp topic in Surl.Cli

## Goal

`surl --control <listen URL> --control-token-file <file>` is parsed into the configuration model, a non-loopback
or non-`http` control URL and any other combination the ADR refuses are refused before anything starts, and
`surl --aihelp control` explains the control endpoint for agents.

## Context

- Decision: BL-822's ADR decision 7 (option names, the loopback rule - `127.0.0.0/8`, `::1`, `localhost` - the
  scheme allowed, whether `--control` without a token file is refused or generates a token, refusal texts and exit
  code, the `--aihelp` topic name - planning `control` - and its help category).
- Where: `Application/Surl.Cli.UnitLibrary` (`CommandLineOptions.cs`, `CommandLineParser.cs`,
  `SurlCommandLine.cs`, `ListenUrlParser.cs` for the URL, `AiHelpTopics.cs`, `AiHelpProse.cs`,
  `AiHelpExamples.cs`, `HelpCategories.cs`). Serving it is BL-816; this task only parses, refuses and documents.
- `--aihelp` completeness (root `CLAUDE.md`): each option needs its `OptionArgumentType` and appears on each of its
  categories' pages; the new topic needs its `AiHelpProse.TopicAbout` paragraphs and an `AiHelpExamples` entry;
  the topic list pinned in `AiHelpTextTests.Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder` grows by
  one. The example must be one `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`
  can run before BL-816 serves anything - planning: the refusal of `--control http://0.0.0.0:0` - and BL-816 may
  add a serving example.
- The token file path is a local path: add it to `Surl.Mcp`'s path classification if `CommandLinePathMembers`'
  reflection test fails (that table is in `Application/Surl.Mcp.UnitLibrary`; if so, file a one-line follow-up
  rather than widening this task's `touches`).
- Stand-in: configuration as data for the control endpoint; `-K` files carry it with no extra work.

## Acceptance criteria

- [ ] Named `CommandLineParserTests`: `--control http://127.0.0.1:0 --control-token-file t` parses; a
      non-loopback host, a non-`http` scheme and each other refusal the ADR lists give its exit code and text; the
      options read from a `-K` file equal the same options on the command line.
- [ ] `surl --aihelp control` answers the topic's about text and example; every `--aihelp` completeness test named
      in Context passes.
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Cli` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
