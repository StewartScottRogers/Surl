---
id: BL-1265
title: Pass the exchange to the store in the POP3 server so its writes match listener and scheme out-of-space rules and publish their exchange (ADR-0168)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1239, BL-1250]
touches: [Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary, Protocols/Mail/Surl.Protocol.Pop3.UnitTests]
requirement: FR-062
created: 2026-11-16
completed:
---
# BL-1265 — Pass the exchange to the store in the POP3 server so its writes match listener and scheme out-of-space rules and publish their exchange (ADR-0168)

## Goal

The POP3 server hands its ExchangeContext to the store (MailboxStore calls), so an out-of-space rule scoped by listener or scheme fires for its writes and its store changes carry the exchange's ID.

## Context

ADR-0168 decision 5 (Documentation/Planning/Decisions/ADR-0168-how-the-content-store-and-the-mail-store-learn-the-exchange-behind-a-write.md). Replace each ContentCaller.Anonymous/ForAccount (or mailbox-store call) in Surl.Protocol.Pop3 with the exchange-carrying form once BL-1250 is Done. No wire byte changes.

## Acceptance criteria

- [ ] A unit test proves an `out-of-space;listener=<this server's listen URL>` rule fires for this server's write.
- [ ] A unit test proves the published StoreChangedEvent for this server's write carries the exchange's ExchangeId.
- [ ] Build clean; fast tests green; the library's quality gates held.

## Notes

## Log

- 2026-11-16: Created.
