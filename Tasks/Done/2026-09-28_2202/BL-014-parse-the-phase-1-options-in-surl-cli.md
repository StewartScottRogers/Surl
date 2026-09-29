---
id: BL-014
title: Parse the Phase 1 options in Surl.Cli
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-013]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-014 — Parse the Phase 1 options in Surl.Cli

## Goal

`Surl.Cli.UnitLibrary` parses a whole `surl` command line into one immutable
parsed-command-line record: every option in the command-line ADR's (BL-003) Phase 1
table, plus the listen URLs parsed by BL-013. Failures carry the exact `SurlExitCode` and
message the ADR gives. The library also produces the `--help` and `--version` text the
ADR specifies.

## Context

- The command-line ADR recorded by BL-003 under `Documentation/Planning/Decisions/` is
  the specification. It gives the option table, the parser conventions (bundling,
  attached arguments, `--option=value`, `--`, `--no-`, as measured from upstream curl
  8.21.0), the `--help` and `--version` text, and the error messages. Its `README.md`
  index names it.
- The listen-URL parser from BL-013 handles the positional arguments.
- `Surl.Cli.UnitLibrary/CLAUDE.md`: never touch the console. `Surl.Console` hands this
  library its arguments and its writers.
- Cyclomatic complexity is at most 10 per method (`CodeMetricsConfig.txt`, `CA1502`,
  enforced at build). A table-driven option parser keeps a growing table from breaking
  the build.
- `--help` and `--version` are results that tell the caller to print text and exit with
  `SurlExitCode.Ok`. They do not serve.

## Acceptance criteria

- [x] A command-line parser in `Surl.Cli.UnitLibrary` returns a parsed-command-line
      record (listen URLs and every Phase 1 option value, defaults applied) or a
      failure with a `SurlExitCode` and message.
- [x] Fast tests cover every row of the ADR's option table: the option accepted in each
      form the ADR adopts, and its bad-value case asserting the exact exit code and
      message.
- [x] Fast tests cover: an unknown option, an option missing its argument, no listen
      URL at all, several listen URLs in order, `--` followed by an argument that starts
      with `-`, and `--help` and `--version` returning the ADR's exact text with
      `SurlExitCode.Ok`.
- [x] No method in `Surl.Cli.UnitLibrary` exceeds complexity 10:
      `dotnet build Surl.Cli.UnitLibrary -warnaserror` is clean.
- [x] The fast tests are green, and `Measure-CodeQuality.ps1` reports no failing member
      in `Surl.Cli.UnitLibrary`.

## Notes

Delivered (2026-09-28):

- `CommandLineParser.Parse(IReadOnlyList<string>)` returns a `CommandLineParseResult`
  whose `Outcome` is `Serve` (with a `SurlCommandLine`), `ShowHelp`, `ShowVersion` or
  `Refused` (with a `CommandLineFailure`). The option table is data
  (`CommandLineOptions`, one row per option), read by `OptionArgumentReader` per argument
  kind, so adding an option adds a row, not a branch.
- `HelpText.Text` is ADR-0007 section 6's help, every line ending in
  `Environment.NewLine`. `VersionText.Compose(informationalVersion, runtimeIdentifier,
  servedSchemes)` builds the two version lines; the caller (`Surl.Console`, BL-019)
  supplies all three, since only it knows the `surl` assembly and the registered servers.
  `--help` and `--version` are results, not text: the caller writes the text and exits
  `SurlExitCode.Ok`.
- `CommandLineFailure` gained `FollowedByTryHelpLine` (default `false`) and the constant
  `TryHelpLine`; every command-line error sets it, listen-URL refusals do not (ADR-0007
  section 5).
- Measured: 270 Surl.Cli tests; Surl.Cli.UnitLibrary 100% line, 100% branch, 0 failing
  members, worst CRAP 10.

Choices where the ADR left a default (sensible default taken):

- **`ExchangeLimits` not yet on `SurlCommandLine`.** ADR-0007 section 3 says the record
  carries `ExchangeLimits`, but that type is BL-046's (Backlog, `Surl.Protocol.Abstractions`),
  outside this task's `touches`. Rather than stall the parser on it, `SurlCommandLine`
  holds the five per-exchange limits directly under `ExchangeLimits`' own member names
  (`HeadTimeout`, `MaxRequestHeadBytes`, `MaxLineBytes`, `MaxMessageBytes`,
  `MaxUploadBytes`). BL-059 (depends on BL-046) folds them into one `Limits` member, which
  brings the record to what the ADR says.
- **Member names the ADR left to BL-014:** `ServedDirectory`, `Verbose`, `AllowUploads`,
  `ListDirectories`, `FollowSymlinks`, `ServeDotFiles`, `MaxConnections`,
  `MaxConnectionsPerAddress`, `IdleTimeout`, `MaxTime`, `LowestTlsVersion`,
  `HighestTlsVersion`, `CertificateFile`, `KeyFile`, `CaCertificateFile`.
- **Order of the end-of-reading checks:** the lowest-above-highest TLS check runs before
  the no-URL check, since it is about an option actually written.
- **A seconds value below one tick** (`0.00000001`) rounds up to one tick rather than
  truncating to zero, so only a written `0` means no limit.
- **A bundle or attached argument in messages** names the whole argument as written
  (`option -m=30: …`, `option -vm: requires parameter`), per "the option exactly as the
  user wrote it".
- **An empty argument after `--`** gets the same blank-argument refusal as before it
  (section 5: "empty argument where a listen URL would be").
- `SslProtocols.Tls` and `Tls11` are obsolete in .NET (SYSLIB0039); the two places that
  name them for `--tlsv1.0`, `--tlsv1.1` and `--tls-max 1.0/1.1` suppress it locally with
  a reason.

The options BL-003 marks "parsed in Phase 1, served once BL-012 lands" (`--cert`,
`--key`, `--cacert`) are parsed and validated here, and are otherwise unused until
`https` is wired.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Surl.Cli parses the whole Phase 1 command line into SurlCommandLine, with ADR-0007's exact failures, help and version text
