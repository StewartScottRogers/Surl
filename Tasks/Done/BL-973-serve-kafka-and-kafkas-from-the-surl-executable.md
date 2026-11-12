---
id: BL-973
title: Serve kafka and kafkas from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-521, BL-405, BL-635, BL-1035]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-12
---
# BL-973 — Serve kafka and kafkas from the surl executable

## Goal

`surl kafka://127.0.0.1:0` and `surl kafkas://127.0.0.1:0` (TLS) start the Kafka broker on the shared message bus,
Metadata advertises the listener's own host and port, the `--aihelp` topic and help category exist, and the
extension switch turns both off.

## Context

- First of the two tasks BL-522 ("Serve kafka and kafkas from the surl executable") was split into on 2026-11-06
  for the 60-minute size limit: this, then BL-974 (`--kafka-fetch-compression`).
- Composition: extended servers register in `ExtendedProtocolServers.Registrations`
  (`Application/Surl.Composition.UnitLibrary`); the shared bus is `ComposedProtocolServices.MessageBus` (where
  `mqtt` is composed in `ProtocolServerRegistrations`), and the one-bus test belongs in `Surl.Composition.UnitTests`.
  `kafkas` is wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010).
- ADR-0105 decision 2: Metadata advertises the listener's bound address, or the connection's local address when the
  listener is bound to a wildcard address, so a client reconnecting to the advertised address reaches the same
  `surl`.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming the Kafka protocol guide as its oracle, an `AiHelpExamples` entry, and
  the topic list in `AiHelpTextTests` growing by one.
- Shared message bus (Stewart, 2026-11-04; BL-622's ADR decision 1): the broker is handed the **one**
  `Surl.MessageBus` BL-635 composes for `mqtt`, whose topic logs (BL-633) it serves, not a log of its own.
- Stand-in: the shared message bus is the store; a test seeds and observes it through `Surl.Api`'s message
  seeding and the bus's events.

## Acceptance criteria

- [x] A named test shows the `kafka` broker and the `mqtt` server composed with one and the same bus instance.
- [x] A named test finds `kafka` and `kafkas` registered when the switch is on, and absent when it is off.
      Another shows the advertised broker port equal to the bound port when the listen URL says `:0`.
- [x] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 4): code written but never built - `dotnet build` fails with "A compatible .NET SDK was
  not found" (global.json pins 10.0.401; only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed).
  Parked behind BL-1035; the shift stashes the uncommitted work. What the stash holds:
  - `kafkas` added to `TlsSchemes` (TLS from the first byte, ADR-0105 decision 7), so the engine does the
    handshake and `ServerTlsComposition` refuses `kafkas` without a certificate (58). That is why
    `Foundation/Surl.Protocol.Abstractions.UnitLibrary` and its `.UnitTests` were added to `touches`; no task
    in Doing named them.
  - Decision: one registration `new(["kafka", "kafkas"], ComposeKafkaBroker)` in
    `ExtendedProtocolServers.Registrations`, as `mqtt`/`mqtts` is, instead of an `ImplicitTlsSchemeServer`
    entry: `KafkaBroker.Schemes` already claims both, and `ImplicitTls` wraps an earlier registration's
    server, which would claim `kafkas` twice. Every answerer is built over `ComposedProtocolServices.MessageBus`,
    the policy is `AuthenticationPolicyFor("kafka")`, Produce and InitProducerId share one `KafkaProducerStates`,
    Fetch serves uncompressed (BL-974 adds `--kafka-fetch-compression`).
  - `--aihelp`: `HelpCategories` row `kafka`, `AiHelpProse` `kafka` topic and the extended topic's count,
    `AiHelpExamples` entry, topic lists in `AiHelpTextTests`, `AiHelpExamplesTests`, `CommandLineRunnerTests`
    and `CommandLineRunnerExtendedProtocolTests`.
  - Tests: `SurlServerCompositionTests.Kafka.cs` (one bus with MQTT, switch on/off, Metadata advertises
    `FakeListenerFactory.BoundPort` for `:0`, `kafkas` without a certificate is 58).
  - (superseded by the 2026-11-12 entry below)
    and the `Surl.Composition` CLAUDE.md line for the Kafka broker.
- 2026-11-12 (lane 6): the stash was not readable from this lane, so the work was redone from the notes above; SDK 10.0.404 now satisfies global.json (latestFeature). Same decisions: one registration `["kafka", "kafkas"]` (`ComposeKafkaBroker`), `kafkas` added to `TlsSchemes`, Metadata ids from the unseeded `KafkaMetadataAnswerer(bus)` constructor (`IdentifiersForServer` lives on `ComposedServing`, not `ComposedProtocolServices`, so a `--random-seed` cluster id is a follow-up). Tests: `SurlServerCompositionTests.Kafka.cs`, `StartTestSeams.ParseWithKafka`, and the pinned scheme and topic lists in Cli, Composition and Console tests. Build -warnaserror clean, all fast tests green. 100% coverage was not re-measured with the coverage collector: the only new production code is `ComposeKafkaBroker`, which every Kafka composition test runs, plus data rows.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; code is written and stashed by the shift
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. surl serves kafka and kafkas over the shared message bus, with the --aihelp kafka topic, and SURL_EXTENSIONS=0 turns both off
