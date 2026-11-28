---
id: BL-2036
title: Seed the AMQP 1.0 container-id from --random-seed in a composed run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1990]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2036 — Seed the AMQP 1.0 container-id from --random-seed in a composed run

## Goal

Two composed runs with the same `--random-seed` send the same AMQP 1.0 `container-id`.

## Context

- Found while working BL-1990 (2026-11-27) by reading the code; nothing was run, so prove it with a failing test
  first.
- ADR-0124 decision 6 names AMQP 1.0 container IDs among what may be seeded.
- The `amqp1`/`amqp1s` entry of `ExtendedProtocolServers.Registrations`
  (`Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs`) builds `Amqp1Container` without its
  `identifiers` argument. The NATS and XMPP entries and `ComposeKafkaMetadataAnswerer` pass
  `services.IdentifiersForServer("<name>")`. Pass `services.IdentifiersForServer("amqp1")`.
- `Amqp1Container.ContainerId` is public. The seeded and unseeded composition tests to copy are in
  `Application/Surl.Composition.UnitTests/ServerLifeIdentifiersCompositionTests.cs`.
- BL-1990's dated note under ADR-0107 decision 3's `open` table says `Surl.Composition` gives the container no
  identifier source. Add a further dated note there; recorded text is never rewritten.
- Stand-in: determinism only. No store, event or fault changes.

## Acceptance criteria

- [ ] `Surl.Composition.UnitTests`: two compositions with `--random-seed 42` give the same `Amqp1Container.ContainerId`; seeds 42 and 43 give different ones; two unseeded compositions give different ones. The same-seed test failed before the change (or, if it passed, Notes say why this is no defect).
- [ ] The `amqp1` registration passes `services.IdentifiersForServer("amqp1")`, and the XML remarks on `Registrations` say the container-id is drawn from it.
- [ ] ADR-0107 has a dated note under decision 3 saying a composed container draws its `container-id` from the `amqp1` server-life source (BL-2022), and the diff to the ADR only adds lines.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
