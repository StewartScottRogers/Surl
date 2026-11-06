---
id: BL-810
title: Seed, reset, snapshot and restore a Surl.Api server's mail, files, messages and accounts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-805, BL-808]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-810 — Seed, reset, snapshot and restore a Surl.Api server's mail, files, messages and accounts

## Goal

A .NET test seeds a running `SurlServer` - a message into a mailbox, a file into the content store, a message
onto a topic or queue, an account - resets it between tests, and snapshots and restores it, through a supported
`Surl.Api` surface, so one server can be shared across a test class without tests leaking into each other.

## Context

- Decision: BL-822's ADR decision 3. Planning surface: `SurlServer.Mail.DeliverAsync(address, messageBytes)`,
  `SurlServer.Files.PutAsync(path, bytes)` and `DeleteAsync(path)`, `SurlServer.Messages.PublishAsync(topic,
  payload, retain)` and `EnqueueAsync(queue, payload)`, `SurlServer.Accounts.Add(user, password)` and
  `Remove(user)`, `SurlServer.ResetAsync()`, `SurlServer.SnapshotAsync()` returning an opaque `SurlSnapshot`, and
  `SurlServer.RestoreAsync(snapshot)`. A snapshot from one server restored into another is refused
  (`ArgumentException`) unless the ADR allows it.
- Built on BL-805 (`ComposedServing`'s store control). Each operation's outcome that a peer would see as a refusal
  (unknown mailbox, a `.surl` path, the store full) is a typed result or exception as the ADR says, never silent.
  Each publishes events, so BL-807's waits see seeding.
- `SurlServerOptions.Files` (in-memory content at start, ADR-0099 decision 4) stays; reset returns to it as the
  ADR decides.
- Where: `Application/Surl.Api`. Update the package `README.md` with a "seed, reset, snapshot" section.
- Stand-in: controllable, for .NET tests; `Surl.Mcp` follows in BL-812, the control endpoint in BL-814.

## Acceptance criteria

- [x] Named tests (fake listeners and connections, no network): a message seeded for `alice@x` is fetched over a
      fake POP3 connection; a file seeded at `/a.txt` is fetched over a fake HTTP connection; a retained message
      seeded on `t/1` is delivered to a fake MQTT subscriber; an added account logs in over fake HTTP Basic; after
      `ResetAsync` all four are gone and configured mailboxes remain; snapshot, change, restore gives back the
      snapshot's state; each refusal the ADR names is surfaced as it says.
- [x] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps
      100% line and branch coverage.

## Notes

- Delivered in `Application/Surl.Api`: `SurlServer.Mail` (`SurlServerMail.DeliverAsync`), `Files`
  (`SurlServerFiles.PutAsync`), `Messages` (`SurlServerMessages.PublishAsync`, `EnqueueAsync`), `Accounts`
  (`SurlServerAccounts.Add`, `Remove`), `SurlServer.ResetAsync`, `SnapshotAsync` (opaque `SurlSnapshot`) and
  `RestoreAsync`, each a thin wrapper over BL-805's `ComposedServing.Stores`. README section "Seed, reset, snapshot";
  `CLAUDE.md` updated. Tests: `SurlServerTests.Stores.cs` (16 with data rows), over `FakeListenerFactory`, which
  gained `Accept(scheme, connection)` so a server with POP3, HTTP and MQTT listeners takes each client on its own.
  `Surl.Api` 100% line and branch (Measure-CodeQuality.ps1 -Library Surl.Api); 194 Surl.Api tests green.
- Choice (refusals, the ADR leaves the shape to this task): every store result that is not a success becomes
  `SurlSeedRefusedException : InvalidOperationException` with `Store` (`StoreKind`) and `Refusal` (the store's own
  enum member name, e.g. `NoSuchAccount`, `NotPermitted`, `TooLarge`, `MessageTooLarge`, `InvalidTopic`,
  `UnknownQueue`). Why: a seed must never fail silently, a test should not need the store libraries' result types
  (they are not the supported surface), and the store's own word is already what the protocol reports. Misuse
  (null, a `:` user name, an existing account, another server's snapshot) stays `ArgumentException`/
  `ArgumentNullException`; a reset or snapshot refused for a store on disk is BL-805's `ComposedStoreException`,
  an `InvalidOperationException`, as ADR-0124 decision 3 says.
- Choice: `Accounts.Add(user, password)` makes an account for every scheme, as `SurlServerOptions.Accounts` does,
  and refuses a `:` user name as `--user` could not spell it. Reset keeps the start `Files` (ADR-0124 decision 3),
  proven by `ResetAsync_AfterSeedingAllFour_...` reading `/hello.txt` after the reset.
- Left out, filed as BL-954: `Files.DeleteAsync` (`ComposedStores` has no delete, and `Surl.Composition` is in
  BL-816's `touches`), and a test of a successful `EnqueueAsync` - no queue-declaring server (STOMP, AMQP) is
  composed yet, so no queue exists; its success path shares `ThrowIfRefusedAsync` with `PublishAsync`.
- No ADR written: ADR-0124 decision 3 already decides the behaviour, and `Documentation/Planning/Decisions` is in
  BL-701's `touches`; the refusal shape above is recorded here and in the README.
- "Configured mailboxes remain": `Surl.Api` has no `--mailbox` property yet, so the reset test shows alice's
  configured INBOX still there and empty (`+OK 0 0` over POP3).

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. SurlServer seeds mail, files, retained messages, queue messages and accounts, and resets, snapshots and restores them, refusals as SurlSeedRefusedException
