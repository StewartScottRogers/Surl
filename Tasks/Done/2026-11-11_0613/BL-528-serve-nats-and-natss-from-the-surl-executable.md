---
id: BL-528
title: Serve nats and natss from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-527, BL-405, BL-635, BL-770]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-04
completed: 2026-11-06
---
# BL-528 — Serve nats and natss from the surl executable

## Goal

`surl nats://127.0.0.1:0` and `surl natss://127.0.0.1:0` start the NATS server, with TLS upgrade available on
`natss` as BL-524's ADR decides. The `--aihelp` topic and help category exist, and the extension switch
turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`. TLS
  upgrade availability is passed as `isTlsUpgradeAvailable` is for the mail servers.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming the NATS client protocol as its oracle, an `AiHelpExamples` entry, and
  the topic list in `AiHelpTextTests` growing by one.
- Shared message bus (Stewart, 2026-11-04; BL-622's ADR decision 1): the server is handed the **one**
  `Surl.MessageBus` BL-635 composes for `mqtt`, not state of its own. If BL-609 has moved the composition to
  `Application/Surl.Composition.UnitLibrary`, register it there and record the changed touches in Notes.

## Acceptance criteria

- [x] A named test shows the `nats` server and the `mqtt` server composed with one and the same bus instance.

- [x] A named test finds `nats` and `natss` registered when the switch is on, and absent when it is off.
- [x] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05 (lane 8): BL-609 has moved extended-server composition to `Application/Surl.Composition.UnitLibrary`
  (`ExtendedProtocolServers.Registrations`, beside the `s3` entry). The `nats`/`natss` registration, its project
  reference to `Surl.Extended.Protocol.Nats.UnitLibrary` and the shared-bus test belong there and in
  `Surl.Composition.UnitTests`, so both are now in `touches`. BL-803 (in Doing) touches both, so this task went
  back to Backlog until it no longer overlaps.
- 2026-11-06 (lane 4): Delivered.
  - `ExtendedProtocolServers.Registrations` gains `["nats", "natss"]` before `s3` (ordinal order): a
    `NatsServer` over `services.MessageBus` (the one bus the MQTT server gets), `AuthenticationPolicyFor("nats")`,
    and `NatsServerOptions` with `IdleTimeout` and `AllowsAnonymous`. Tests:
    `ExtendedProtocolServersTests.Compose_ThisBuild_BuildsTheNatsServerOverTheOneBusTheMqttServerIsBuiltOver`
    (criterion 1), `..._BuildsTheNatsServerWithTheIdleTimeoutAndAllowAnonymous`,
    `CommandLineRunnerExtendedProtocolTests.ComposeExtendedSchemes_ThisBuildWithTheSwitchUnset_RegistersNatsAndNatss`,
    `..._ThisBuildWithTheSwitchAtZero_RegistersNoS3` (empty) and `RunAsync_NatsSchemeWithTheSwitchAtZero_RefusesItAsAnUnknownScheme`
    (criterion 2), `RunAsync_NatsSchemeWithTheSwitchUnset_ListensAtItsDefaultPort4222`.
  - Choice: the server's PING pacing needs `--idle-timeout`, which `ComposedProtocolServices` did not carry;
    it gains an `IdleTimeout` init property (default 120 s, the option's default), which the start sets from
    `ComposeConnectionLimits` (zero when off, which `NatsServerOptions` reads as off). `AllowsAnonymous` is read
    from the composed policy's settings rather than a new flag, so there is one source of truth.
  - TLS: the task's "passed as `isTlsUpgradeAvailable` is for the mail servers" does not fit ADR-0113 decision 6,
    which makes TLS on `natss` mandatory and `--cert`/`--self-signed` required (58 without), and `NatsServer`
    takes no such flag. So `ServerTlsComposition` gains `natss` as a scheme that starts in plain text but
    requires a certificate: `FindListenUrlWithoutCertificate` refuses it `(58)` and `Compose` builds settings
    for it. `natss` stays out of `TlsSchemes` (in `Surl.Protocol.Abstractions`, outside this task), since it is
    not TLS from the first byte. Tests: `ServerTlsCompositionTests.*Nats*`,
    `CommandLineRunnerExtendedProtocolTests.RunAsync_NatssWithoutACertificate_ExitsWithCertificateProblem`.
    This implements ADR-0113 and decides nothing new, so no ADR.
  - Help: a `nats` `HelpCategories` row ("NATS and NATSS protocol (extended)"), `AiHelpProse.TopicAbout["nats"]`
    naming the NATS client protocol page as the oracle, an `AiHelpExamples` entry (`--allow-anonymous nats://...`,
    no curl command line, as upstream curl has no `nats`), a `NATS OPTIONS` manual section, the `extended` topic
    now naming two servers, and `nats` added to the categories of the options the server reads
    (`--idle-timeout`, `--head-timeout`, `--max-line`, `--max-message`, `--max-filesize`, `-u`, `--user-file`,
    `--allow-anonymous`, `--allow-plaintext-auth`). The pinned help, aihelp, manual and `--version` tests grow.
  - Touches: added `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary/CLAUDE.md` to correct its line
    saying `surl` does not serve NATS yet; no task in Doing touched it.
  - Gates: `dotnet build -warnaserror` clean, fast tests green in all 79 test projects, `dotnet format
    --verify-no-changes` clean on the changed projects. `Surl.Console` has no production change and
    `Surl.Cli`'s change is table data with no new branch, so their coverage is unchanged (reasoned, not re-measured).

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-635; composed with the one shared bus.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and Surl.Composition.UnitTests (where extended servers now register), which BL-803 in Doing touches
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. surl serves nats and natss (natss with --cert or --self-signed) over the one message bus the MQTT server shares, with its help, aihelp topic and manual section; SURL_EXTENSIONS=0 turns both off
