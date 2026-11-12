---
id: BL-1001
title: Pass each instance's server-life identifier source to the Kafka broker and the NATS server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-999, BL-1000, BL-997, BL-973, BL-528]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-1001 — Pass each instance's server-life identifier source to the Kafka broker and the NATS server

## Goal

`Surl.Composition` hands the Kafka broker and the NATS server the instance's server-life identifier source
(BL-997), so a seeded `surl` announces the same cluster ID and `server_id` every run.

## Context

- Last of the three tasks BL-850 was split into (see BL-999).
- BL-997 exposes the server-life source, derived from the seed and a name per server so adding a server never
  shifts another's ID. Pass it where BL-973 and BL-528 register the broker and server in
  `Application/Surl.Composition.UnitLibrary`.
- Stand-in: repeatable identifiers per seeded instance; no static mutable state.

## Acceptance criteria

- [ ] A composition test shows two compositions with seed 42 announce the same NATS `server_id` and Kafka
      `cluster_id`, and two unseeded ones announce different ones.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-12: Backlog -> Doing.
