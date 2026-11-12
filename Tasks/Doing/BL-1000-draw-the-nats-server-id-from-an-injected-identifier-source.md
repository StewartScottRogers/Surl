---
id: BL-1000
title: Draw the NATS server_id from an injected identifier source
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-851, BL-1035]
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

- 2026-11-11 (lane 4): code written but not built - `dotnet` cannot resolve the pinned SDK 10.0.401 (BL-1035),
  so the task waits on BL-1035. The shift stashes the uncommitted work; redo it from this outline if lost:
  - `NatsServer` gains a last optional parameter `IIdentifierRandom? identifiers = null`, so
    `Surl.Composition` (held by BL-533, and BL-1001's to change) compiles unchanged.
  - Default when null: an internal `NatsUnseededIdentifierRandom` filling from `RandomNumberGenerator` - the same
    bytes as `Surl.Core`'s `CryptographicIdentifierRandom`, which a protocol server may not reference (ADR-0002;
    `ProtocolIsolationTests` forbids `Surl.Core.UnitLibrary`). `NatsServer` itself no longer calls
    `RandomNumberGenerator`.
  - `internal static string DrawServerId(IIdentifierRandom)`: one byte per character, bytes >= 252 (7 x 36)
    drawn again, `byte % 36` into `ServerIdAlphabet` (made `internal` for the tests).
  - Tests: the test project references `Surl.Core.UnitLibrary` for `SeededIdentifierRandom(seed).ForServer("nats")`;
    `UnitTestByteSequenceIdentifierRandom` cycles chosen bytes. New tests in `NatsServerTests`:
    `ServerId_SameSeededSource_IsTheSameEveryRun`, `ServerId_DifferentSeeds_Differ`,
    `ServerId_SourceGivingEachByteBelowTheLimit_DrawsEveryAlphabetCharacter`,
    `ServerId_ByteOfEachValue_MapsToItsRemainderModuloTheAlphabet`, `ServerId_BytesAtOrAboveTheLimit_AreDrawnAgain`.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds; code is written and stashed
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. returned by the shift starting 20261111-212123: it was in Doing but no stopped lane held it; look for its work in 'git stash list' and in factory/BL-1000-* branches before redoing it
- 2026-11-11: Backlog -> Doing.
