---
id: BL-405
title: Register extended protocol servers in surl behind one switch
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-543]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-405 — Register extended protocol servers in surl behind one switch

## Goal

`surl` has one place where extended protocol servers are composed. It has their own default-port table, and
the switch BL-543's ADR names (planning name `SURL_EXTENSIONS=0`) leaves every extended server
unregistered. Every later extended server joins with one line in each place, and the original servers are
untouched.

## Context

- Decision: BL-543's ADR, decisions 6 (the switch), 7 (default ports) and 8 (`--aihelp`).
- `Application/Surl.Console/CommandLineRunner.cs`: `ComposeProtocolServers` lists the original servers, and
  `ComposeUnservedProtocolServers` composes them only to ask for their schemes.
- `Application/Surl.Cli.UnitLibrary/SchemeDefaultPorts.cs` is upstream curl's port table and stays so. The
  extended ports go in a separate table, planning name `ExtendedSchemeDefaultPorts`, that `ListenUrlParser`
  consults only while extensions are on.
- The environment is read through an injected reader, never `Environment.GetEnvironmentVariable` in a static
  (root `CLAUDE.md`: no static service locators).
- No extended server exists yet. The composition is empty, and the tests use a test-only fake extended server
  to show that it is registered when the switch is on and absent when it is off.
- `--aihelp`: if the ADR adds an `extended` topic, it lands here with its `AiHelpProse.TopicAbout` paragraphs,
  and the topic list pinned in `AiHelpTextTests` grows by one (root `CLAUDE.md`, "`surl --aihelp`").

## Acceptance criteria

- [ ] With the switch on (unset), a fake extended server given to the composition is registered and its scheme
      is accepted by the listen-URL parser at its extended default port. With the switch at its "off" value,
      the same scheme is refused exactly as an unknown scheme is, with the `SurlExitCode` the ADR names. Both
      are named tests in `Surl.Console.UnitTests`.
- [ ] Every existing test in `Surl.Console.UnitTests` and `Surl.Cli.UnitTests` passes unchanged, and no
      original scheme's default port changes.
- [ ] The `--aihelp` completeness tests pass (`AiHelpTextTests`, `AiHelpFactsTests`,
      `CommandLineRunnerAiHelpTests`).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
