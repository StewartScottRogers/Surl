# Surl.Cli.UnitLibrary

Phase 1.

Surl's command line: the option table, argument parsing, the help and the manual, and the
mapping from a bad command line to a `SurlExitCode`. `surl [options] <url>` names what to
listen on, the way `curl [options] <url>` names what to fetch. An option keeps curl's name
and meaning wherever a server-side meaning exists (`--cert`, `--key`, `--cacert`, `-u`,
`-v`, `-s`, `-S`, `--trace`, `--trace-ascii`, `--trace-time`); the table is ADR-0007's,
with the rows ADR-0010, ADR-0031, ADR-0032, ADR-0033 and ADR-0034 add. It references
`Surl.Core.UnitLibrary`, `Surl.Output.UnitLibrary` (for `LogLevel` and `TraceDumpLayout`)
and `Surl.Protocol.Abstractions.UnitLibrary`.

- `CommandLineOptions` is the one option table: every `CommandLineOption` row carries its
  `OptionHelp` (argument name, description, `HelpCategories`, short-list flag, default and,
  for a loosening option, its `Explanation`), so an option cannot be added without its help.
  `CommandLineParser.Parse` reads a whole command line by it, left to right, into a
  `CommandLineParseResult`: `Serve` with a `SurlCommandLine`, `ShowHelp` with its
  `HelpSubject`, `ShowManual`, `ShowVersion`, or `Refused` with a `CommandLineFailure`.
  `ListenUrlParser` reads each listen URL; `OptionArgumentReader` reads each argument kind.
- Accounts and loosening options (ADR-0032 section 1): each `-u`/`--user` adds a
  `CommandLineAccount` to `SurlCommandLine.Accounts`, split at the first `:`; a refusal
  of one never echoes its value (`CommandLineOption.ArgumentHoldsSecret`), and a user name
  given twice is refused after the whole line is read. `--user-file` is kept as a path
  (`SurlCommandLine.UserFile`) and read by `Surl.Console`. `--allow-anonymous`,
  `--allow-plaintext-auth`, `--auth <methods>` (`GivenAuthenticationMethods`, matched
  case-insensitively, in ADR-0032 section 3's order) and `--self-signed` are parsed here;
  `--self-signed` with `--cert` is refused.
- Log levels (ADR-0033 section 2): `-s`, `-v`, `--log-level`, `--trace` and `--trace-ascii`
  each set `SurlCommandLine.LogLevel`, the last one given winning, and `-S` (`ShowError`)
  turns `none` into `error` once the whole line is read. `TraceFile`, `TraceLayout`,
  `TraceTime` and `LogFile` hold the rest; opening the files is `Surl.Console`'s.
- Help (ADR-0034): `HelpText.Answer` turns a help subject into a `HelpAnswer` - the short
  list, `all`, `category`, one of `HelpCategories.All`, or an option page - laid out to 79
  columns by `HelpLayout`. `ManualText.Text` is the `--manual` text; `VersionText.Compose`
  the `--version` text.
- AI help (ADR-0046): `AiHelpText.Answer` turns an `--aihelp` topic into a `HelpAnswer` in
  Markdown - the overview, one of `AiHelpTopics.All` (every help category plus `exit-codes`
  and `listen-urls`), `all`, or the unknown-topic answer - with its option and exit-code
  tables generated from `CommandLineOptions`, `OptionArgumentType` and
  `ExitCodeGuidanceTable`, and the option's left side shared with `HelpText.LeftSide`. Every
  hand-written section still reads `Nothing for this topic.` (BL-140), and nothing calls it
  from the command line yet (BL-141).

Never touch the console here; `Surl.Console` hands this library its arguments and writers.
Any change to an option's behaviour updates its `OptionHelp` and the `ManualText` section
that describes it in the same change (ADR-0034 decision 6).
