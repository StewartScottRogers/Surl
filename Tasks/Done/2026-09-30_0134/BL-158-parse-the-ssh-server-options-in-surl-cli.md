---
id: BL-158
title: Parse the SSH server options in Surl.Cli
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-154]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests, Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs, Surl.Protocol.Abstractions.UnitTests/SurlExitCodeTests.cs]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-29
---
# BL-158 — Parse the SSH server options in Surl.Cli

## Goal

`surl` parses the SSH server options BL-154's ADR decides (host key and authorized keys, or
whatever the ADR names) into `SurlCommandLine`, with their help, AI-help facts and manual
text, and refuses a start that gives them until BL-171 composes the SSH server.

## Context

- Decision: BL-154's ADR (option names, arguments, defaults, descriptions, categories, refusal
  texts, any new `SurlExitCode` member).
- Code: `Surl.Cli.UnitLibrary/CommandLineOptions.cs` (one row per option with its
  `OptionHelp`; see `--user-file` for a `<file>` option), `SurlCommandLine.cs`,
  `OptionArgumentType.cs`, `ManualText.cs`, `AiHelpProse.cs`; `Surl.Cli.UnitLibrary/CLAUDE.md`
  (an option cannot be added without its help; a changed behaviour updates `ManualText` and
  `AiHelpProse` in the same change). `AiHelpFactsTests` fail when an option or a
  `SurlExitCode` member is missing from the AI help.
- Categories: the ADR's non-protocol categories only; the SSH protocol category joins in BL-171
  (ADR-0034 decision 1: the registering task adds it).
- Until BL-171, the options would describe listeners that do not exist. Follow ADR-0032
  section 1's precedent for `--auth` words: `Surl.Console` refuses a start that gives one with
  `FailedInit` (2), `surl: (2) --<option> is not available in this build`, no `try` line;
  BL-171 removes the refusal. This is why `Surl.Console` is in `touches`.
- A new `SurlExitCode` member, if the ADR adds one, goes into
  `Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs` with its test in `SurlExitCodeTests`
  and its `ExitCodeGuidanceTable` row (ADR-0046) in this same change.

## Acceptance criteria

- [x] Each option BL-154's ADR names parses as it says (argument kind, default, repeats,
      negation), with a `CommandLineParserTests` case for each refusal text the ADR gives.
- [x] `surl --help <option>` and `--help all` show the ADR's description, in its categories;
      `ManualText` and `AiHelpProse` describe the options; `AiHelpFactsTests`,
      `AiHelpTextTests`, `HelpTextTests` and `ManualTextTests` pass.
- [x] A `Surl.Console.UnitTests` test shows a start giving one of the options returns
      `FailedInit` with the text in Context.
- [x] Any new `SurlExitCode` member has its number, its `SurlExitCodeTests` case and its
      `ExitCodeGuidanceTable` row.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green;
      `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member in
      `Surl.Cli.UnitLibrary` and `Surl.Console`.

## Notes

- Plan (ADR-0051 decision 5): five new rows in `CommandLineOptions` - `--hostkey <file>` and
  `--hostcert <file>` (each adds to `SurlCommandLine.HostKeyFiles`/`HostCertificateFiles`),
  `--throwaway-hostkey` and `--allow-weak-ssh-algorithms` (negatable flags, later wins),
  `--authorized-keys <user:file>` (new `CommandLineAuthorizedKeys`, reader
  `OptionArgumentReader.ReadAuthorizedKeys`, argument type `user:file`). `--pass`'s description is
  now `Passphrase for --key and --hostkey`.
- Parse refusals, each with the `try` line: decision 6's five `--authorized-keys` texts (no colon,
  empty user, control character, empty file, user given twice after the whole line, named as the
  repeat was written); decision 4's `option --throwaway-hostkey: cannot be used with --hostkey`,
  checked once the whole line is read.
- Choice: `--pass` without `--cert` is no longer refused when a `--hostkey` is given, since
  ADR-0051 decision 4 makes it decrypt host keys too; `--key` and `--key-type` without `--cert`
  are still refused.
- Choice: `--throwaway-hostkey` is listed in `security` and `testing`, not `testing` alone as
  ADR-0051's table says: `AiHelpFactsTests` holds every testing option to be a security option
  too (as `--self-signed` is), and `--help testing` needs its `Explanation` paragraph, which it
  has. ADR-0051 could not be edited here (BL-173 held `Documentation/Planning/Decisions`), so
  BL-229 records it there. `--allow-weak-ssh-algorithms` is `security` only, as the ADR says, and
  so has no `Explanation`.
- Choice: `Surl.Console`'s refusal (`CommandLineRunner.FindUnavailableOption`) fires when an
  option's value is set - a file option given, or a flag left on - so `--no-throwaway-hostkey`
  alone is served; the first in option-table order is named; it runs before the data directory
  and `--user-file` are touched.
- `--manual` gains an `SSH OPTIONS` section between `ACCOUNTS` and `LOOSENING OPTIONS`, and exit
  code 2's line and `ExitCodeGuidanceTable`'s `FailedInit` meaning name "an option not available
  in this build". `AiHelpProse`'s auth, security, testing and tls paragraphs describe the options
  and their refusal. No new `SurlExitCode` member (ADR-0051: none needed), so `Surl.Protocol.
  Abstractions` was not changed.
- Measured: Surl.Cli.UnitLibrary and Surl.Console 100% line and branch, 0 failing members.
  `Surl.Content.UnitLibrary`'s `ContentStore.DescribeDirectoryEntry` (complexity 12) fails the
  gate from before this task; filed as BL-228.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl parses --hostkey, --hostcert, --throwaway-hostkey, --authorized-keys and --allow-weak-ssh-algorithms with help, manual and AI help, and refuses a start giving one as not available in this build
