---
id: BL-123
title: Answer --manual and write the --help testing text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-103, BL-107, BL-116, BL-117]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests, Surl.Protocol.Abstractions.UnitLibrary]
requirement: FR-009
created: 2026-09-29
completed: 2026-09-29
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

- [x] `HelpTextTests` (or a `ManualTextTests` beside them) pin the `--help testing` text and
      the whole `--manual` text as ADR-0034 lays them out, with no line longer than its
      width and none with trailing spaces.
- [x] A test proves every option named in the manual and in `--help testing` exists in
      `CommandLineOptions.All`, and every loosening option ADR-0032 lists is explained in
      `--help testing`.
- [x] `CommandLineParserTests` prove `--manual` parses as ADR-0034 says (ending reading as
      `--help` does, or as decided), and `CommandLineRunnerTests` prove `RunAsync` writes the
      manual to `output` and returns `SurlExitCode.Ok`.
- [x] `dotnet build Surl.Cli.UnitLibrary -warnaserror` and `dotnet build Surl.Console
      -warnaserror` are clean; the fast tests pass; both keep 100% line and branch coverage.

## Notes

- Built ADR-0034 decisions 4 to 6 as the ADR shapes them: `CommandLineOptionKind.Manual`,
  `CommandLineOutcome.ShowManual`, `CommandLineParseResult.ShowManual`, the `-M`/`--manual`
  row (category `surl`, no default), `OptionHelp.Explanation` (a defaulted last member, so the
  other rows stay as they are), `ManualText` (a `string[]` of lines in
  `Surl.Cli.UnitLibrary/ManualText.cs`), the `--help testing` paragraphs after its option
  lines, the paragraph on each loosening option's own page, and the new try line
  `try 'surl --help' or 'surl --manual' for more information`.
- `--manual` parses like `-V`: no argument, not negatable, `=value` refused, and it ends
  reading where it stands, so `-V --manual` shows the version, `--help --manual` shows
  `--manual`'s help page, and an error before it is still reported.
- Before any text was pinned, a read-only agent checked every claim against the code. Where
  ADR-0034 decision 5's sentences were untrue of the code, the text was changed, as that
  decision says to:
  - `--auth`: this build checks only basic, bearer and digest, and refuses to start on
    ntlm, negotiate or aws-sigv4. The paragraph says so, and its example is
    `--auth digest` rather than answering curl's `--ntlm`.
  - `--allow-anonymous`: MQTT accepts every *well-formed* CONNECT. A bad protocol level
    still gets CONNACK 1, and an empty client id without CleanSession still gets CONNACK 2.
    HTTP has no uploads yet, so the risk is written as "can publish and subscribe over
    MQTT", not as uploads.
  - Every warning is written "from the info log level up" (ADR-0033 section 7): `-s` hides it.
- Manual decisions (sensible defaults): the ACCOUNTS section says the HTTP rules apply
  "unless --allow-anonymous is given", and that methods other than GET and HEAD always need
  a login (`HttpAuthenticationRequest.IsWrite`). EXIT CODES lists only the codes the code
  returns, with what each means there, including 2 for a `--cacert` file that does not exist
  and 23 for a trace file that is the `--log-file` file (ADR-0037). The command
  `surl --help testing` is set on its own indented line so no line break splits it.
- `OptionNames` (in the test project) pulls every `--name`, `--no-name` and `-x` out of a
  text and asserts each one is in the option table. `-k` and `--digest` are skipped because
  they are named as curl's options.
- Added `Surl.Protocol.Abstractions.UnitLibrary` to `touches`. The only change there is the
  `SurlExitCode.FailedInit` doc comment, which quoted the old try line. No task in Doing
  (BL-118, BL-130) names that project.
- Coverage (`Measure-CodeQuality.ps1`): Surl.Cli.UnitLibrary and Surl.Console are at 100%
  line and 100% branch. The one failing member solution-wide is in Surl.Content.UnitLibrary,
  which this task does not touch. Surl.Cli.UnitTests has 540 tests and Surl.Console.UnitTests
  has 182, all passing.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl -M/--manual writes the manual, --help testing explains the four loosening options, and the try line names --manual
