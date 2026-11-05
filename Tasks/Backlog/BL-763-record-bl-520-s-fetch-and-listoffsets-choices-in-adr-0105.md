---
id: BL-763
title: Record BL-520's Fetch and ListOffsets choices in ADR-0105
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-520]
touches: [Documentation/Planning/Decisions/ADR-0105-how-the-kafka-broker-answers-kafka-and-kafkas.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-763 — Record BL-520's Fetch and ListOffsets choices in ADR-0105

## Goal

ADR-0105 carries a dated BL-520 amendment recording the Fetch and ListOffsets choices BL-520 made
where the ADR and the Kafka protocol guide leave room.

## Context

BL-520 could not edit ADR-0105 itself: BL-452 held `Documentation/Planning/Decisions` in Doing at
the time. The choices are listed under BL-520's Notes (in `Tasks/Done`) and are implemented in
`KafkaFetchAnswerer`, `KafkaListOffsetsAnswerer`, `KafkaFetchableTopic` and
`KafkaRecordBatchWriter` (`Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary`). Copy them
into a section headed `## Amendment, 2026-11-05 (BL-520)`, marked "Decided by Claude under Stewart's
delegation", in the style of the BL-519 amendment, true of the code as it is.

## Acceptance criteria

- [ ] ADR-0105 has an `Amendment, 2026-11-05 (BL-520)` section stating each choice under BL-520's
      Notes, and each statement is true of the Kafka library's code.

## Notes

## Log

- 2026-11-05: Created.
