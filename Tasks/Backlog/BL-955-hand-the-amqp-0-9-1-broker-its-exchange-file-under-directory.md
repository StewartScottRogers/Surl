---
id: BL-955
title: Hand the AMQP 0-9-1 broker its exchange file under --directory and answer a malformed one with exit 37
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-507, BL-732]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-955 — Hand the AMQP 0-9-1 broker its exchange file under --directory and answer a malformed one with exit 37

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

- [ ] A named `Surl.Composition.UnitTests` test: with `--directory`, a composed broker is handed
      `Amqp0ExchangeFiles` under `<directory>/.surl`, and a durable exchange declared before a restart is there after.
- [ ] A named test: a malformed `.surl/amqp0/exchanges` refuses the start with exit 37 and the message above.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the composition keeps 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-06: Created.
