---
id: BL-574
title: Record the SCRAM SASL decisions of BL-416 in an ADR
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-416]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed:
---
# BL-574 — Record the SCRAM SASL decisions of BL-416 in an ADR

## Goal

An ADR, marked "Decided by Claude under Stewart's delegation", records the design decisions BL-416
made for SASL `SCRAM-SHA-1` and `SCRAM-SHA-256` in `Surl.Authentication`, and the decisions index
lists it.

## Context

- BL-416 could not write the ADR itself: `Documentation/Planning/Decisions` was in BL-358's
  `touches` while BL-416 ran. Its decisions are in BL-416's Notes, in the doc comments of
  `ScramSaslExchange`, `SaslPrep` and `AccountBook.FindScramCredentials`, and in
  `Foundation/Surl.Authentication.UnitLibrary/CLAUDE.md` ("SASL SCRAM-SHA-1 and SCRAM-SHA-256").
- It amends ADR-0049 section 2's offer order (SCRAM-SHA-256, SCRAM-SHA-1 after GSSAPI) and details
  ADR-0084 decision 4.

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions` states each decision in BL-416's Notes and why:
      no `-PLUS` and no channel binding; the server nonce; the per-account salt and 4096 iterations;
      the unknown user's consistent dummy salt and `e=invalid-proof`; the server errors carried as
      `RefusalNote`; success data as a challenge or `AdditionalSuccessData`; SASLprep without the
      prohibition and bidi checks; not in the default `--auth` set; the offer order.
- [ ] `Documentation/Planning/Decisions/README.md` has its row.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
