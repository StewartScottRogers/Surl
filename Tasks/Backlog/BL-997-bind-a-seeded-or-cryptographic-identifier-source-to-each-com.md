---
id: BL-997
title: Bind a seeded or cryptographic identifier source to each composed instance
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-996]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-997 — Bind a seeded or cryptographic identifier source to each composed instance

## Goal

Each `ComposedServing` gets its own identifier source - seeded from `SurlCommandLine.RandomSeed` when present,
cryptographic when not - stamps `ExchangeContext.Identifiers` with `ForExchange(exchangeId)`, and exposes the
server-life source the per-server fixes pass to constructors; two instances in one process never share one source.

## Context

- Second of the three tasks BL-852 was split into (see BL-996).
- Decision: ADR-0124 decision 6. BL-851 built `IIdentifierRandom`, `CryptographicIdentifierRandom` and
  `SeededIdentifierRandom`.
- Where: `Application/Surl.Composition.UnitLibrary` (`ComposedServing`, `SurlServerComposition`). The server-life
  source is derived from the seed and a name per server, so adding a server never shifts another's ID; BL-850 (the
  Kafka cluster ID and NATS `server_id`) waits on it.
- Stand-in: no static mutable state (root `CLAUDE.md`, "Surl is a stand-in for tests"): many instances side by side.

## Acceptance criteria

- [ ] A composition test shows two `ComposedServing`s with seed 42 stamp equal `ExchangeContext.Identifiers` bytes
      for exchange 1, and an unseeded one stamps the cryptographic source.
- [ ] A composition test shows two instances with different seeds, or both unseeded, drawing different bytes, and
      the server-life source exposed for a named server.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
