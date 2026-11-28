---
id: BL-2064
title: Note in ADR-0107 and ADR-0146 what --max-filesize 0 does with held transaction bytes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2044, BL-2063]
touches: [Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2064 — Note in ADR-0107 and ADR-0146 what --max-filesize 0 does with held transaction bytes

## Goal

A reader of ADR-0107 decision 7 and ADR-0146 decision 1.7 learns from dated notes that under `--max-filesize 0` the
bytes held in AMQP 1.0 transactions have no bound.

## Context

- Found while working BL-2044 (2026-11-28). BL-2063 makes `Amqp1ContainerConnection.Transactions.cs`, `Hold`
  (`Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary`), read 0 as no limit (ADR-0006). Describe the code as
  it is when this task runs.
- BL-2044 added a note under ADR-0107 decision 7's table about held transaction bytes and `--max-filesize`. Its third
  bullet ("The held-bytes check compares with the exchange's `MaxUploadBytes` ... does not read 0 as no limit") is out
  of date once BL-2063 is done.
- ADR-0146 decision 1.7 says "the 100-transaction and `--max-filesize` bounds keep what a transaction holds finite".
  Under `--max-filesize 0` only the number of open transactions is bounded (`MaxOpenTransactions`);
  `Amqp1Transaction.Hold` caps neither the number of messages held nor their bytes.
- Add notes in the form BL-1990's and BL-2044's use ("*Amended <date> (BL-###), pointing at ...:*"), each placed after
  the text it corrects, and a bullet in ADR-0107's header list of amendments as BL-2044 did. Recorded text is never
  rewritten.

## Acceptance criteria

- [ ] A dated note after BL-2044's `--max-filesize` note in ADR-0107 decision 7 says its third bullet no longer describes the code, and that under `--max-filesize 0` the held-bytes check applies no bound.
- [ ] A dated note at ADR-0146 decision 1.7 says that under `--max-filesize 0` what a transaction holds has no byte bound, and that the 100-transaction limit bounds only how many are open.
- [ ] ADR-0107's header list of amendments has a bullet for the new note.
- [ ] The diff to both ADRs only adds lines.

## Notes

## Log

- 2026-11-28: Created.
