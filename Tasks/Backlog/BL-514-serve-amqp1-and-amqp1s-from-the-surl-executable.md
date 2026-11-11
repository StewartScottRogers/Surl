---
id: BL-514
title: Serve amqp1 and amqp1s from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-513, BL-405, BL-635, BL-764, BL-1035]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-514 — Serve amqp1 and amqp1s from the surl executable

## Goal

`surl amqp1://127.0.0.1:0` and `surl amqp1s://127.0.0.1:0` (TLS) start the AMQP 1.0 container with its shared
nodes. The `--aihelp` topic and help category exist, and the extension switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `amqp1s` is wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming OASIS AMQP 1.0 as its oracle, an `AiHelpExamples` entry, and the topic
  list in `AiHelpTextTests` growing by one.
- Shared message bus (Stewart, 2026-11-04; BL-622's ADR decision 1): the container is handed the **one**
  `Surl.MessageBus` BL-635 composes for `mqtt`, not state of its own. If BL-609 has moved the composition to
  `Application/Surl.Composition.UnitLibrary`, register it there and record the changed touches in Notes.

## Acceptance criteria

- [ ] A named test shows the `amqp1` container and the `mqtt` server composed with one and the same bus instance.

- [ ] A named test finds `amqp1` and `amqp1s` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05 (lane 4): BL-609 moved the server composition to `Application/Surl.Composition.UnitLibrary`: extended
  servers register in `ExtendedProtocolServers.Registrations`, the shared bus is `ComposedProtocolServices.MessageBus`,
  and the one-bus test belongs in `Surl.Composition.UnitTests`. Both are added to `touches`. BL-803 (in Doing)
  touches the same projects, so the task returned to Backlog until BL-803 releases them. No code was written.
- 2026-11-11 (lane 2): Code written and left uncommitted for the shift to stash; the task waits on BL-1035 because
  `dotnet build` cannot resolve the pinned SDK 10.0.401, so the build-and-test gate cannot run with the pinned toolchain.
  - **Touches widened** to `Foundation/Surl.Protocol.Abstractions.UnitLibrary` and `.UnitTests` (no task in Doing
    named them): `amqp1s` joins `TlsSchemes` so the engine does the TLS handshake. ADR-0107 decision 7 says "wrapped
    in `ImplicitTlsSchemeServer` ... as `mqtts` is"; `mqtts` is actually one registration with its plain twin plus a
    `TlsSchemes` row, so `amqp1s` follows `mqtts` (and `s3`): one `["amqp1", "amqp1s"]` registration, no wrapper.
    The effect is what the ADR asks for.
  - **Composition** (`ExtendedProtocolServers.Registrations`): `Amqp1Container` over `services.MessageBus`,
    `AuthenticationPolicyFor("amqp1")`, `Amqp1ContainerOptions { IdleTimeout }` and
    `Amqp1SecurityNegotiationOptions { AllowsAnonymous = policy's AllowAnonymous, CanUpgradeToTls = IsTlsUpgradeAvailable }`.
    `amqp1` is added to `ServerTlsComposition.UpgradableSchemes`, so with `--cert`/`--self-signed` the listener has
    TLS settings for the TLS header (ADR-0107 decision 1). Without a certificate, `amqp1` starts and `amqp1s` exits 58.
  - **`--aihelp`**: `amqp1` help category ("AMQP 1.0 protocol (extended)"), an `AiHelpProse.TopicAbout` naming
    OASIS AMQP 1.0 as its oracle, an `--allow-anonymous` example, the `extended` paragraph naming four servers,
    and an `AMQP 1.0 OPTIONS` manual section. The `amqp1` category was added to `--idle-timeout`, `--head-timeout`,
    `--max-message`, `-u`, `--user-file`, `--allow-anonymous`, `--allow-plaintext-auth` and `--auth`.
  - **Tests**: `ExtendedProtocolServersTests.Compose_ThisBuild_BuildsTheAmqp1ContainerOverTheOneBusTheMqttServerIsBuiltOver`
    (criterion 1), `CommandLineRunnerExtendedProtocolTests.ComposeExtendedSchemes_ThisBuildWithTheSwitchUnset_ListsDnsOnceThoughItHasAUdpAndATcpServer`
    and `..._ThisBuildWithTheSwitchAtZero_RegistersNeitherAmqp1NorAmqp1s` (criterion 2), plus the options, default-port,
    switch-off and 58 tests.
  - **Unofficial check**: with `global.json` set aside for the run only (restored, unchanged), SDK 11.0.100 built the four
    changed test projects with no errors. Their fast tests passed: Cli 1238, Composition 227, Abstractions 514, Console 417.
    The boxes stay unticked until the pinned SDK confirms them. To resume: run `dotnet build -warnaserror` and the fast
    tests, measure coverage for `Surl.Console`/`Surl.Cli`, tick the boxes and commit.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-635; composed with the one shared bus.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and Surl.Composition.UnitTests, which BL-803 in Doing touches; resumes once BL-803 releases them
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so the build/test gate cannot run; code is written and uncommitted
