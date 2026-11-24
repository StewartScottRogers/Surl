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
completed:
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

- [ ] The ADR's option(s) parse with the argument kinds and error texts the ADR states,
      each pinned by a named test in `Surl.Cli.UnitTests`.
- [ ] `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues` and
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName`
      pass with the new option(s).
- [ ] `surl --help http` lists the option(s).
- [ ] Build clean and fast tests green.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Backlog. Lane 3 could not integrate: fast tests failed twice (Surl.Mcp.UnitTests: Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce; then Surl.Mcp.UnitTests: Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce) after rebasing onto the other lanes' work. The work is on branch factory/BL-1661-lane-3-20261124-122841; start with git cherry-pick --no-commit factory/BL-1661-lane-3-20261124-122841 and fix it.
- 2026-11-24: Backlog -> Doing.
