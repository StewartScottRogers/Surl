# Surl.Cli.UnitLibrary

Phase 1.

Surl's command line: the option table, argument parsing, the help, the AI help and the
manual, and the mapping from a bad command line to a `SurlExitCode`. `surl [options] <url>`
names what to listen on, the way `curl [options] <url>` names what to fetch. An option
keeps curl's name and meaning wherever a server-side meaning exists (`--cert`, `--key`,
`--cacert`, `-u`, `-v`, `-s`, `-S`, `--trace`, `--trace-ascii`, `--trace-time`); the table
is ADR-0007's, with the rows ADR-0010, ADR-0031, ADR-0032, ADR-0033, ADR-0034 and ADR-0046
add. It references
`Surl.Core.UnitLibrary`, `Surl.Output.UnitLibrary` (for `LogLevel` and `TraceDumpLayout`)
and `Surl.Protocol.Abstractions.UnitLibrary`.

- `CommandLineOptions` is the one option table: every `CommandLineOption` row carries its
  `OptionHelp` (argument name, description, `HelpCategories`, short-list flag, default and,
  for a loosening option, its `Explanation`), so an option cannot be added without its help.
  `CommandLineParser.Parse` reads a whole command line by it, left to right, into a
  `CommandLineParseResult`: `Serve` with a `SurlCommandLine`, `ShowHelp` with its
  `HelpSubject`, `ShowAiHelp` with its `AiHelpTopic`, `ShowManual`, `ShowVersion`, or `Refused` with a `CommandLineFailure`.
  `ListenUrlParser` reads each listen URL; `OptionArgumentReader` reads each argument kind.
- Accounts and loosening options (ADR-0032 section 1): each `-u`/`--user` adds a
  `CommandLineAccount` to `SurlCommandLine.Accounts`, split at the first `:`; a refusal
  of one never echoes its value (`CommandLineOption.ArgumentHoldsSecret`), and a user name
  given twice is refused after the whole line is read. `--user-file` is kept as a path
  (`SurlCommandLine.UserFile`) and read by `Surl.Console`. `--allow-anonymous`,
  `--allow-plaintext-auth`, `--auth <methods>` (`GivenAuthenticationMethods`, matched
  case-insensitively, in ADR-0032 section 3's order) and `--self-signed` are parsed here;
  `--self-signed` with `--cert` is refused.
- SSH server options (ADR-0051 decisions 4 to 6): `--hostkey` and `--hostcert` each add a path
  to `HostKeyFiles` and `HostCertificateFiles`; `--authorized-keys <user:file>` adds a
  `CommandLineAuthorizedKeys` to `AuthorizedKeys`, split at the first `:`, a user given twice
  refused after the whole line is read; `--throwaway-hostkey` (`ThrowawayHostKey`) with
  `--hostkey` is refused; `--allow-weak-ssh-algorithms` sets `AllowWeakSshAlgorithms`. `--pass`
  without `--cert` is accepted when a `--hostkey` is given. Each is in the `ssh` help category
  (`SSH protocol`, schemes `scp` and `sftp`, ADR-0051 decision 12), with every option the SSH
  server reads. `Surl.Console` reads the files and still refuses `--hostcert` (until BL-222) and
  `--allow-weak-ssh-algorithms` (until BL-221) as not available in this build.
- Log levels (ADR-0033 section 2): `-s`, `-v`, `--log-level`, `--trace` and `--trace-ascii`
  each set `SurlCommandLine.LogLevel`, the last one given winning, and `-S` (`ShowError`)
  turns `none` into `error` once the whole line is read. `TraceFile`, `TraceLayout`,
  `TraceTime` and `LogFile` hold the rest; opening the files is `Surl.Console`'s.
- Help (ADR-0034): `HelpText.Answer` turns a help subject into a `HelpAnswer` - the short
  list, `all`, `category`, one of `HelpCategories.All`, or an option page - laid out to 79
  columns by `HelpLayout`. `ManualText.Text` is the `--manual` text; `VersionText.Compose`
  the `--version` text.
- AI help (ADR-0046; the glossary's "AI help", "AI help topic", "AI help example",
  "argument type" and "exit-code guidance"): `AiHelpText.Answer` (`AiHelpText.cs`) turns an
  `--aihelp` topic into a `HelpAnswer` in Markdown - the overview, one of
  `AiHelpTopics.All` (`AiHelpTopics.cs`, `AiHelpTopic.cs`: every `HelpCategories.All` row
  with its name, description and `HelpCategory.Schemes`, plus `exit-codes` and
  `listen-urls`, found in any case by `AiHelpTopics.TryFind`), `all`, or the unknown-topic
  answer. Its tables are generated, never hand-written: the option table from
  `CommandLineOptions` and each row's `CommandLineOption.ArgumentType`
  (`OptionArgumentType.cs`, paired with its reader in `OptionArgumentReading.cs`), the
  option's left side shared with `HelpText.LeftSide`; the exit-code table from
  `ExitCodeGuidanceTable.All` (`ExitCodeGuidanceTable.cs`, `ExitCodeGuidance.cs`). The
  hand-written paragraphs are `AiHelpProse`'s source constants (`AiHelpProse.cs`); the
  examples are `AiHelpExamples.All` (`AiHelpExamples.cs`, `AiHelpExample.cs`,
  `AiHelpExamplePrecondition.cs`), public so `Surl.Console.UnitTests` can run them.
  `--aihelp [topic]` is a row of `CommandLineOptions` (`CommandLineOptionKind.AiHelp`, no
  short name), read as `--help` reads its subject into `CommandLineParseResult.AiHelpTopic`
  with the outcome `CommandLineOutcome.ShowAiHelp`, which `Surl.Console` answers.
  `AiHelpFactsTests` and `AiHelpTextTests` in `Surl.Cli.UnitTests` fail when an option, a
  topic or a `SurlExitCode` member is missing from it (root `CLAUDE.md`). Work that changes
  what surl does updates the `AiHelpProse` paragraph and the example that describe it in
  the same change, as it does `ManualText`.

Never touch the console here; `Surl.Console` hands this library its arguments and writers.
Any change to an option's behaviour updates its `OptionHelp` and the `ManualText` section
that describes it in the same change (ADR-0034 decision 6).
