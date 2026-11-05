---
id: BL-785
title: Read repeatable --fault items into the configuration model in Surl.Cli
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-05
completed:
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

- [ ] Named `CommandLineParserTests` show: two `--fault` items kept in order with every key the ADR defines; each
      refusal the ADR lists (unknown kind, unknown key, missing required key, bad number) giving `FailedInit` (2)
      and the ADR's text; a `--fault` read from a `-K` file equal to the same item on the command line.
- [ ] `surl --help testing` and `surl --aihelp testing` list `--fault`; the `--aihelp` completeness tests pass.
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Cli` keeps 100% line and branch coverage and no method over complexity 10.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Amended by ADR-0124 (BL-822): reply shapes for original schemes only; extended ones by BL-833 to BL-836.
- 2026-11-05: Backlog -> Doing.
