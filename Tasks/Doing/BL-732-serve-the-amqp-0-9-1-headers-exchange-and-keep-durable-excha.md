---
id: BL-732
title: Serve the AMQP 0-9-1 headers exchange and keep durable exchanges across restarts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-506]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-732 — Serve the AMQP 0-9-1 headers exchange and keep durable exchanges across restarts

## Goal

The AMQP 0-9-1 broker pre-declares `amq.headers` and `amq.match`, accepts `exchange.declare` of type `headers`,
routes a publish by its `headers` table, and keeps durable declared exchanges and their bindings across a restart
under `--directory`.

## Context

- Decision: ADR-0112 decision 5 (the exchange table's headers row, and "durable exchanges ... persist with
  BL-725") and decision 6's storage paragraph. Matrix rows `Amqp0:Exchange-headers-1` and `Amqp0:Surl-Exchange-1`.
- AMQP 0-9-1 section 3.1.3 (the headers exchange type: a binding's `arguments` table, `x-match` `all` (default) or
  `any`, matched against the message's `headers` property; `x-` entries other than `x-match` are not matched) and
  the XML's `exchange` class rules `recommended-types` and `required-instances` (`amq.match`, and `amq.headers`).
- Persistence follows ADR-0031 decision 6 and ADR-0050 decision 7's rules: a file under `<path>/.surl/amqp0/`
  written through `IContentFileSystem`, loaded once at start after the data-directory lock and before any listener
  binds, a malformed file refusing the start with `CouldNotReadFile` (37); a failed write noted and never ending
  serving; ordinal order so tests can pin the bytes. The broker then references `Surl.Content.UnitLibrary`
  (a horizontal library, ADR-0002 decision 3); the composition (BL-507) hands it the file system. `amq.topic`
  bindings are the bus's and are not in this file.
- Until this task, `exchange.declare` of type `headers` is `503` (ADR-0112 decision 5).

## Acceptance criteria

- [ ] Named tests show: `amq.headers` and `amq.match` present from start; a publish routed by `x-match` `all` and
      by `any`; a durable declared exchange and its bindings present after a save and load; a non-durable one gone;
      a malformed file refusing the start with exit 37.
- [ ] The matrix rows name the tests, and ADR-0112 decision 5's "until BL-725" sentences are updated.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-503 (ADR-0112), split from BL-506 so each task is one run.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
