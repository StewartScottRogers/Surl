# ADR-0168 — How the content store and the mail store learn the exchange behind a write

- **Status:** Accepted
- **Date:** 2026-11-16
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-16, in BL-1239
  (the second stand-in audit).
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) (decision 1: events; decision 4: fault
  scopes), [ADR-0127](ADR-0127-what-the-mail-store-publishes-and-how-it-answers-out-of-space.md),
  [ADR-0129](ADR-0129-what-the-content-store-publishes-and-how-it-runs-out-of-space.md), ADR-0003 and the message
  bus, which already does what this ADR asks of the other two stores.
- **Amends:** ADR-0124 decision 4 (a rule scoped by `listener` or `scheme` now fires on the content store and the
  mail store, as decision 4 always promised), ADR-0127 decisions 2 to 4 (the mail store is told the exchange) and
  ADR-0129's content-change rule (`ExchangeId` is no longer always `null`).
- **Acted on by:** the follow-up tasks in decision 5.

## Context

ADR-0124 decision 4 lets `out-of-space` be scoped by `listener`, `scheme`, `account` and `path`, and the
command-line reader accepts all four. The content store builds its `FaultPoint` from the caller's account, scheme and
exchange ID but never a listen URL, and the mail store builds it from account and mailbox only. So
`out-of-space;listener=ftp://127.0.0.1:2121` and `out-of-space;scheme=smtp` are accepted and never fire: a fault rule
that silently does nothing. Every content and mail `StoreChangedEvent` for a client write also carries
`ExchangeId` null, so `FindEvents` with `Store = Content, Scheme = "ftp"` matches nothing (the event facts reach a
scheme and a listen URL only through the exchange), and an exchange cannot list the stores it changed.

The message bus, the Redis keyspace and the AMQP exchange table already take the exchange and fill `ExchangeId`,
`ListenUrl` and `Scheme`. This ADR gives the other two stores the same.

No byte upstream curl sends or sees changes: the same rules answer the same way, and now also match when scoped by
listener or scheme.

## Decision

1. **The carrier, content store.** `ContentCaller` gains `ListenUrl? ListenUrl` and the factory
   `ContentCaller.ForExchange(ExchangeContext exchange, string? accountName)`: it takes `ExchangeId`, `ListenUrl` and
   `Scheme` from the exchange and is anonymous when `accountName` is `null`. `ForAccount`, `Anonymous` and
   `WithExchange` stay and keep meaning what they did (the last leaves `ListenUrl` null). The fault point and every
   `StoreChangedEvent` and `FaultAppliedEvent` the content store publishes take `ExchangeId` and `ListenUrl` from the
   caller, so `ContentStore` and `ContentUploadSession` need no new parameter on any method. `ContentCaller` stays
   immutable.
2. **The carrier, mail store.** The client-driven mutators of `MailboxStore` (`Deliver`, `DeliverAsync`, `Append`,
   `AppendAsync` and the rest that publish a change: delete, expunge, rename, create, flag changes) gain a trailing
   optional parameter `ExchangeContext? exchange = null`, as the message bus's methods take an `exchange`. A
   supplied exchange fills the fault point's `ExchangeId`, `ListenUrl` and `Scheme` and the published events'
   `ExchangeId`. Existing callers compile unchanged. `Surl.MailStore` already references
   `Surl.Protocol.Abstractions`, so no project reference is added.
3. **Which writes stay exchange-less.** A seed (`ComposedStores`, `--mailbox`, `--file` and their `Surl.Api` and
   control-endpoint forms), a reset, a snapshot and a restore have no exchange and keep publishing `ExchangeId`
   `null` and matching no `listener` or `scheme` rule (an `account` or `path` rule still matches, as today). So does
   any caller that passes `Anonymous(scheme)` or `ForAccount(name, scheme)` without an exchange: it keeps its scheme
   in the fault point, so a `scheme=` rule fires for it, but publishes `ExchangeId` null. A server that passes its
   exchange is what makes the event carry it, which is why the servers are follow-ups.
4. **Matching.** `FaultPlan`'s `MatchesListener` and `MatchesScheme` are unchanged. A rule scoped by `listener`
   still needs a listen URL on the point: the point now has one whenever the write came through an exchange.
5. **The order of follow-ups.** The two store libraries come first, each in its own task; then one task per server
   library that passes its exchange, each depending on its store's task: content servers FTP, SMB, SSH (SCP and
   SFTP), TFTP, HTTP, RTSP, Gopher, S3, Azure Storage, Google Cloud Storage, OCI registry and the MCP server; mail
   servers SMTP, IMAP and POP3. Each server task's tests prove an `out-of-space` rule scoped by `listener=` fires
   for that server's write, and that the published `StoreChangedEvent` carries the exchange's ID.
6. **Amendments.** ADR-0124, ADR-0127 and ADR-0129 each carry an "Amended by" line naming this ADR.
