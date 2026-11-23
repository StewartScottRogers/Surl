---
id: BL-1613
title: Cut a graceful-close drop at N bytes and close every joined connection gracefully in the serving engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1608, BL-1611]
touches: [Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: FR-062
created: 2026-11-22
completed:
---
# BL-1613 — Cut a graceful-close drop at N bytes and close every joined connection gracefully in the serving engine

## Goal

A drop with close=graceful sends exactly N bytes then closes every joined connection gracefully.

## Context

From BL-1608; decided in ADR-0188 (Documentation/Planning/Decisions). Decision 2 and 3 of ADR-0188. Files: Foundation/Surl.Core.UnitLibrary ExchangeDropBudget.cs, DroppingAfterBytesConnection.cs, ServingEngine.cs, ServingEngine.Datagrams.cs, FaultPlan.cs. Graceful uses DisposeAsync (ADR-0021), never Abort. Includes the ADR fallback for FTP if BL-1618 measures exit 56.

## Acceptance criteria

- [ ] At N the write is cut at N, flushed, then each joined connection is disposed (data first, then control); no Abort is called.
- [ ] The note is `Exchange <id> cut after <N> bytes sent by fault rule <rule id>, closing gracefully.` and one `FaultAppliedEvent` is published once per exchange; the reset note is unchanged.
- [ ] `FaultPlan` carries `Close` to the engine; a datagram flow with graceful behaves as drop.
- [ ] Tests: single connection, two joined connections, datagram flow, and an existing reset rule unchanged; gates hold.

## Notes

## Log

- 2026-11-22: Created.
