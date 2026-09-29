---
id: BL-123
title: Answer --manual and write the --help testing text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-103, BL-107, BL-116, BL-117]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-009
created: 2026-09-29
completed:
---
# BL-123 — Answer --manual and write the --help testing text

## Goal

`surl --manual` writes ADR-0034's long text (deployment checklist, data directory and `.surl`
folder, in-memory mode, accounts and `--user-file`, log levels) and `surl --help testing`
explains every loosening option and why none is the default, each true of the code as it
then is.

## Context

FR-009 as ADR-0034 (BL-102) rewords it; ADR-0034 decisions 5 (`--help testing`) and 6
(`--manual`: sections, order, where the text lives, line width). It waits for the behaviour
it describes: BL-103 (categorised help), BL-107 (log levels), BL-116 (`--self-signed`) and
BL-117 (accounts, loosening options and their warnings).

- `Surl.Cli.UnitLibrary/HelpText.cs` and the categorised table BL-103 built; add
  `--manual` to `CommandLineOptions.cs` and its outcome to `CommandLineOutcome.cs` (or the
  shape ADR-0034 gives), and write it in `Surl.Console/CommandLineRunner.cs` `RunAsync`.
- Sources for the text, each to be checked against the code, not copied blindly:
  ADR-0031 (data directory, `.surl`, lock, in-memory), ADR-0006 (exposure defaults and
  limits), ADR-0032 (accounts, loosening options, `--self-signed`), ADR-0033 (levels),
  `Documentation/Wiki/Glossary.md` for the terms.

## Acceptance criteria

- [ ] `HelpTextTests` (or a `ManualTextTests` beside them) pin the `--help testing` text and
      the whole `--manual` text as ADR-0034 lays them out, with no line longer than its
      width and none with trailing spaces.
- [ ] A test proves every option named in the manual and in `--help testing` exists in
      `CommandLineOptions.All`, and every loosening option ADR-0032 lists is explained in
      `--help testing`.
- [ ] `CommandLineParserTests` prove `--manual` parses as ADR-0034 says (ending reading as
      `--help` does, or as decided), and `CommandLineRunnerTests` prove `RunAsync` writes the
      manual to `output` and returns `SurlExitCode.Ok`.
- [ ] `dotnet build Surl.Cli.UnitLibrary -warnaserror` and `dotnet build Surl.Console
      -warnaserror` are clean; the fast tests pass; both keep 100% line and branch coverage.

## Notes

## Log

- 2026-09-29: Created.
