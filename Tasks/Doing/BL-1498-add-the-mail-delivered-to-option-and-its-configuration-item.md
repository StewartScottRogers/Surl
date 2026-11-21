---
id: BL-1498
title: Add the --mail-delivered-to option and its configuration item
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1487]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1498 — Add the --mail-delivered-to option and its configuration item

## Goal

`surl --mail-delivered-to` parses as a boolean mail-store setting and is a field of the one configuration model (command line, `-K` file), default off.

## Context

ADR-0053 Amendment 1 decision 1 (decided in BL-1487): the option is off by default, a boolean "like `--mail-catch-all`'s siblings", needs no start-up warning, and defaults keep today's bytes. Model the work on the existing mail routing options in `Application/Surl.Cli.UnitLibrary`: `CommandLineOptions.cs`, `CommandLineParser.cs`, `SurlCommandLine.cs`, `ConfigurationItemValueKind.cs`, `ConfiguredMailCatchAll*.cs`. This task adds the parsed value and configuration item only; the mail store, SMTP server and composition wiring are BL-1500, BL-1501 and BL-1502, and the long prose in `AiHelpProse` and the manual is BL-1497. Do whatever the existing completeness tests force so the build is green (`WithArgument<T>` with its `OptionArgumentType`, the option on the page of each of its categories, a `--help` line), with minimal text.

## Acceptance criteria

- [ ] `--mail-delivered-to` is a registered option with an `OptionArgumentType` and appears in `--aihelp all` and on the topic page of each of its categories; `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues` and `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName` pass.
- [ ] A new `CommandLineParser` test shows the parsed command line carries `DeliveredTo` true with the option and false without it.
- [ ] The same field is settable from a `-K` file and listed by the configuration items that `ConfiguredMailCatchAll`'s siblings are, with a test.
- [ ] Giving the option twice or with a value it does not take fails with the same `SurlExitCode` the sibling mail boolean options use, with a test.
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean and the `Surl.Cli.UnitTests` fast tests pass at 100% line and branch coverage for the new code.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
