---
id: BL-103
title: Answer --help, --help all, --help category and --help <category> from categorised option help
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-102]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-009
created: 2026-09-29
completed: 2026-09-29
---
# BL-103 — Answer --help, --help all, --help category and --help <category> from categorised option help

## Goal

`surl --help` writes the short list with its pointer to categories, and `--help all`,
`--help category`, `--help <category>` (and `--help <option>` if ADR-0034 keeps it) write
ADR-0034's texts, built from one categorised option table in `Surl.Cli`, for every option
surl parses today.

## Context

FR-009; ADR-0034 (BL-102) pins the categories, each option's categories and description,
every layout, how the optional subject is read, and the result shape. This task covers the
options that exist now; BL-104 and BL-108 add their options' help entries to the table
this task builds, and BL-123 adds `--manual` and the `--help testing` paragraphs.

- `Surl.Cli.UnitLibrary/HelpText.cs`: today one static `Lines` array and `Text`; replace
  it with the categorised help ADR-0034 decides (option lines generated from one table so a
  new option is one entry, not an edit in several texts).
- `Surl.Cli.UnitLibrary/CommandLineOptions.cs` (`help` is `CommandLineOptionKind.Help`,
  no argument), `CommandLineParser.cs`, `CommandLineOutcome.cs` (`ShowHelp`) and
  `CommandLineParseResult.cs`: carry the help subject ADR-0034 section 4 decides.
- `Surl.Console/CommandLineRunner.cs` `RunAsync` writes `HelpText.Text` for `ShowHelp`;
  it must write the text for the parsed subject. Tests:
  `Surl.Console.UnitTests/CommandLineRunnerTests.cs`
  (`RunAsync_Help_WritesHelpTextAndReturnsOk`) and `ProgramTests.cs`
  (`Main_Help_WritesHelpTextToStandardOutputAndReturnsOk`).
- Help is not sent to upstream curl, so no wire measurement is needed beyond ADR-0034's.

## Acceptance criteria

- [x] `Surl.Cli.UnitTests/HelpTextTests.cs` pins, byte for byte from ADR-0034 (lines built
      with `Environment.NewLine`): the `--help` short list and pointer lines, `--help all`,
      `--help category`, one `--help <category>` for each category that has options today,
      and the unknown-subject answer.
- [x] A `HelpTextTests` test proves every option in `CommandLineOptions.All` appears in
      `--help all` and in at least one category, and that no line has trailing spaces.
- [x] `CommandLineParserTests` prove how the subject is read (`--help`, `--help all`,
      `-h auth`, `--help` followed by a listen URL, `--help` after an earlier error), each
      as ADR-0034 section 4 states.
- [x] `CommandLineRunnerTests` prove `RunAsync` writes each subject's text to `output` and
      returns `SurlExitCode.Ok`, and the unknown subject's answer with the exit code
      ADR-0034 states.
- [x] `dotnet build Surl.Cli.UnitLibrary -warnaserror` and
      `dotnet build Surl.Console -warnaserror` are clean; the fast tests pass; both keep
      100% line and branch coverage; no method exceeds complexity 10.

## Notes

- Delivered directly in the session against ADR-0034 (the ADR is the plan; no separate
  architect pass was needed for one library and its runner).
- Shape, as ADR-0034 section 4: `CommandLineOption` gains a last `OptionHelp Help`, so an
  option cannot be added without its help; `HelpCategories.All` holds the 14 categories;
  `HelpText.Answer(string?)` returns a `HelpAnswer(Output, Error)`;
  `CommandLineParseResult.ShowHelp(string?)` and `HelpSubject` replace the static
  `ShowHelp`; `CommandLineRunner` writes both streams and returns `Ok`.
- Choice: `OptionHelp` has no `Explanation` member yet. ADR-0034 gives it one for the four
  loosening options' `--help testing` paragraphs, but none of those options is parsed and
  BL-123 writes the text; an always-null member would be an uncoverable branch. BL-123 adds
  it with the first paragraph.
- Choice: the layout rules live in their own `HelpLayout` (curl's column rule, the pointer
  name list, the option-page paragraph wrap) so `HelpLayoutTests` can drive the branches
  today's 34-character descriptions never reach (a page wider than 79 columns, a
  description of 77 or more, a word too long for a line).
- The option page's first line is four spaces and the left side without its padding,
  so `    --max-line <bytes>` and `    -h, --help <subject>` (ADR-0034 decision 3).
- `-hV` now asks for help on `V` (ADR-0034 decision 4: the rest of the argument after `-h`
  is the subject); `-Vh` still shows the version. `--help=all` is no longer refused.
- Split `CommandLineParser.ApplyOption` into `ApplyOptionTakingAValue` and
  `ApplyOptionTakingNoValue` to keep Cobertura's complexity at 10.
- Verified: `dotnet build -warnaserror` clean, fast tests green (Surl.Cli.UnitTests 368,
  Surl.Console.UnitTests 105), `Measure-CodeQuality.ps1`: Surl.Cli.UnitLibrary and
  Surl.Console 100% line and branch, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl --help answers the short list, all, category, each category and each option from one categorised option table
