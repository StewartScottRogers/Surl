---
id: BL-548
title: Serve HTTP over a Unix domain socket from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-547]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Foundation/Surl.Output.UnitLibrary, Foundation/Surl.Output.UnitTests, Application/Surl.Mcp.UnitLibrary]
requirement: none
created: 2026-11-04
completed: 2026-11-06
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

- [x] `Surl.Cli.UnitTests` has named tests that parse the option, its missing argument and each
      refusal the ADR names, each returning the ADR's `SurlExitCode`.
- [x] `Surl.Console.UnitTests` has a named test that runs `CommandLineRunner` with the option and
      serves one HTTP request over the Unix socket without `TestCategory=Integration`.
- [x] `surl --help`, `surl --manual` and `surl --aihelp all` describe the option, and the
      `--aihelp` completeness tests named in Context pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and
      `Surl.Cli.UnitLibrary` keep 100% line and branch coverage.

## Notes

- Planning assumption (BL-399): the option name mirrors curl's `--unix-socket`; BL-571's ADR may
  choose another form, and this task follows the ADR.
- Delivered (2026-11-06), per ADR-0094 decisions 1, 2 and 6:
  - `Surl.Cli`: option rows `--unix-socket <path>` (`OptionArgumentReader.Path`) and
    `--abstract-unix-socket <name>` (new `OptionArgumentReader.AbstractSocketName`, type
    `abstract socket name`), category `surl`, setting `SurlCommandLine.UnixSocket`, the last one
    winning. New `UnixSocketCombination` refuses, once the whole line is read, a second listen URL,
    a UDP scheme, `--http3`, then `ftp`/`ftps`, each `FailedInit` (2) with the try-help line, and
    sets the socket on the one listen URL. Tests: `CommandLineParserUnixSocketTests` (parse, missing
    and blank argument, each refusal). `--help`, `--manual` (LISTEN URLS section), `--aihelp
    listen-urls` and a new `surl`-topic example describe it.
  - `Surl.Output`: `ListenerStatusLine.Format` writes `... over unix://<path>` (or `unix://@<name>`)
    for a Unix socket listen URL, and `FormatBoundListenUrl` uses the port asked for, since the
    listener binds none (it threw before).
  - `Surl.Console.UnitTests`: `CommandLineRunnerUnixSocketTests.RunAsync_UnixSocket_ServesOneHttpRequestOverTheSocketFileAndRemovesIt`
    runs `CommandLineRunner` with `surl`'s own listener factory on a socket file in the temp directory,
    sends curl 8.21.0's request bytes (ADR-0094's measurement) over a client `Socket`, gets an
    HTTP/1.1 answer, and finds the file removed after stop. No network, so not `Integration`, like
    BL-547's Unix socket tests. Nothing in `Surl.Console` itself needed changing.
- `touches` grew, no task in Doing naming either: `Foundation/Surl.Output.UnitLibrary` and
  `.UnitTests` (the start-up line threw on a listen URL with no bound port), and
  `Application/Surl.Mcp.UnitLibrary` (`CommandLinePathMembersTests` fails on an unclassified
  `SurlCommandLine` member; `UnixSocket` is listed without paths because its socket file is also on
  the listen URL, which the `ListenUrls` row already reads - listing it twice would refuse it twice).
- Choices taken as sensible defaults:
  - **The start-up line shows the port asked for**: `Listening on http://localhost:80/ over unix:///tmp/surl.sock`,
    where ADR-0094's example omits `:80`. Every other line, and the engine's `FormatListenUrl`, writes
    `<scheme>://<host>:<port>/`, so tests that read the port keep one format.
  - **UDP schemes are a fixed set in `Surl.Cli`** (`tftp`, `coap`, `coaps`, `dns` - the extended DNS
    server answers on UDP beside TCP): the parser runs before any server is composed, and a
    datagram listen URL given a Unix socket would otherwise bind UDP on its host and port.
  - **`--http3` is refused too** (`option --unix-socket: --http3 listens on UDP`): ADR-0094 said its
    refusal belongs to the task that built `--http3`, which now exists.
  - **The refusal names the option from the socket**: `--abstract-unix-socket` for an abstract name,
    else `--unix-socket`, which is the option given last.
  - **The FTP refusal reads** `option --unix-socket: ftp is not served over a Unix socket: its data connections are TCP`
    (ADR-0094 fixes the exit code, not the words).
- Coverage: `Surl.Console` and `Surl.Output.UnitLibrary` 100% line and branch; `Surl.Cli.UnitLibrary`
  100% line, every member this task changed or added 100% branch and complexity at most 10. The Cli
  library's remaining failing members (`CommandLineParser.RefuseCombination` 95% branch,
  `OptionArgumentReader.RefuseAuthorizedKeys` 91.67%, and complexity in `MailboxFileReader`,
  `ConfiguredFaultReader` and others) pre-date this task.
- Follow-up filed: BL-915 (a `Surl.McpServing.UnitTests` test failed once in a full fast run and
  passed three runs after; nothing this task touched).

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. surl --unix-socket <path> (and --abstract-unix-socket on Linux) serves its one listen URL on a Unix domain socket, per ADR-0094
