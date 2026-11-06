---
id: BL-852
title: Add --random-seed and SurlServerOptions.RandomSeed and bind the seeded identifier source per instance
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-851]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Api, Application/Surl.Api.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-852 — Add --random-seed and SurlServerOptions.RandomSeed and bind the seeded identifier source per instance

## Goal

`surl --random-seed <n>` and `SurlServerOptions.RandomSeed` make one instance's non-secret identifiers repeat
run after run, and an unseeded instance behaves exactly as today.

## Context

- Decision: ADR-0124 decision 6, "The seed's spelling": `--random-seed <n>` (0 to 2^63-1) on the command line
  and in `-K` files, in the `testing` help category, and `SurlServerOptions.RandomSeed` as its `Surl.Api`
  spelling (ADR-0099 decision 4). No warning; unseeded is the default. Filed by BL-786.
- Depends on BL-851 for `IIdentifierRandom`, `CryptographicIdentifierRandom` and `SeededIdentifierRandom`.
- `Application/Surl.Cli.UnitLibrary`: the option, its `OptionArgumentReading<long>` with its
  `OptionArgumentType`, its `--aihelp` facts (CLAUDE.md "`surl --aihelp` is how an agent learns the command line"),
  the manual line, and an out-of-range or non-numeric value refused with `FailedInit` (2) in the existing option
  error form.
- `Application/Surl.Composition.UnitLibrary`: each `ComposedServing` gets its own source - seeded when the
  setting is present, cryptographic when not - and stamps `ExchangeContext.Identifiers` with
  `ForExchange(exchangeId)`; it also exposes the server-life source the per-server fixes (BL-850) pass to
  constructors. Two instances in one process never share one source.
- `Application/Surl.Api`: `SurlServerOptions.RandomSeed` (`long?`, null by default) flows to the composition.

## Acceptance criteria

- [ ] `surl --random-seed 42 ...` and a `-K` file line `random-seed = 42` are accepted; `--random-seed -1` and
      `--random-seed x` exit `FailedInit` (2) with the existing option-error message form; the `--aihelp`
      completeness tests (`AiHelpFactsTests`, `AiHelpTextTests`) pass with the option on the `testing` page.
- [ ] A composition test shows two `ComposedServing`s with seed 42 stamp equal `ExchangeContext.Identifiers` bytes
      for exchange 1, and an unseeded one stamps the cryptographic source.
- [ ] A `Surl.Api` test shows `SurlServerOptions.RandomSeed` reaches the composition and two servers started side
      by side with different seeds draw different identifier bytes.
- [ ] All three projects stay at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

## Log

- 2026-11-05: Created.
