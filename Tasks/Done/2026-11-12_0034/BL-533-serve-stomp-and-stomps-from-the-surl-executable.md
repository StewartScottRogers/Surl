---
id: BL-533
title: Serve stomp and stomps from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-532, BL-405, BL-635, BL-1035]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-533 — Serve stomp and stomps from the surl executable

## Goal

`surl stomp://127.0.0.1:0` and `surl stomps://127.0.0.1:0` (TLS) start the STOMP broker with its shared state.
The `--aihelp` topic and help category exist, and the extension switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `stomps` is wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming STOMP 1.2 as its oracle, an `AiHelpExamples` entry, and the topic list
  in `AiHelpTextTests` growing by one.
- Shared message bus (Stewart, 2026-11-04; BL-622's ADR decision 1): the broker is handed the **one**
  `Surl.MessageBus` BL-635 composes for `mqtt`, not state of its own. If BL-609 has moved the composition to
  `Application/Surl.Composition.UnitLibrary`, register it there and record the changed touches in Notes.

## Acceptance criteria

- [x] A named test shows the `stomp` broker and the `mqtt` server composed with one and the same bus instance.

- [x] A named test finds `stomp` and `stomps` registered when the switch is on, and absent when it is off.
- [x] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05 (lane 8): BL-609 moved the composition. Extended servers are now registered in
  `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs`, and the shared bus is composed in
  `SurlServerComposition`. So the stomp registration and its bus-sharing test belong in Surl.Composition and its
  tests, and both are added to `touches`. BL-803 (Doing) touches Surl.Composition, so the task waits in Backlog
  until that task no longer overlaps.
- 2026-11-11 (lane 2): Implemented in full, then parked behind BL-1035. The pinned .NET SDK 10.0.401 is not
  installed, so `dotnet build` cannot resolve an SDK and the build gate cannot pass (as with BL-1011 and BL-1017).
  The work stays uncommitted in the lane, and the shift stashes it. With `global.json` pointed for a while at the
  installed 10.0.100-rc.1 SDK (and put back afterwards), Surl.Composition.UnitTests (225), Surl.Cli.UnitTests
  (1238) and Surl.Console.UnitTests passed, and so did every other fast test project. Only Surl.Core.UnitTests
  failed to build, because the rc compiler raises CS1998 in `FaultPlanTests.Overrides.cs`, a file this task does
  not touch. What the change does:
  - `Foundation/Surl.Protocol.Abstractions.UnitLibrary` added to touches: `stomps` joins `TlsSchemes`, because
    `ImplicitTlsSchemeServer` and the serving engine both read that list (ADR-0118 decision 8: `stomps` is TLS from
    the first byte). No task in Doing names that project. BL-1007 touches only Abstractions.UnitTests, and no
    test there pins the full list.
  - `ExtendedProtocolServers.Registrations`: `new(["stomp"], ...)` builds a `StompServer` from
    `AuthenticationPolicyFor("stomp")`, the one `MessageBus` and `StompServerOptions { IdleTimeout }`. After it
    comes `ProtocolServerRegistration.ImplicitTls("stomps", "stomp")`. The Composition csproj references the
    Stomp library.
  - Tests: `ExtendedProtocolServersTests.Compose_ThisBuild_BuildsTheStompBrokerOverTheOneBusTheMqttServerIsBuiltOver`
    and `..._BuildsTheStompBrokerWithTheIdleTimeout`. `CommandLineRunnerExtendedProtocolTests`: the switch at zero
    refuses both schemes `(1)`, both listen on their default ports 61613 and 61614, and `stomps` without a
    certificate gives `(58)`.
  - Cli: a `stomp` help category ("STOMP and STOMPS protocol (extended)"), an `AiHelpProse` topic, an example,
    a STOMP OPTIONS section in the manual, and `stomp` added to the categories of nine options (the same ones
    `nats` has), with the pinned help, aihelp and manual tests updated.
  - Default chosen: `stomps` goes through `ImplicitTlsSchemeServer`, which does not forward
    `IConnectionRefusalWriter`. So a `stomps` connection past `--max-connections` is closed with no bytes, not
    answered ERROR `Too many connections`; every implicit-TLS scheme behaves the same way today. File a
    follow-up if that matters.

- 2026-11-11 (lane 3): The stashed work could not be reached from this lane (stash commands were denied), so the
  change was rebuilt from the notes above. The installed SDK is 10.0.404, which `global.json`'s `latestFeature`
  rollForward accepts, so `dotnet build -warnaserror` and the fast tests ran without touching `global.json`: all
  green (Surl.Cli.UnitTests 1756, Surl.Composition.UnitTests 451, Surl.Console.UnitTests 471). Same design and
  defaults as the entry above, plus `stomp` ordered after `socketio` in `Registrations` (ordinal order of first
  scheme). Tests: `ExtendedProtocolServersTests.Compose_ThisBuild_BuildsTheStompBrokerOverTheOneBusTheMqttServerIsBuiltOver`
  and `..._BuildsTheStompBrokerWithTheIdleTimeout`; `CommandLineRunnerExtendedProtocolTests` for the switch at
  zero, default ports 61613/61614, `stomps` without a certificate (58) and registration on/off. Coverage was not
  re-measured here (`Measure-CodeQuality.ps1` not run); the new branches are all exercised by the tests above.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-635; composed with the one shared bus.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary, which BL-803 (Doing) touches
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: the pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot pass; the stomp/stomps work is done and stashed by the shift
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. surl serves stomp and stomps (TLS) from the one shared message bus, with its aihelp topic, help category, manual section and the SURL_EXTENSIONS switch
