---
id: BL-822
title: Decide how Surl serves as a stand-in for tests: event stream, extension points, store control, faults, determinism and the control endpoint
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-822 — Decide how Surl serves as a stand-in for tests: event stream, extension points, store control, faults, determinism and the control endpoint

## Goal

One ADR, "Surl is a stand-in for tests", records the principles Stewart set on 2026-11-05 and decides the design
every stand-in task below builds on: the per-instance event stream, the extension points, the seed/reset/snapshot
contract of each shared store, the fault-injection model, determinism, hooks and the loopback control endpoint.

## Context

- **The principles are Stewart's** (root `CLAUDE.md`, section "Surl is a stand-in for tests", commit 263754a5;
  approved as a plan 2026-11-05). The ADR's header says "Decided by: Stewart, 2026-11-05 (the principles);
  Claude under Stewart's delegation (the design)". The principles are recorded, not reopened: embeddable,
  composable, observable, controllable, fault-injectable, deterministic, open for extension, configuration as data,
  and all of it beside secure by default (anything that widens what a peer or test may do is off until asked for,
  and warned).
- **Number:** the next free `ADR-NNNN` under `Documentation/Planning/Decisions/` when this task runs (ADR-0122 is
  the highest on 2026-11-05; lanes add ADRs concurrently, so check again before writing), and its row in that
  folder's `README.md` index.
- **What already exists, built on, not replaced:** `Surl.Composition` (ADR-0099; BL-609, BL-613, BL-614) composes
  one server per `ComposedServing`; `Surl.Api` (`Application/Surl.Api`) records exchanges through an internal
  `RecordingExchangeLogFactory` beside the levelled log (ADR-0099 decision 7, BL-617, Done) and records no listen
  URL because `IExchangeLogFactory.Create` gets none; `Surl.Mcp`'s `get_exchanges` reads that record (ADR-0103
  decision 3, BL-626, Done). The shared stores are ADR-0100's (`ContentStore`, `MailboxStore`, `MessageBus`,
  `AccountBook`), configured by ADR-0101's one configuration model (`SurlCommandLine` in `Surl.Cli`) and ADR-0102's
  meanings. `ExchangeContext` (`Foundation/Surl.Protocol.Abstractions.UnitLibrary/ExchangeContext.cs`) is what
  every server is told about one exchange. `Surl.HttpServing` (`Protocols/Http/Surl.HttpServing.UnitLibrary`) is a
  horizontal HTTP/1.1 serving library that references no protocol server.
- **Planning defaults.** The tasks that wait on this ADR were filed against the defaults below so their `touches`
  are exact. Decide each point by the standing rules; where the ADR departs from a default, amend every waiting
  task still in `Backlog` in the same run (body, `touches`, and a `Log` line naming this ADR), and file follow-ups
  for any already claimed.
  1. **Event stream.** Contract in `Surl.Protocol.Abstractions` (planning names `ISurlEventPublisher`,
     `SurlEvent` and its records `ExchangeStartedEvent` with the listen URL and scheme, `ExchangeBytesEvent`,
     `ExchangeEndedEvent`, `LoginJudgedEvent` with account and scheme and never a secret, `StoreChangedEvent` for
     mail, content, bus and accounts, `FaultAppliedEvent`; `ExchangeContext.Events`) - BL-823. Implementation, a
     bounded `SurlEventStream` with subscribers that never blocks a server, in `Surl.Core`, whose `ServingEngine`
     publishes exchange start, bytes and end - BL-787. One stream per `ComposedServing`, never static - BL-803.
     Publishers: the engine and the four stores (BL-788, BL-789, BL-790, BL-791). Subscribers: `Surl.Api`'s record
     (BL-806) and queries (BL-807), `Surl.Mcp` (BL-809), the control endpoint (BL-814), hooks (BL-811). The levelled
     log and `--trace` stay on `IExchangeLog`. Decide: event ordering and sequence numbers, the bound per subscriber
     and what happens past it, whether bytes are published when nobody subscribes (default: not), and the time
     source (the injected `TimeProvider`).
  2. **Extension points**, each registered explicitly and AOT-safe (no reflection, no scanning): protocol servers
     from a registration list in `Surl.Composition` instead of a switch (BL-804) and a test's own server through
     `Surl.Api` (BL-813); fault kinds a closed set plus hooks (BL-811); stores - decide whether a test may supply its
     own, and say so.
  3. **Seed, reset, snapshot and restore** for each store: what "reset" returns to (default: the configured state -
     configured mailboxes, shares, topics and accounts exist, empty), what a snapshot holds and that it is in memory
     and bounded, what each does with a `--directory` store on disk (decide: allowed, refused, or in-memory only),
     and that each change is published on the stream. Library tasks BL-799 to BL-802, composition BL-805,
     `Surl.Api` BL-810, `Surl.Mcp` BL-812.
  4. **Fault model.** Kinds: delay, drop the connection, refuse a login, answer a chosen protocol error, out of
     space. Scope: listener (listen URL or scheme), account, path (file path, mailbox or topic); a count (`times`)
     or always. Contract `IFaultPlan` with `FaultRule` and `FaultPoint` in `Surl.Protocol.Abstractions` (BL-823),
     on `ExchangeContext.Faults`; matcher and engine-level faults (delay, drop) in `Surl.Core` (BL-792); refuse
     login in `Surl.Authentication` (BL-791); out of space in each store (BL-788 to BL-790); chosen error in each
     server family (BL-793 to BL-798). Configured as data: a repeatable `--fault <item>` in ADR-0101's item grammar
     (so `-K` files carry it), its exact item keys, start-up warning text and refusals (BL-785), `Surl.Api` typed
     spelling and runtime add/remove (BL-808). Decide whether a chosen error is free text or checked against each
     protocol's reply grammar, and what a fault does to a TLS handshake.
  5. **Hooks** (`Surl.Api` only, in-process; BL-811): event callbacks, and response overrides bounded so a hook
     can choose only what a fault rule could (it can never accept a refused login, turn TLS off, reach outside the
     served root or widen any secure default); a time limit per callback; what a throwing hook does.
  6. **Determinism.** `TimeProvider` everywhere; which randomness may be seeded (non-secret choices such as
     generated IDs and names) and which never (keys, nonces, challenges, session tokens); the seed's spelling
     (`Surl.Api` option and command-line option, or `Surl.Api` only); no static mutable state. BL-786 audits the
     code against this decision and files the fixes.
  7. **Loopback control endpoint** for non-.NET test suites: HTTP and JSON, off by default, `--control <listen
     URL>` loopback-only (`127.0.0.0/8`, `::1`, `localhost`), a bearer token from `--control-token-file` (decide
     whether a missing file means refusal or a generated token, and never print a secret), a start-up warning. It
     rides on `Surl.HttpServing` without referencing a protocol server, in a new
     `Application/Surl.Control.UnitLibrary` that defines the interface it drives and that `Surl.Composition`
     implements and binds (BL-814, BL-815, BL-816); System.Text.Json source generation for AOT. Decide the request
     paths, JSON shapes, error answers, body bound, and its `--aihelp` topic name (planning: `control`).
  8. **Surl.Mcp tools** added by BL-809 and BL-812: names, schemas and bounds, as an amendment to ADR-0103
     decision 3 (its header gains an "Amended" line).
  9. **The extended servers.** BL-793 to BL-798 wire the chosen-error fault into the original families only.
     State that every protocol server filed after this ADR wires it in its own task, and file one follow-up per
     already-built extended server family (lanes may file extra tasks; report them).
- No package is needed for any of this (base class library only).

## Acceptance criteria

- [x] `Documentation/Planning/Decisions/ADR-NNNN-surl-is-a-stand-in-for-tests.md` exists with the header above,
      and one numbered decision per Context item 1 to 9, each naming its types, the library each lives in and the
      task that builds it; its "Alternatives considered" includes a central recorder per host (rejected: the
      principle "observable") and a `Surl.Control` that references a protocol server (rejected: isolation).
- [x] The ADR's index row is in `Documentation/Planning/Decisions/README.md`, and ADR-0099 decision 7 and ADR-0103
      decision 3 each carry an "Amended by" line naming the new ADR.
- [x] `Documentation/Product/Requirements.md` gains functional rows for the event stream and queries, store
      control, fault injection, hooks, determinism and the control endpoint, each "Not built yet" naming its tasks.
- [x] `Documentation/Wiki/Glossary.md` defines stand-in, event stream, fault rule, snapshot and control endpoint.
- [x] Every waiting task whose planning default the ADR changed is amended (body, `touches`, `Log` line), and the
      extended-server follow-ups of Context item 9 are filed and listed in this task's Notes.

## Notes

- The ADR is **ADR-0124** (`Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md`); 0123
  was the highest when written. Index row added; ADR-0099 decision 7 and ADR-0103 decision 3 carry "Amended by"
  lines, and their index rows say so.
- Requirements: FR-060 (event stream and queries), FR-061 (store control), FR-062 (faults), FR-063 (hooks),
  FR-064 (determinism), FR-065 (control endpoint), all Draft and "Not built yet" naming their tasks. Glossary:
  new section "Stand-in for tests".
- Planning defaults kept, so most waiting tasks needed no change: planning names, drop-oldest bounded
  subscriptions, bytes only when asked, reset to the configured state, add-only test servers, TELNET `error` as
  one line then close, `--control` loopback `http` only, topic `control`, unreadable token file 37, control
  exchanges not published, wait cap 60000 ms, the `Surl.Mcp` tool names.
- Departures, each amended in its task (body bullet and `Log` line): BL-823 (`IsPublishingBytes`, stream-stamped
  `Sequence`/`At`, `HookFailedEvent`, `IFaultPlan.FindAsync` because hooks may decide asynchronously), BL-792
  (`FindAsync`, stage check, matching rules, TLS), BL-804 (extended servers on the same registration record, and
  an `AdditionalProtocolServers` input for BL-813), BL-811 (async override, 1 s default limit), BL-813 (custom
  listen URLs read by `Surl.Api` with `System.Uri`, no `Surl.Cli` change, so it is not Blocked), BL-785 (reply
  shapes for original schemes only). No `touches` changed for these.
- Choices taken with a sensible default (and why): `--control` without a token file is refused rather than
  generating a token (never print a secret, never write an unasked file); `--directory` stores refuse reset,
  snapshot and restore (a fixture must never empty a real directory); chosen errors are checked against a
  per-scheme shape (a fault stays an answer the protocol allows); `--random-seed` exists on the command line too
  (configuration as data; it widens nothing because only non-secret choices are seeded); stores are not
  replaceable (it would need a rewrite across every server).
- Follow-ups filed (lanes may file extra tasks): BL-833 (S3, Azure Storage, GCS), BL-834 (AMQP 0-9-1, AMQP 1.0,
  Kafka, MQTT 5, NATS), BL-835 (CoAP, MCP, SignalR), BL-836 (DNS, Redis) - each wires the chosen-error fault and
  adds its reply shapes to `Surl.Cli`, depending on BL-823 and BL-785 - and BL-837 (Low, docs), an audit after
  them that files tasks for servers built later (STOMP, XMPP, gRPC, OCI, WebTransport, Socket.IO, SOCKS).

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ADR-0124 decides how Surl serves as a stand-in for tests (event stream, extension points, store control, faults, hooks, determinism, control endpoint, Surl.Mcp tools); FR-060 to FR-065, glossary, six tasks amended, BL-833 to BL-837 filed
