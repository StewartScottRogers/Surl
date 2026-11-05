---
id: BL-644
title: Read surl options from a config file with -K and --config in curl's config-file syntax
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-641]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-644 — Read surl options from a config file with -K and --config in curl's config-file syntax

## Goal

`surl -K <file>` and `surl --config <file>` read options from a file in upstream curl's config-file syntax, in
place on the command line, so any option `surl` takes can come from a file, with a bad line refused by
`<file>:<line>`.

## Context

- Decision: BL-641's ADR, decisions 1, 2, 5 and 6 (syntax rows measured from upstream curl 8.21.0, default file
  or none, nesting and depth, refusals and their exact stderr, bounds). The ADR wins over this summary.
- Where: `Application/Surl.Cli.UnitLibrary` - a config-file reader that turns the file's lines into the same
  argument stream `CommandLineParser.Parse` reads, so every option behaves identically from a file; it reads
  through an injected file-reading seam (Surl.Cli never touches the console or the disk directly; read its
  `CLAUDE.md`), and `Application/Surl.Console` supplies the real reader and writes the refusal with its exit code.
- `-K`/`--config` is a `CommandLineOptions` row: argument type a file path, help category per BL-641's ADR.
  Root `CLAUDE.md` "`surl --aihelp`" lists the completeness tests a new option must pass; the example needs an
  `AiHelpExamplePrecondition` for the config file to exist (as `KeytabAndUserFileExist` does).
- An option holding a secret (`--user`, `--pass`) read from a file is still never echoed in a refusal
  (`CommandLineOption.ArgumentHoldsSecret`).
- Tests: in-memory file content, no disk; platform-neutral paths.

## Acceptance criteria

- [x] Named tests in `Application/Surl.Cli.UnitTests` cover every syntax row of BL-641's ADR (comment, `=`/`:`/
      whitespace separators, dashes optional, each escape, quoted whitespace, an unknown escape), options from the
      file taking effect at the `-K`'s position (a later command-line option overriding a file's scalar option and
      the reverse), several `-K`s, nesting up to the limit, nesting past it and a cycle refused, `-` reading
      standard input, and each refusal's exit code and exact text with `<file>:<line>`.
- [x] A named test in `Application/Surl.Console.UnitTests` runs `surl -K <file> <url>` through `CommandLineRunner`
      with the file read by the real seam over a temporary file, and one with an unreadable file returns the
      ADR's exit code and text.
- [x] `-K`/`--config` passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues` and
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName`; `ManualText`, `OptionHelp`
      and the `AiHelpProse` paragraph describe it; its `AiHelpExamples` entry passes
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Cli.UnitLibrary` and `Surl.Console` keep 100% line and branch coverage.

## Notes

- Built: `IConfigFileSource` (seam), `ConfigFileText`/`ConfigFileLine` (line reader), `-K`/`--config` and
  `--url` rows (category `surl`), in-place expansion in `CommandLineParser` (each line's words read as an
  isolated argument frame, so `help` with no subject or `log-file` with no value never takes a word from
  the command line or the next line), `UnreadableConfigFileSource` for the overloads without a seam;
  `DiskConfigFileSource` and `CommandLineRunner`'s `configFiles` seam in `Surl.Console`. Tests:
  `CommandLineParserConfigFileTests` (every syntax row, in place both ways, several `-K`, nesting to 5,
  past 5, cycles, `-K -` once, refusal rows 1 to 9 with exact text), `CommandLineRunnerConfigFileTests`
  (real seam over a temporary file, a missing file and a directory: 37, standard input),
  `DiskConfigFileSourceTests`; the `surl` topic gains an `AiHelpExamples` entry with
  `AiHelpExamplePrecondition.ConfigFileExists`, and `ManualText` a CONFIG FILES section.
- Choices (sensible defaults, rule 1):
  - A config line whose value no option takes is refused: a flag (`verbose = 1`) with `option --verbose:
    does not take a parameter`, as `--verbose=1` is on the command line; a value after one already written
    into the word (`--log-file=a b`) with decision 5 row 5's text. curl warns and ignores; refusing matches
    ADR-0101's "refuse rather than silently drop" stance (rows 7 and 8).
  - A refusal's name never holds a value: of a short bundle it is the word up to the first option taking a
    value (`-u` of `-uann:secret`), so `ArgumentHoldsSecret` holds in files too.
  - A nested file's own refusal is located once, by the innermost file; an opening refusal (unreadable,
    too deep, cycle) is located by the including line, as decision 5 row 1's text shows. A too-large
    nested file names itself only.
  - `-K` is not in the short `--help` list (curl's short list leaves it out too).
  - The seam is synchronous (`byte[]? Read(path, maxByteCount)`), because `CommandLineParser.Parse` is
    synchronous and called by `Surl.Api` and `Surl.Mcp`; the parser asks for 16 MiB + 1 bytes so a file
    past the bound is refused without reading all of it.
  - Cycle identity is the source's `IdentityOf`: the full path, upper-cased except on Linux.
- Left out, filed as BL-771: `SurlCommandLine.ConfigFiles` and the readable-by-others warning (ADR-0101
  decision 5) - adding the member breaks `Surl.Mcp.UnitTests`' member classification test, and
  `Surl.Mcp.UnitLibrary` is outside this task's `touches`. `Surl.Api` and `Surl.Mcp` still parse with
  the two-argument overload, so a `-K` there exits 37 until BL-668 and BL-612 give them a source.
- Coverage (fast tests): `Surl.Console` 100% line, 100% branch, 0 failing members. `Surl.Cli.UnitLibrary`
  100% line, 99.58% branch: every new member is at 100%; the three failing members
  (`RefuseCombination`, `OptionArgumentReader.RefuseAuthorizedKeys`, `HelpText.CategoryPageLines`) are
  untouched and were failing before, tracked by BL-652 (whose goal is 0 failing members).
- The real-disk tests are `[TestCategory("Integration")]` as `.claude/rules/testing.md` requires; the
  disk branch of `DiskConfigFileSource.Read` is covered fast by an invalid path.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. surl -K/--config reads options from a file in curl's config-file syntax in place, and --url adds a listen URL
