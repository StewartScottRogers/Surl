---
id: BL-1092
title: Replace the BL-510 and BL-511 placeholders in the Amqp1 matrix with their tests
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1092 — Replace the BL-510 and BL-511 placeholders in the Amqp1 matrix with their tests

## Goal

Every row of `Documentation/Conformance/Extended/Amqp1.md` that still says `BL-510` or `BL-511` names the unit test that proves it, or `n/a` with a reason.

## Context

BL-510 and BL-511 are Done, but the matrix still lists their task IDs as placeholders (11 and 18 rows). BL-515 found this and left them: its `touches` is the live-client tests. Find each row's test in `Surl.Extended.Protocol.Amqp1.UnitTests`.

## Acceptance criteria

- [x] `grep -c "BL-51[01]" Documentation/Conformance/Extended/Amqp1.md` prints 0, and each replaced row names a test that exists.

## Notes

All 29 rows (11 BL-510, 18 BL-511) now name tests taken from the test projects' method lists. Amqp1:RFC4616-1 cites `Surl.Authentication.UnitTests`, where PLAIN authzid handling is tested. Amqp1:Security-5.3.3.5-3 cites the after-outcome SASL-frame test, whose name says the AMQP header is answered.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Amqp1 matrix BL-510/BL-511 placeholders replaced by 29 named tests
