---
id: BL-522
title: Serve kafka and kafkas from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-521, BL-405, BL-635]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-522 — Serve kafka and kafkas from the surl executable

## Goal

`surl kafka://127.0.0.1:0` and `surl kafkas://127.0.0.1:0` (TLS) start the Kafka broker with its log, which is
loaded after the data-directory lock if it persists. Metadata advertises the listener's own host and port. The
`--aihelp` topic and help category exist, and the extension switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `kafkas` is wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010). Metadata's broker host and port come from
  the bound listener, so a client reconnecting to the advertised address reaches the same `surl`.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming the Kafka protocol guide as its oracle, an `AiHelpExamples` entry, and
  the topic list in `AiHelpTextTests` growing by one.
- Shared message bus (Stewart, 2026-11-04; BL-622's ADR decision 1): the broker is handed the **one**
  `Surl.MessageBus` BL-635 composes for `mqtt`, whose topic logs (BL-633) it serves, not a log of its own. If
  BL-609 has moved the composition to `Application/Surl.Composition.UnitLibrary`, register it there and record the
  changed touches in Notes.
- ADR-0105 decision 4 adds one option, `--kafka-fetch-compression <none|gzip|snappy|lz4|zstd>` (default `none`,
  help category `kafka`), passed to the broker as the codec of the batches it serves; it needs its
  `OptionArgumentType` and `--aihelp` facts. ADR-0105 decision 2: Metadata advertises the listener's bound address,
  or the connection's local address when the listener is bound to a wildcard address.

## Acceptance criteria

- [ ] A named test shows the `kafka` broker and the `mqtt` server composed with one and the same bus instance.

- [ ] A named test finds `kafka` and `kafkas` registered when the switch is on, and absent when it is off.
      Another shows the advertised broker port equal to the bound port when the listen URL says `:0`.
- [ ] A named test shows `--kafka-fetch-compression zstd` reaching the broker, the default `none`, and another
      value refused as the parser refuses an unknown enumerated argument.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-635; composed with the one shared bus.
- 2026-11-05: Amended by BL-516 for ADR-0105: adds `--kafka-fetch-compression` and the advertised address rule.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and Surl.Composition.UnitTests, which BL-803 in Doing touches; resumes once BL-803 releases them
