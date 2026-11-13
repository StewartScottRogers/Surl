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
completed:
---
# BL-1092 — Replace the BL-510 and BL-511 placeholders in the Amqp1 matrix with their tests

## Goal

Every row of `Documentation/Conformance/Extended/Amqp1.md` that still says `BL-510` or `BL-511` names the unit test that proves it, or `n/a` with a reason.

## Context

BL-510 and BL-511 are Done, but the matrix still lists their task IDs as placeholders (11 and 18 rows). BL-515 found this and left them: its `touches` is the live-client tests. Find each row's test in `Surl.Extended.Protocol.Amqp1.UnitTests`.

## Acceptance criteria

- [ ] `grep -c "BL-51[01]" Documentation/Conformance/Extended/Amqp1.md` prints 0, and each replaced row names a test that exists.

## Notes

## Log

- 2026-11-12: Created.
