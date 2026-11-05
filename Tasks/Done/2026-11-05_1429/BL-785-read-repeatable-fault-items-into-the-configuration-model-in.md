---
id: BL-785
title: Read repeatable --fault items into the configuration model in Surl.Cli
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Mcp.UnitLibrary, Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-785 — Read repeatable --fault items into the configuration model in Surl.Cli

## Goal

`surl --fault <item>` (repeatable, and in a `-K` file) is parsed into `SurlCommandLine.Faults`, so the command
line, config files, `Surl.Api` and the control endpoint all describe faults in one configuration model.

## Context

- Decision: BL-822's ADR decision 4 (the item keys, kinds, scopes, `times`, refusals and warning text). Planning
  examples, to be replaced by the ADR's grammar: `drop;listener=smtp://127.0.0.1:2525;times=1`,
  `refuse-login;account=alice`, `delay;ms=500;path=/slow`, `error;scheme=smtp;command=RCPT;reply=451 4.3.0 try
  later`, `out-of-space;path=/uploads`.
- The item grammar is ADR-0101 decision 3's, already read by `ConfigurationItemReader`
  (`Application/Surl.Cli.UnitLibrary/ConfigurationItemReader.cs`, BL-645): reuse it, its bounds (ADR-0101
  decision 6) and its refusal texts. The record goes beside the layer-2 records (planning name `ConfiguredFault`),
  and the list onto `SurlCommandLine` in command-line order. `-K` files need nothing extra: the config reader
  (BL-644) feeds options to the same parser.
- Composing the faults is BL-803's; the start-up warning is written there. This task only reads and refuses.
- `--aihelp` completeness (root `CLAUDE.md`): the option needs its `OptionArgumentType` through its reader's
  `OptionArgumentReading<T>` and must appear on the page of each of its categories (planning: `testing`), or
  `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues` and
  `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName` fail. `--help testing` explains
  each loosening option (FR-009); `--fault` is one.
- Stand-in: this is the configuration-as-data half of fault injection; it publishes nothing.
- Amended by ADR-0124 decision 4: the `reply` of an `error` rule is checked against the original schemes'
  reply-shape table only; an `error` rule naming an extended scheme is refused as a scheme with no error shape
  until BL-833 to BL-836 add its row. The examples above are the ADR's, with `stage=command` on the path delay.

## Acceptance criteria

- [x] Named `CommandLineParserTests` show: two `--fault` items kept in order with every key the ADR defines; each
      refusal the ADR lists (unknown kind, unknown key, missing required key, bad number) giving `FailedInit` (2)
      and the ADR's text; a `--fault` read from a `-K` file equal to the same item on the command line.
- [x] `surl --help testing` and `surl --aihelp testing` list `--fault`; the `--aihelp` completeness tests pass.
- [x] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Cli` keeps 100% line and branch coverage and no method over complexity 10.

## Notes

- Delivered: `ConfiguredFault` (public record mirroring `FaultRule` without its ID, using the abstractions'
  `FaultKind`/`FaultStage`), `ConfiguredFaultReader` (item read by `ConfigurationItemReader` with a kind head and
  ten value keys, then the ADR's checks) and `FaultReplyShapes` (the original schemes' reply table), `--fault`
  in the option table (`security` and `testing`, with its explanation), `SurlCommandLine.Faults`, and the
  parser's 256-rule bound. Tests: `CommandLineParserFaultTests` (a class of its own beside
  `CommandLineParserGroupTests`, as `--group`'s are), plus the pinned help texts.
- Refusal texts decided and recorded in ADR-0124 decision 4 (the ADR named the cases but not the words);
  touches gained that ADR file. Neither appears in any task in Doing.
- `--fault` is also in the `security` category: `AiHelpFactsTests` requires every `testing` option to be,
  and the security topic's prose now says "the other six".
- `Application/Surl.Mcp.UnitLibrary` added to touches: `CommandLinePathMembersTests` requires every
  `SurlCommandLine` member classified; `Faults` holds no local path. No task in Doing touched it.
- Argument column `<kind>[;<key>=<value>]` (no trailing `...`) so the help row fits the 38-column layout.
- Choices: `scheme` accepts every scheme surl has a server for, extended ones included, whatever
  `SURL_EXTENSIONS` says (the reader is static and a rule for a scheme not served simply never matches);
  `gophers` takes `gopher`'s reply shape; an SMB reply must have NTSTATUS severity error (0xC0000000 and up);
  MQTT takes the reason code alone; `ldap` and `dict` need text after the code as `ftp` does; `listener` is kept
  as written and must parse as a listen URL.
- Coverage: the new classes are 100% line and branch. Four partial branches remain in lines this task did not
  change (`CommandLineParser.RefuseCombination`, `ConfigurationItemReader.ReadHeadValue`, `HelpText` testing
  answer, `OptionArgumentReader` authorized keys); no `--aihelp` example is added since the warning and the
  applied faults come with BL-803.

## Log

- 2026-11-05: Created.
- 2026-11-05: Amended by ADR-0124 (BL-822): reply shapes for original schemes only; extended ones by BL-833 to BL-836.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. surl --fault <kind>[;<key>=<value>] is read, checked and refused per ADR-0124 decision 4 into SurlCommandLine.Faults, from the command line and -K files, and listed by --help/--aihelp testing
