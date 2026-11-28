---
id: BL-2062
title: Add a dated note to ADR-0146 decision 3.6 on a session-end subscription several sessions hold
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2043, BL-2044]
touches: [Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2062 — Add a dated note to ADR-0146 decision 3.6 on a session-end subscription several sessions hold

## Goal

ADR-0146 says when a `session-end` subscription that more than one session of a connection holds is deleted.

## Context

- BL-2043 (2026-11-28) made `Amqp1ContainerConnection` delete it when the last session holding it ends, shared
  or not (a shared one, or one resumed in another session after a detach). Decision 3.6 says only "keeps the
  subscription ... until that event" and does not say whose session end.
- BL-2043's Notes hold the decision and why, decided by Claude under Stewart's delegation. BL-2043 did not edit
  the ADR because BL-2044 was editing it.
- The rule as the code has it: `Surl.Extended.Protocol.Amqp1.UnitLibrary/CLAUDE.md`, "Durable and shared topic
  subscriptions".

## Acceptance criteria

- [ ] ADR-0146 has a dated note on decision 3.6, marked "Decided by Claude under Stewart's delegation", stating that a `session-end` subscription is deleted when the last session that consumed it ends, shared or not, whether or not a link of that session still consumes it, and why.
- [ ] The note states that once a `closed` detach or a replacement deletes the queue, no session holds that subscription.

## Notes

## Log

- 2026-11-28: Created.
