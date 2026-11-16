---
id: BL-1260
title: Pass the exchange to the store in the Google Cloud Storage server so its writes match listener and scheme out-of-space rules and publish their exchange (ADR-0168)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1239, BL-1249]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests]
requirement: FR-062
created: 2026-11-16
completed: 2026-11-16
---
# BL-1260 — Pass the exchange to the store in the Google Cloud Storage server so its writes match listener and scheme out-of-space rules and publish their exchange (ADR-0168)

## Goal

The Google Cloud Storage server hands its ExchangeContext to the store (ContentCaller (ContentCaller.ForExchange)), so an out-of-space rule scoped by listener or scheme fires for its writes and its store changes carry the exchange's ID.

## Context

ADR-0168 decision 5 (Documentation/Planning/Decisions/ADR-0168-how-the-content-store-and-the-mail-store-learn-the-exchange-behind-a-write.md). Replace each ContentCaller.Anonymous/ForAccount (or mailbox-store call) in Surl.Extended.Protocol.GoogleCloudStorage with the exchange-carrying form once BL-1249 is Done. No wire byte changes.

## Acceptance criteria

- [x] A unit test proves an `out-of-space;listener=<this server's listen URL>` rule fires for this server's write.
- [x] A unit test proves the published StoreChangedEvent for this server's write carries the exchange's ExchangeId.
- [x] Build clean; fast tests green; the library's quality gates held.

## Notes

GcsLoginJudge (the one place the server builds a ContentCaller) now takes the ExchangeContext and uses ContentCaller.ForExchange. Tests in GcsStoreExchangeTests; harness gained storePublishesEvents; test project references Surl.Core for FaultPlan.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Google Cloud Storage writes pass the exchange to the store: listener-scoped out-of-space rules fire and store changes carry ExchangeId
