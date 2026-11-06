---
id: BL-815
title: Add --control and --control-token-file with their refusals and the control --aihelp topic in Surl.Cli
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-785]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
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

- [x] Named `CommandLineParserTests`: `--control http://127.0.0.1:0 --control-token-file t` parses; a
      non-loopback host, a non-`http` scheme and each other refusal the ADR lists give its exit code and text; the
      options read from a `-K` file equal the same options on the command line.
- [x] `surl --aihelp control` answers the topic's about text and example; every `--aihelp` completeness test named
      in Context passes.
- [x] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Cli` keeps 100% line and branch coverage.

## Notes

- Delivered directly (one library and its tests; ADR-0124 decision 7 is the plan). `ControlEndpointOptionReader`
  reads `--control` into `SurlCommandLine.ControlListenUrl` when the option is read (so a `-K` refusal is located
  `<file>:<line>:`), and checks the pair in `CommandLineParser.Finish`. Tests: `CommandLineParserControlTests`
  (31 cases), `AiHelpTextTests.Prose_EverySurlLineQuoted_IsOneTheCodeWrites` pins each refusal text.
- Choices (defaults taken): the scheme check comes first, so `--control ftp://...` and `--control nosuch://...`
  both say `needs http, not <scheme>` rather than "Protocol not supported"; a malformed control URL is refused as
  a bare listen URL is (exit 3, `(3) URL rejected: ...`); refusals carry no try-help line, as the ADR quotes them;
  the last `--control` given wins; `[::1%zone]` and the short form `127.1` are refused (the ADR names `::1` and
  `127.0.0.0/8`, and .NET parses a named zone as scope 0, which would otherwise pass on some platforms); no host
  name is resolved, only `localhost` is accepted by name.
- Both options are in the `security` category as well as `testing`: `AiHelpFactsTests` requires every testing
  option to be a security option, and they widen what a peer may do. The ADR's "testing" stays true.
- `--control`'s help says "Serve the test control endpoint" without "(warns)", and the prose, manual and
  explanation say this build only reads and checks the options: BL-816 serves it, warns and reads the token file,
  and should update those words then.
- Touches widened to `Application/Surl.Mcp.UnitLibrary` and `.UnitTests`: `CommandLinePathMembers`' reflection
  test failed as Context foresaw; no task in Doing names them, so the two-line fix (`ControlTokenFile` is a path
  member, `ControlListenUrl` is not) was made here instead of filing a follow-up that would leave the fast tests red.
- Coverage: `Surl.Cli.UnitLibrary` 100% lines; its six members over the complexity limit by Cobertura's measure
  (`MailboxFileReader`, `ConfiguredFaultReader.ReadScope`/`ReadReply`) and `CsvRecordReader.ReadQuoted`'s one branch
  predate this task and are untouched by it; `Surl.Mcp.UnitLibrary` 100%/100%.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. surl --control <loopback http URL> --control-token-file <file> parses into SurlCommandLine, the ADR-0124 refusals exit 2, and surl --aihelp control explains it
