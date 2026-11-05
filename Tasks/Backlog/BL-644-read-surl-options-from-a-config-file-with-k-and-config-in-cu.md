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
completed:
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

- [ ] Named tests in `Application/Surl.Cli.UnitTests` cover every syntax row of BL-641's ADR (comment, `=`/`:`/
      whitespace separators, dashes optional, each escape, quoted whitespace, an unknown escape), options from the
      file taking effect at the `-K`'s position (a later command-line option overriding a file's scalar option and
      the reverse), several `-K`s, nesting up to the limit, nesting past it and a cycle refused, `-` reading
      standard input, and each refusal's exit code and exact text with `<file>:<line>`.
- [ ] A named test in `Application/Surl.Console.UnitTests` runs `surl -K <file> <url>` through `CommandLineRunner`
      with the file read by the real seam over a temporary file, and one with an unreadable file returns the
      ADR's exit code and text.
- [ ] `-K`/`--config` passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues` and
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName`; `ManualText`, `OptionHelp`
      and the `AiHelpProse` paragraph describe it; its `AiHelpExamples` entry passes
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Cli.UnitLibrary` and `Surl.Console` keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
