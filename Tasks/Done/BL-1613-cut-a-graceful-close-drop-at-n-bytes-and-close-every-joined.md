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
completed: 2026-11-22
---
# BL-1613 — Cut a graceful-close drop at N bytes and close every joined connection gracefully in the serving engine

## Goal

A drop with close=graceful sends exactly N bytes then closes every joined connection gracefully.

## Context

From BL-1608; decided in ADR-0188 (Documentation/Planning/Decisions). Decision 2 and 3 of ADR-0188. Files: Foundation/Surl.Core.UnitLibrary ExchangeDropBudget.cs, DroppingAfterBytesConnection.cs, ServingEngine.cs, ServingEngine.Datagrams.cs, FaultPlan.cs. Graceful uses DisposeAsync (ADR-0021), never Abort. Includes the ADR fallback for FTP if BL-1618 measures exit 56.

## Acceptance criteria

- [x] At N the write is cut at N, flushed, then each joined connection is disposed (data first, then control); no Abort is called.
- [x] The note is `Exchange <id> cut after <N> bytes sent by fault rule <rule id>, closing gracefully.` and one `FaultAppliedEvent` is published once per exchange; the reset note is unchanged.
- [x] `FaultPlan` carries `Close` to the engine; a datagram flow with graceful behaves as drop.
- [x] Tests: single connection, two joined connections, datagram flow, and an existing reset rule unchanged; gates hold.

## Notes

ExchangeDropBudget gained a graceful flag and DropAsync (dispose joined connections last-joined-first, else Abort); the engine picks the note by FaultRule.Close. FaultPlan already returns the whole FaultRule, so Close reaches the engine unchanged. FTP 426 fallback depends on BL-1618's measurement and is not applied here.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Done. Graceful drop cuts at N bytes and disposes every joined connection, data then control, with its own note
