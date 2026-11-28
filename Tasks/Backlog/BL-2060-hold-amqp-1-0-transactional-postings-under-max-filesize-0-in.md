---
id: BL-2060
title: Hold AMQP 1.0 transactional postings under --max-filesize 0 instead of refusing them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2060 — Hold AMQP 1.0 transactional postings under --max-filesize 0 instead of refusing them

## Goal

Under `--max-filesize 0` the AMQP 1.0 container holds the messages posted in a transaction and publishes them at the
commit, as it does under any limit they fit in.

## Context

- Found while working BL-2044 (2026-11-28) by reading the code, not by running it, so start with the failing test.
- `Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1ContainerConnection.Transactions.cs`, `Hold`,
  compares held plus partial plus the posting's bytes with `context.Limits.MaxUploadBytes` and has no `> 0` guard.
  `--max-filesize 0` is no limit (ADR-0006, and `ExchangeLimits`: 0 is no limit), so as read every posting of one byte
  or more is answered `rejected` with `amqp:resource-limit-exceeded`.
- STOMP's equivalent guards it: `StompServerConnection.Routing.cs`, `Hold`, `maxBytes > 0 && ...`.
- The rest of the library was read for the same mistake (2026-11-28) and needs no change:
  - `Amqp1ContainerConnection.Links.cs`, `AnsweringAttach`, sends the link's `max-message-size` as `MaxUploadBytes`,
    so 0 under no limit. OASIS AMQP 1.0 Part 2 section 2.7.3: "If this field is zero or unset, there is no maximum size
    imposed by the link endpoint." No test pins the 0 case.
  - `Amqp1ContainerConnection.Transfers.cs`, `ContinueDeliveryAsync`, compares with the bus's `MaxMessageBytes`, which
    `Surl.Composition`'s `SurlServerComposition` sets to `long.MaxValue` under `--max-filesize 0`.
- With no byte limit, held bytes have no bound, as partial deliveries have none; `MaxOpenTransactions` stays.
- Judged by the specification (ADR-0083); no curl and no Curl port run. No new option, store, event or fault: a test
  sees the posting's `disposition` and the committed messages on the shared bus.
- BL-2061 records the change in ADR-0107 and ADR-0146; do not edit them here.

## Acceptance criteria

- [ ] A new test in `Amqp1ContainerConnectionTests`, `ServeAsync_TransactionalTransfersUnderMaxFilesizeZero_AreHeldAndPublishedAtTheCommit` (`MaxUploadBytes = 0`, two postings then a commit), was seen to fail before the fix and passes: each posting is answered `transactional-state` with `accepted`, and both messages are on the queue only after the commit.
- [ ] A new test, `ServeAsync_AttachAsSenderUnderMaxFilesizeZero_IsAnsweredWithMaxMessageSizeZero`, passes: the answering `attach` has `max-message-size` 0.
- [ ] `ServeAsync_HeldMessagesPastMaxFilesize_AreRejectedWithResourceLimitExceeded` passes unchanged.
- [ ] The library's `CLAUDE.md`, "Local transactions", says held messages have no byte bound when `--max-filesize` is 0.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green, with no test needing `TestCategory=Integration`.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-28: Created.
