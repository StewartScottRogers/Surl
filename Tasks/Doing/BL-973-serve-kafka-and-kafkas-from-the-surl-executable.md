---
id: BL-973
title: Serve kafka and kafkas from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-521, BL-405, BL-635]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
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

- [ ] A named test shows the `kafka` broker and the `mqtt` server composed with one and the same bus instance.
- [ ] A named test finds `kafka` and `kafkas` registered when the switch is on, and absent when it is off.
      Another shows the advertised broker port equal to the bound port when the listen URL says `:0`.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
