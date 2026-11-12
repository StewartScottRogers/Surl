---
id: BL-996
title: Read --random-seed into SurlCommandLine with its help, manual and aihelp text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-851, BL-1035]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-11
---
# BL-996 — Read --random-seed into SurlCommandLine with its help, manual and aihelp text

## Goal

`surl --random-seed <n>` (0 to 2^63-1) and a `-K` file line `random-seed = <n>` are read into
`SurlCommandLine.RandomSeed` (`long?`, null by default), refused when out of range or not a number, and documented
in `--help`, the manual and `--aihelp` on the `testing` page.

## Context

- First of the three tasks BL-852 ("Add --random-seed and SurlServerOptions.RandomSeed and bind the seeded
  identifier source per instance") was split into on 2026-11-06 for the 60-minute size limit: this (the option),
  BL-997 (each composed instance binds its own source), BL-998 (`Surl.Api`'s `SurlServerOptions.RandomSeed`).
- Decision: ADR-0124 decision 6, "The seed's spelling": `--random-seed <n>` on the command line and in `-K` files,
  in the `testing` help category, no warning, unseeded by default. Filed by BL-786.
- `Application/Surl.Cli.UnitLibrary`: the option row, its `OptionArgumentReading<long>` with its
  `OptionArgumentType`, its `--aihelp` facts (root `CLAUDE.md`, "`surl --aihelp` is how an agent learns the command
  line"), the manual line, and an out-of-range or non-numeric value refused with `FailedInit` (2) in the existing
  option-error form. The help sentence says what the seed makes repeatable; until BL-997 lands no identifier draws
  from it, so word it as what the option sets, not as a guarantee.
- Adding `SurlCommandLine.RandomSeed` makes `Surl.Mcp.UnitTests`'
  `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` fail until it is classified in
  `Application/Surl.Mcp.UnitLibrary` - a one-line entry.
- Stand-in: the seed is how a test makes an instance's non-secret identifiers repeat.

## Acceptance criteria

- [x] `surl --random-seed 42 ...` and a `-K` file line `random-seed = 42` set `SurlCommandLine.RandomSeed` to 42;
      `--random-seed -1` and `--random-seed x` exit `FailedInit` (2) with the existing option-error message form.
- [x] The `--aihelp` completeness tests (`AiHelpFactsTests`, `AiHelpTextTests`) pass with the option on the
      `testing` page, and `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` passes.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Cli.UnitLibrary` keeps 100% line and
      branch coverage.

## Notes

- 2026-11-11: Not started. `dotnet --version` in the checkout fails with "A compatible .NET SDK was not found": global.json pins 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed, so nothing can be built or tested. Added BL-1035 (reinstall the pinned SDK) to depends-on, as BL-507 and BL-975 did.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. --random-seed read into SurlCommandLine.RandomSeed with help, manual, aihelp; Mcp member classified
