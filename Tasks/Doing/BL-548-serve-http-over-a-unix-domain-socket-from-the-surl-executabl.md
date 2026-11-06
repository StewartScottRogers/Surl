---
id: BL-548
title: Serve HTTP over a Unix domain socket from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-547]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-548 — Serve HTTP over a Unix domain socket from the surl executable

## Goal

`surl` accepts BL-571's command-line form for a Unix domain socket (planning assumption:
`surl --unix-socket <path> http://localhost/`) and serves its HTTP server there, so a pinned
upstream curl run with `--unix-socket <path>` completes its exchange.

## Context

- Decision: BL-571's ADR, decisions 1 to 6. The option's name, its argument and its refusals are
  the ADR's.
- Code: `Application/Surl.Cli.UnitLibrary` (`CommandLineOptions`, `CommandLineParser`,
  `ListenUrlParser`, `OptionArgumentReading`, `HelpCategories`, `AiHelpProse`, `AiHelpExamples`,
  `ExitCodeGuidanceTable`) and `Application/Surl.Console/CommandLineRunner.cs`, which starts the
  listener through `IListenerFactory` from BL-547.
- `--aihelp` completeness (root `CLAUDE.md`, "`surl --aihelp` is how an agent learns the command
  line"): the new option needs its `OptionArgumentType` and appears on each of its categories'
  pages (`AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`,
  `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName`); an
  `AiHelpExamples` entry shows HTTP over a Unix socket, and
  `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` runs it.
- Upstream test cases this makes reachable by hand: 1435, 1436 (`--unix-socket %HTTPUNIXPATH`).

## Acceptance criteria

- [ ] `Surl.Cli.UnitTests` has named tests that parse the option, its missing argument and each
      refusal the ADR names, each returning the ADR's `SurlExitCode`.
- [ ] `Surl.Console.UnitTests` has a named test that runs `CommandLineRunner` with the option and
      serves one HTTP request over the Unix socket without `TestCategory=Integration`.
- [ ] `surl --help`, `surl --manual` and `surl --aihelp all` describe the option, and the
      `--aihelp` completeness tests named in Context pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and
      `Surl.Cli.UnitLibrary` keep 100% line and branch coverage.

## Notes

- Planning assumption (BL-399): the option name mirrors curl's `--unix-socket`; BL-571's ADR may
  choose another form, and this task follows the ADR.

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
