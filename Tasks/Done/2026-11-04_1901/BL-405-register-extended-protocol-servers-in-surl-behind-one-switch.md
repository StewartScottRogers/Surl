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
completed: 2026-11-04
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

- [x] With the switch on (unset), a fake extended server given to the composition is registered and its scheme
      is accepted by the listen-URL parser at its extended default port. With the switch at its "off" value,
      the same scheme is refused exactly as an unknown scheme is, with the `SurlExitCode` the ADR names. Both
      are named tests in `Surl.Console.UnitTests`.
- [x] Every existing test in `Surl.Console.UnitTests` and `Surl.Cli.UnitTests` passes unchanged, and no
      original scheme's default port changes.
- [x] The `--aihelp` completeness tests pass (`AiHelpTextTests`, `AiHelpFactsTests`,
      `CommandLineRunnerAiHelpTests`).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep
      100% line and branch coverage.

## Notes

- Built as ADR-0083 decisions 6 to 8 say; the choices below are defaults, so no new ADR.
  - `Surl.Cli`: `ExtendedSchemeDefaultPorts` holds decision 7's table row for row (35 schemes;
    `mcp+stdio` has no row, because its listen URL names no port and the MCP server's ADR decides
    its form). `ListenUrlParser.Parse(argument, extendedSchemes)` and
    `CommandLineParser.Parse(arguments, extendedSchemes)` accept a scheme only when it is in
    upstream curl's table, or is both claimed by a registered extended server and in the extended
    table. The one-argument overloads pass none, so no existing caller changes.
    `VersionText.Compose` has an `extendedSchemes` overload that writes `Extended: ...` after
    `Protocols:`, and nothing when there are none.
  - `Surl.Console`: `ExtendedProtocolServers` is the one place: the switch `SURL_EXTENSIONS`
    (`0` = off) and `Compose`, which is empty today. `ExtendedProtocolServerServices` carries the
    content store, the policy and the TLS-upgrade flag. `CommandLineRunner` takes two optional
    seams, `IEnvironmentVariableReader` (`ProcessEnvironmentVariableReader` by default) and
    `composeExtendedProtocolServers`. It reads the switch once, at construction, and never calls
    the composer while the switch is off.
  - `--aihelp`: the `extended` topic (about text per decision 8), the exit-code row for `(1)`
    tagged `extended`, and one example: `surl s3://127.0.0.1:0/` refused with `(1)`. That stays
    true until the first extended server lands. That server's task must change the example in the
    same diff, or `RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` fails.
- Choice: the parser accepts an extended scheme only when a registered extended server claims
  it, not every row of the extended table. With no server built, an extended scheme is still
  refused with `(1)`, and "every scheme a listen URL may name has a registered server" stays true.
- Choice: `AiHelpText.DefaultPort` still reads upstream curl's table only. No extended topic has
  schemes yet, so a fallback to the extended table would be an uncovered branch. The first
  extended server's task adds it along with its own topic.
- Tests changed for the new topic, as the root `CLAUDE.md` requires: the topic list and table in
  `AiHelpTextTests`, `TopicsAddedBesideTheCategories` in `AiHelpFactsTests`, and the example order
  in `AiHelpExamplesTests`. `CommandLineRunnerAiHelpTests` now also checks extended schemes against
  the topics. No original scheme's default port changed.
- Verified: build `-warnaserror` clean; `dotnet format --verify-no-changes` clean on Application;
  fast tests green (Surl.Cli.UnitTests 765, Surl.Console.UnitTests 418, no failures in any
  project). Measure-CodeQuality: Surl.Cli.UnitLibrary and Surl.Console are at 100% line and
  branch coverage with 0 failing members.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. surl composes extended servers in ExtendedProtocolServers behind SURL_EXTENSIONS, with their own default-port table, an Extended: --version line and the extended --aihelp topic
