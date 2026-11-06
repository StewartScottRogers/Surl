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
completed:
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

- [ ] Named tests (fake listeners and connections, no network): a message seeded for `alice@x` is fetched over a
      fake POP3 connection; a file seeded at `/a.txt` is fetched over a fake HTTP connection; a retained message
      seeded on `t/1` is delivered to a fake MQTT subscriber; an added account logs in over fake HTTP Basic; after
      `ResetAsync` all four are gone and configured mailboxes remain; snapshot, change, restore gives back the
      snapshot's state; each refusal the ADR names is surfaced as it says.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
