---
id: BL-1944
title: Point ADR-0107 decision 1 at ADR-0122 decision 5 for the amqp1 TLS header under TLS-SRP
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1932]
touches: [Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1944 — Point ADR-0107 decision 1 at ADR-0122 decision 5 for the amqp1 TLS header under TLS-SRP

## Goal

A reader of ADR-0107 decision 1 learns that TLS-SRP, as well as a certificate, makes `amqp1` upgrade on a TLS header.

## Context

Found while working BL-1932 (2026-11-27). The decision 1 table of
`Documentation/Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md` conditions the
`AMQP 2 1 0 0` rows on a certificate alone. ADR-0122 decision 5 widens that to a certificate or TLS-SRP, and the code
does so (`ServerTlsComposition.IsTlsUpgradeAvailable`). Add a dated amendment or pointer note, in the form other ADRs
use; do not rewrite the recorded decision.

## Acceptance criteria

- [ ] ADR-0107 decision 1 carries a dated note naming ADR-0122 decision 5 and saying TLS-SRP also makes the TLS upgrade available.
- [ ] The wording of the original decision 1 table rows is unchanged.

## Notes

## Log

- 2026-11-27: Created.
