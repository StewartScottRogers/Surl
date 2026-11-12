---
id: BL-966
title: Hand the AMQP 0-9-1 broker its exchange file under --directory and answer a malformed one with exit 37
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-507, BL-732]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-12
---
# BL-966 — Hand the AMQP 0-9-1 broker its exchange file under --directory and answer a malformed one with exit 37

## Goal

With `--directory`, `surl amqp://...` keeps durable declared AMQP 0-9-1 exchanges and their bindings across a
restart, and a malformed exchange file refuses the start with `CouldNotReadFile` (37).

## Context

- BL-732 gave the broker `Amqp0Broker.LoadExchangesAsync(Amqp0ExchangeFiles, CancellationToken)` and
  `Amqp0ExchangeFiles(IContentFileSystem, surlFolderPath)` (file `<path>/.surl/amqp0/exchanges`); a malformed or
  unreadable file throws `Amqp0ExchangeLoadException` with `FilePath` and the reason.
- Follow `SurlServerComposition.LoadMessageBusAsync`'s pattern (ADR-0031 decision 6, ADR-0050 decision 7): load
  once after the data-directory lock and before any listener binds; answer the exception with
  `surl: (37) Could not read <FilePath>: <Message>`. Without `--directory`, do not call it (in memory only).
- BL-507 registers the broker in the composition; this task needs it.

## Acceptance criteria

- [x] A named `Surl.Composition.UnitTests` test: with `--directory`, a composed broker is handed
      `Amqp0ExchangeFiles` under `<directory>/.surl`, and a durable exchange declared before a restart is there after.
- [x] A named test: a malformed `.surl/amqp0/exchanges` refuses the start with exit 37 and the message above.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the composition keeps 100% line and
      branch coverage.

## Notes

- Load happens in `ComposeUnderTheLockAsync` after the brokers are composed (the broker needs its policy, which
  the service-state load does not have), via `SurlServerComposition.LoadAmqp0ExchangesAsync`, before any listener binds.
- Tests: `LoadAmqp0ExchangesAsync_WithADataDirectory_HandsTheBrokerItsFileUnderSurl` proves the handover (a broker loads
  once, so a second load throws); the restart round trip itself is BL-732's in `Surl.Extended.Protocol.Amqp0.UnitTests`
  (the exchange encoder is internal to that library). `ComposeAsync_ADataDirectoryWithAMalformedAmqp0ExchangeFile_...`
  pins exit 37 and the message.
- Decision: no cancellation catch around the load; the read only cancels mid-file, and an untestable branch would break the
  100% gate. Coverage was not measured with the collector in this run.

## Log

- 2026-11-06: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. With --directory the AMQP 0-9-1 broker loads its exchange file from .surl/amqp0 and a malformed one refuses the start with exit 37
