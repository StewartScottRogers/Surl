---
id: BL-1661
title: Add the cookie options to the command line, Surl.Api and --aihelp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1660]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: FR-015
created: 2026-11-24
completed: 2026-11-24
---
# BL-1661 — Add the cookie options to the command line, Surl.Api and --aihelp

## Goal

The option(s) BL-1650's ADR names are read by `surl`, documented in `--help` and
`--aihelp`, and reach the HTTP server through `Surl.Composition`.

## Context

Root `CLAUDE.md`, "`surl --aihelp` is how an agent learns the command line": an option is
not finished until its `OptionArgumentType` and topic pages exist. If the `Surl.Api`
spelling or the `Surl.Composition` wiring needs more than this task's two projects, file
it as a follow-up task rather than widening this one.

## Acceptance criteria

- [x] The ADR's option(s) parse with the argument kinds and error texts the ADR states,
      each pinned by a named test in `Surl.Cli.UnitTests`.
- [x] `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues` and
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName`
      pass with the new option(s).
- [x] `surl --help http` lists the option(s).
- [x] Build clean and fast tests green.

## Notes

- `--set-cookie` is in category `http` only, with no `Explanation`: it loosens nothing, and the `testing` pins (`TestingOptionsLoosenNothing_AreExactlyRandomSeed`) and explanation-per-option tests are for loosening options.
- `Surl.Composition` wiring was outside `touches` (BL-1664 holds it): filed BL-1671, and BL-1662 now depends on it. The `Surl.Api` and Surl.Mcp spellings are not filed yet; BL-1671 names them as separate follow-ups.
- The `http` prose says honestly that the server does not set the cookies until BL-1671; BL-1671 must update that sentence.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. surl --set-cookie is read, bounded to 64, shown in --help http and --aihelp http
