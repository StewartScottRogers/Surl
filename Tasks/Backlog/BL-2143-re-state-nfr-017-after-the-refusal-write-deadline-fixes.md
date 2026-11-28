---
id: BL-2143
title: Re-state NFR-017 after the refusal write deadline fixes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2126, BL-2137, BL-2138, BL-2139, BL-2140, BL-2141, BL-2142]
touches: [Documentation/Product/Requirements.md]
requirement: NFR-017
created: 2026-11-28
completed:
---
# BL-2143 — Re-state NFR-017 after the refusal write deadline fixes

## Goal

NFR-017 in `Documentation/Product/Requirements.md` states the target the decision records give and a status that names only checks that exist.

## Context

BL-2126 marked the row `Draft`: `ServingEngine` reset a refused connection whose refusal write threw, and no test held the one-second deadline for limit answers of the Redis, NATS, STOMP or MQTT 5 over TCP servers. BL-2137, BL-2139, BL-2140, BL-2141 and BL-2142 change that, and BL-2138 decides the NATS and STOMP slow-consumer deadline, which the target must name if it differs from one second. The section preamble allows `Enforced by` an existing gate, or `Draft` with what it waits on in brackets; a check across every server does not follow from per-server tests, so say what remains if nothing checks it. Stand-in: nothing to add; documentation only.

## Acceptance criteria

- [ ] NFR-017's target agrees with ADR-0006 section 5 and its amendments, the slow-consumer deadline included.
- [ ] NFR-017's status no longer says the engine resets a refused connection or that no test holds the deadline for Redis, NATS, STOMP or MQTT 5.
- [ ] The status is `Enforced by` an existing gate, or `Draft` with what it waits on in brackets, and every test class it names exists.
- [ ] The file's `Last updated` date is that of the change.

## Notes

## Log

- 2026-11-28: Created.
