---
id: BL-1534
title: Carry the refusing out-of-space rule's ID on ContentCaller (ADR-0129 amendment 1)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1534 — Carry the refusing out-of-space rule's ID on ContentCaller (ADR-0129 amendment 1)

## Goal

A caller built by ContentCaller.ForExchange exposes RefusedByFaultRuleId, set by the content store when an out-of-space rule refuses an upload or session commit.

## Context

ADR-0129 amendment 1 (BL-1524) decides this; read it first. The wire bytes do not change.

## Acceptance criteria

- [x] ContentCaller has read-only RefusedByFaultRuleId, null by default and for callers not built for an exchange.
- [x] A ContentStoreTests case pins that an out-of-space rule sets it to the rule's ID before TooLarge is answered, and that a real over-limit upload leaves it null.
- [x] ContentUploadResult is unchanged and the Content library keeps 100% line and branch coverage.

## Notes

Set only when ExchangeId is non-null (ForExchange or WithExchange callers); ForAccount/Anonymous callers are never written to.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. ContentCaller.RefusedByFaultRuleId set by out-of-space refusals
