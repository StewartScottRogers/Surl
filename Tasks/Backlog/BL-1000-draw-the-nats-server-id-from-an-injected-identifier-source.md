---
id: BL-1000
title: Draw the NATS server_id from an injected identifier source
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-851]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-1000 — Draw the NATS server_id from an injected identifier source

## Goal

The NATS `server_id` and `server_name`, drawn once when the server is created, come from an `IIdentifierRandom`
the server is given (defaulting to `CryptographicIdentifierRandom`), so a seeded source announces the same IDs
every run.

## Context

- Second of the three tasks BL-850 was split into (see BL-999).
- Decision: ADR-0124 decision 6 names NATS `server_id` as seedable. Found by BL-786's audit.
- `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary/NatsServer.cs:86` -
  `ServerId = RandomNumberGenerator.GetString(ServerIdAlphabet, ServerIdLength)` in the constructor.
- Keep the shape: 22 characters of the NATS alphabet, drawn with rejection sampling or a modulo-free mapping so the
  alphabet stays uniform.
- Stand-in: a seeded instance announces a repeatable `server_id`.

## Acceptance criteria

- [ ] `NatsServerTests.ServerId_SameSeededSource_IsTheSameEveryRun` passes; different seeds give different IDs; the
      ID is 22 characters of the alphabet, and a test pins that every alphabet character can be drawn.
- [ ] No `RandomNumberGenerator` call is left for the server ID.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-06: Created.
