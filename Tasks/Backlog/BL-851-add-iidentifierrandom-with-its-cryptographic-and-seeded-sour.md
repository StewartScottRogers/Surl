---
id: BL-851
title: Add IIdentifierRandom with its cryptographic and seeded sources and ExchangeContext.Identifiers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-851 — Add IIdentifierRandom with its cryptographic and seeded sources and ExchangeContext.Identifiers

## Goal

Protocol servers can draw non-secret identifiers from a seedable source, `IIdentifierRandom`, that every exchange
carries as `ExchangeContext.Identifiers`, with a cryptographic default and a seeded implementation that gives the
same identifiers every run.

## Context

- Decision: ADR-0124 decision 6 (`Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md`).
  Filed by BL-786's audit; every seedable-identifier fix task (BL-853 to BL-846, BL-850) depends on this one.
- Contract in `Foundation/Surl.Protocol.Abstractions.UnitLibrary`: `public interface IIdentifierRandom
  { void NextBytes(Span<byte> destination); }`, and `ExchangeContext.Identifiers` (an added record parameter or
  init property whose default is the cryptographic source, so every existing `new ExchangeContext(...)` still
  compiles and behaves as today - configuration only grows).
- Implementations in `Foundation/Surl.Core.UnitLibrary`: `CryptographicIdentifierRandom` (stateless,
  `RandomNumberGenerator.Fill`; a static `Instance` is allowed by decision 6) and `SeededIdentifierRandom`
  (constructed from a `long` seed 0 to 2^63-1; `ForExchange(long exchangeId)` returns a source whose byte
  sequence is derived from the seed and the exchange ID only - e.g. SHA-256 in counter mode over seed, exchange
  ID and block counter - so interleaving other exchanges never changes it; and a server-life source for
  identifiers drawn once when a server is created, derived from the seed and a caller-given name such as `"nats"`).
- Never use it for anything secret (keys, nonces, challenges, salts, session or connection tokens): say so in
  the interface's XML documentation.
- No static mutable state; `SeededIdentifierRandom` instances are independent.

## Acceptance criteria

- [ ] `IIdentifierRandom` and `ExchangeContext.Identifiers` exist in `Surl.Protocol.Abstractions` with XML
      documentation naming ADR-0124 decision 6 and the never-secret rule; every existing caller compiles unchanged.
- [ ] `CryptographicIdentifierRandom` and `SeededIdentifierRandom` exist in `Surl.Core`, one public type per file.
- [ ] `SeededIdentifierRandomTests.ForExchange_SameSeedAndExchangeId_GivesTheSameBytesWhateverElseWasDrawn`
      passes: two instances with one seed give equal bytes for exchange 7 even when one of them drew for
      exchanges 1 to 6 first.
- [ ] `SeededIdentifierRandomTests.ForExchange_DifferentSeedsOrExchangeIds_GiveDifferentBytes` and
      `SeededIdentifierRandomTests.ForServer_SameSeedAndName_GivesTheSameBytes` pass.
- [ ] Both libraries stay at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

## Log

- 2026-11-05: Created.
