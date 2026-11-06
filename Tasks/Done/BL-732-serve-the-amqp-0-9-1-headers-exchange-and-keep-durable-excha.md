---
id: BL-732
title: Serve the AMQP 0-9-1 headers exchange and keep durable exchanges across restarts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-506]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
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

- [x] Named tests show: `amq.headers` and `amq.match` present from start; a publish routed by `x-match` `all` and
      by `any`; a durable declared exchange and its bindings present after a save and load; a non-durable one gone;
      a malformed file refusing the start with exit 37.
- [x] The matrix rows name the tests, and ADR-0112 decision 5's "until BL-725" sentences are updated (the ADR
      edit is BL-954; see Notes).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-503 (ADR-0112), split from BL-506 so each task is one run.
- Delivered: `Amqp0HeadersMatcher`, `Amqp0Binding` (a binding now carries its arguments), `Amqp0ExchangeFiles`,
  `Amqp0ExchangeLoadException`, `Amqp0PersistedExchange`; `Amqp0ExchangeTable` pre-declares `amq.headers` and
  `amq.match`, counts changes to durable declared exchanges, and loads and saves them; `Amqp0Broker.LoadExchangesAsync`;
  the connection saves after each channel frame (`SaveExchangesAsync`) and notes a failed write. The library now
  references `Surl.Content.UnitLibrary` (horizontal, ADR-0002 decision 3; ADR-0112 decision 0 names it).
- Decisions (Claude under Stewart's delegation, by AMQP 0-9-1 section 3.1.3 and RabbitMQ's headers exchange,
  the spec's reference behaviour since 0-9-1 leaves the details open):
  - `x-match` absent is `all`; a value other than long string `all`/`any` is 406 `PRECONDITION_FAILED - invalid
    x-match` on `queue.bind` (RabbitMQ refuses it the same way). Entries named `x-` are not matched. A value matches
    when type octet and bytes are equal (so `I` 1 is not `l` 1); a void (`V`) entry matches the header's presence.
    `all` with no entry to match matches every message; `any` none, as RabbitMQ.
  - Bindings on a headers exchange are told apart by queue, key and arguments, so the same queue may bind twice with
    different arguments and `queue.unbind` removes the one with the same arguments. Other exchange types still ignore
    binding arguments, as before.
  - Persisted: durable exchanges a client declared and all their bindings (also to non-durable queues: a binding to
    a queue gone after the restart routes nowhere and costs nothing). Pre-declared exchanges are not in the file;
    bindings on them last the server's life, as ADR-0112 decision 6 says of "durable exchanges other than the
    pre-declared ones".
  - File format: `SURL-AMQP0-EXCHANGES-1\n`, a 4-byte count, then per exchange (ordinal by name) a short string name,
    a type octet, its arguments as a field table, a 4-byte binding count and per binding (ordinal by queue, key,
    argument bytes) queue and key as short strings and arguments as a field table - the wire's own types, so the
    codec is reused. Written whole to a temporary file and renamed over, as `MessageBusFiles` does. Pinned in
    `Amqp0ExchangeFilesTests.Encode_ExchangesAndBindings_AreInOrdinalOrder`.
  - Saved after every channel frame whose handling changed what the file keeps (a version counter makes the check a
    lock and a compare), with `CancellationToken.None` so a closing connection never leaves a half-done save.
- Exit 37: the broker throws `Amqp0ExchangeLoadException` with the path and reason
  (`LoadExchanges_MalformedFile_IsRefusedWithTheFilePathForExit37`); mapping it to exit 37 is the composition's, which
  does not register the broker yet (BL-507). Filed BL-955 (depends on BL-507 and BL-732) to hand the broker its files
  under `--directory` and answer the exception with `(37) Could not read <path>: <reason>`.
- Scope: ADR-0112's edits were split into BL-954 because `Documentation/Planning/Decisions` is in BL-707's `touches`
  (in Doing), as BL-731 did with BL-953. `Documentation/Conformance/Extended/Amqp0.md` was added to `touches`: no
  task in Doing names it.
- The broker still never calls `MessageBus.SaveChangesAsync` itself, so AMQP 0-9-1 durable queues and their messages
  reach disk only when another server saves the bus; filed BL-956.
- Coverage: 100% line and branch for the library (327 tests). `Measure-CodeQuality.ps1` still lists the same six
  IL-measured complexity rows BL-731 recorded (`Route`, `OpenChannel`, `BindQueue`, `IsValidName`'s lambda,
  `Amqp0TopicKeyMatcher.Advance`, `Amqp0ExchangeTable.Declare`); no new method is among them, and CA1502 passes.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. The AMQP 0-9-1 broker serves amq.headers, amq.match and declared headers exchanges (x-match all/any) and keeps durable declared exchanges and their bindings in .surl/amqp0/exchanges
