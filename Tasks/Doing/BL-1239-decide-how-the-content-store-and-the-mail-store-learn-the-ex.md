---
id: BL-1239
title: Decide how the content store and the mail store learn the exchange behind a write, so listener- and scheme-scoped out-of-space rules match and store changes carry their exchange
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-062
created: 2026-11-16
completed:
---
# BL-1239 — Decide how the content store and the mail store learn the exchange behind a write, so listener- and scheme-scoped out-of-space rules match and store changes carry their exchange

## Goal

An ADR, decided by Claude under Stewart's delegation, says how a client's write reaches the content store and the mail store with its exchange (ID, listen URL, scheme), so an `out-of-space` rule scoped by `listener` or `scheme` fires as ADR-0124 decision 4 promises, and each `StoreChangedEvent` and `FaultAppliedEvent` those stores publish for a client write carries its `ExchangeId`. The follow-up tasks are filed.

## Context

Found by the second stand-in audit (2026-11-16). ADR-0124 decision 4 lets every fault kind be scoped by `listener`, `scheme`, `account` and `path`, and `Surl.Cli`'s `ConfiguredFaultReader` accepts all four on `out-of-space` (`ScopeKeys`). The two biggest stores cannot match some of them, so such a rule is accepted and then never fires, which breaks the rule that a fault rule never silently does nothing:

- **Content store.** `Foundation/Surl.Content.UnitLibrary/ContentStoreEventsAndFaults.cs` `RunsOutOfSpaceAsync` builds `FaultPoint(StoreWrite) { Path, Scheme, Account, ExchangeId = caller.ExchangeId }` with no `ListenUrl`, so `out-of-space;listener=ftp://127.0.0.1:2121` never matches (`Foundation/Surl.Core.UnitLibrary/FaultPlan.cs` `MatchesListener` needs a point listen URL). `ContentCaller` has `ExchangeId` and `WithExchange(long)`, but no production code calls `WithExchange` (only `ContentCaller.Anonymous(scheme)` / `ForAccount(name, scheme)` in FTP, SMB, SSH, TFTP, HTTP, RTSP, Gopher, S3, Azure, GCS, OCI and MCP), so every content `StoreChangedEvent` (`ContentStore.cs` lines 745, 872, 910, 936, 1031; `ContentUploadSession.cs` 223) and every content out-of-space `FaultAppliedEvent` has `ExchangeId` null. ADR-0129 says "a later task may carry it through `ContentCaller`".
- **Mail store.** `Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.Events.cs` asks `FaultPoint(StoreWrite) { Account = owner, Path = mailbox }` only, so `out-of-space;scheme=smtp` and `out-of-space;listener=...` never match a delivery or an `APPEND`; `PublishMessage`, `PublishMailbox` and both `FaultAppliedEvent`s publish `ExchangeId: null`. ADR-0127 decisions 2 to 4 chose that ("the store is not told which exchange called it").
- **Consequence for observing.** `Application/Surl.Api/SurlEventFacts.cs` matches a query's `Scheme`/`ListenUrl` on a store change through its exchange, so `FindEvents`/`WaitForEventAsync` with `Store = Content, Scheme = "ftp"` never matches, and `ExchangeFacts` cannot list the stores an FTP or SMTP exchange changed. The same holds for `Surl.Mcp`'s `get_events` and the control endpoint's `GET /v1/events`.
- **Already right, to copy from:** the message bus (`Protocols/Messaging/Surl.MessageBus.UnitLibrary/MessageBus.Events.cs` line 96 takes an `exchange` and fills `ExchangeId`, `ListenUrl`, `Scheme`), the Redis keyspace (`RedisKeyspaceCommand.cs` line 64) and the AMQP 0-9-1 exchange table.
- Questions the ADR answers:
  1. The carrier: does `ContentCaller` gain the listen URL (for example a `ContentCaller.ForExchange(ExchangeContext, account)` factory), and does `MailboxStore`'s `Deliver`/`Append` take an optional exchange record as the bus's methods do?
  2. Which seeds stay exchange-less (a seed through `ComposedStores` has no exchange and must keep publishing `ExchangeId` null).
  3. The amendments: ADR-0127 decisions 2 to 4, ADR-0129's content-change rule, and ADR-0124 decision 4 (header "Amended by").
  4. The order of follow-ups: the two store libraries first, then one task per server family that passes its exchange (the content servers listed above; SMTP, IMAP, POP3).
- No wire byte changes: the same rules answer the same way; they now also match when scoped by listener or scheme. State that in the ADR.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/`, with the next free number, marked "Decided by Claude under Stewart's delegation", answers questions 1 to 4 and states that no byte upstream curl sees changes.
- [ ] ADR-0124, ADR-0127 and ADR-0129 each carry an "Amended by" line naming the new ADR.
- [ ] One feature task per library the ADR changes is filed with `task-board.ps1 new`, each depending on this task, with exact `touches`: the store libraries (`Surl.Content.UnitLibrary`, `Surl.MailStore.UnitLibrary`) first, then one per server library, each depending on its store's task. Each is listed in Notes.
- [ ] Each server follow-up's acceptance criteria name a unit test proving an `out-of-space` rule scoped by `listener=` fires for that server's write, and a test proving the published `StoreChangedEvent` carries the exchange's ID.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
