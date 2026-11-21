---
id: BL-1562
title: Publish the login on an abandoned body check in the authentication session (ADR-0045 amendment 1)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1553, BL-1561]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1562 — Publish the login on an abandoned body check in the authentication session (ADR-0045 amendment 1)

## Goal

Abandoning a body check publishes the LoginJudgedEvent the ADR-0045 amendment 1 decision 2 says.

## Context

ADR-0045 amendment 1 (BL-1553) decides what an abandoned body-bound login publishes. Peer-visible answers must not change.

## Acceptance criteria

- [x] HttpAuthenticationSessionTests: AbandonAsync with a sent content hash publishes is_accepted true; without one publishes nothing; a faulted account publishes is_accepted false.
- [x] Build clean, fast tests green, 100% coverage held.

## Notes

- Sent-hash detection: the AwaitingBody check names its account only when the head verified the signature over a sent content hash (SignatureV4Check.BindBodyToSentHash), so a null AccountName means nothing was judged. Tests are in the new HttpAuthenticationSessionTests.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. AbandonAsync on the session's body check publishes LoginJudgedEvent per ADR-0045 amendment 1
