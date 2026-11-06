---
id: BL-697
title: Amend ADR-0100 decisions 3.3 and 3.6 with the topic-log outcome and rules BL-633 chose
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-633]
touches: [Documentation/Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-697 — Amend ADR-0100 decisions 3.3 and 3.6 with the topic-log outcome and rules BL-633 chose

## Goal

ADR-0100 decisions 3.3 and 3.6 state the topic-log rules and the typed outcome that BL-633 built into
`Surl.MessageBus`, so the Kafka broker (BL-519, BL-520) can map each to its protocol's reply.

## Context

- BL-633's `touches` did not include `Documentation/Planning/Decisions`, so it recorded these choices in its own
  Notes (Tasks, BL-633) and in `Protocols/Messaging/Surl.MessageBus.UnitLibrary/CLAUDE.md`. Read both. Same
  pattern as BL-694 for BL-631's queue outcomes.
- New `MessageBusOutcome.OffsetOutOfRange`: a `MessageBus.ReadLog` offset below the log start offset or above the
  high watermark (Kafka `OFFSET_OUT_OF_RANGE`, error code 1). An offset equal to the high watermark reads nothing,
  successfully.
- Rules: a record counts its payload, key, the UTF-8 of each header name and value, and
  `MessageBus.LogRecordOverheadBytes` (16: offset and timestamp); per-topic retention drops the oldest but always
  keeps the newest record, however large; `MaxTopics` counts topics with a log (every topic published on), so a
  publish on a new topic past it is `TooManyTopics` whether retained or not; past `MaxLogBytes` (after the topic's
  own retention) a publish is `StoreFull`; `ReadLog` returns at least one record whatever `maxBytes` says;
  `BusPublishResult.LogOffset` is the offset a publish got (Kafka Produce's base offset).

## Acceptance criteria

- [ ] The ADR-0100 decision 3.6 outcome table has a row for `OffsetOutOfRange` with Kafka's answer
      (`OFFSET_OUT_OF_RANGE`, 1), and its `StoreFull` and `TooManyTopics` rows say they cover the logs.
- [ ] ADR-0100 decision 3.3's topic-log row or the bullets under it state the record-size rule, the
      keep-the-newest rule and the at-least-one-record read rule, each marked "Decided by Claude under Stewart's
      delegation".

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
