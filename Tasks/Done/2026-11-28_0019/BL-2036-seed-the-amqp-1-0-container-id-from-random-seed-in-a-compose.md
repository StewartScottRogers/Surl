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
completed: 2026-11-27
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

- [x] `Surl.Composition.UnitTests`: two compositions with `--random-seed 42` give the same `Amqp1Container.ContainerId`; seeds 42 and 43 give different ones; two unseeded compositions give different ones. The same-seed test failed before the change (or, if it passed, Notes say why this is no defect).
- [x] The `amqp1` registration passes `services.IdentifiersForServer("amqp1")`, and the XML remarks on `Registrations` say the container-id is drawn from it.
- [x] ADR-0107 has a dated note under decision 3 saying a composed container draws its `container-id` from the `amqp1` server-life source (BL-2022), and the diff to the ADR only adds lines.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Done without the planner, implementer and reviewer subagents: the change is one constructor argument that the
  task file already names, so there was nothing to plan. No upstream curl conformance run: upstream curl has no
  AMQP (ADR-0083). No new ADR: ADR-0124 decision 6 already decides it.
- Before the change `Amqp1ContainerId_TwoCompositionsWithSeed42_AreTheSame` failed with two different
  container-ids (the other 7 tests in `ServerLifeIdentifiersCompositionTests` passed); after it the fast tests pass.
- The `(BL-2022)` in the third criterion is this task's ID before commit `81665e792` renumbered it to BL-2036
  (BL-2022 now names an unrelated task), so the ADR note cites BL-2036.
- Also updated to stay true of the code: the XML summary of `ComposedProtocolServices.IdentifiersForServer`, the
  library's `CLAUDE.md`, and ADR-0107's header list of amendments. The ADR diff is 8 lines added, 0 removed.
- Verified 2026-11-27: `dotnet build -warnaserror` 0 warnings; fast tests 30563 passed, 0 failed in 83 projects;
  `dotnet format --verify-no-changes` clean; `Surl.Composition.UnitLibrary` 100% line and 100% branch coverage,
  0 failing members (measured from the Composition, Console and Api test projects).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Composed runs with the same --random-seed send the same AMQP 1.0 container-id
