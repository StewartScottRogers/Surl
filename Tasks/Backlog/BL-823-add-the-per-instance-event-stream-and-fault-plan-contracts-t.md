---
id: BL-823
title: Add the per-instance event stream and fault plan contracts to Surl.Protocol.Abstractions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-822]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-823 — Add the per-instance event stream and fault plan contracts to Surl.Protocol.Abstractions

## Goal

Every server, store and login check can publish what happened on one per-instance event stream and ask one fault
plan whether to misbehave, through contracts in `Surl.Protocol.Abstractions` that both reach through
`ExchangeContext`, with null objects as defaults so no existing server or test changes behaviour.

## Context

- Decision: BL-822's ADR (Surl is a stand-in for tests), decisions 1 (event stream) and 4 (fault model). Use its
  names; the planning names are `ISurlEventPublisher` (`Publish(SurlEvent)`), the abstract record `SurlEvent` and
  its records (`ExchangeStartedEvent`, `ExchangeBytesEvent`, `ExchangeEndedEvent`, `LoginJudgedEvent`,
  `StoreChangedEvent`, `FaultAppliedEvent`), `IFaultPlan` (`Find(FaultPoint)` returning a `FaultRule` or null),
  `FaultPoint`, `FaultRule`, `FaultKind`, `FaultStage`.
- Where: `Foundation/Surl.Protocol.Abstractions.UnitLibrary`. Read its `CLAUDE.md`: it references nothing, every
  contract lands with an ADR, and its test doubles are production code held to the gates. Add
  `ExchangeContext.Events` and `ExchangeContext.Faults` as `init` properties beside `Limits` and
  `DataConnections`, defaulting to a null publisher (publishes nothing) and an empty plan (finds nothing), so every
  existing `new ExchangeContext(...)` compiles and behaves as before.
- Test doubles, as `InMemoryConnection` and `RecordingExchangeLog` are: a recording publisher (keeps every event in
  order) and a scripted fault plan (answers the rules it is given), so protocol and store tests need no engine.
- Update this library's `CLAUDE.md` "What it holds" list with the new contracts.
- Stand-in: this task is the contract every later stand-in task depends on; it publishes nothing itself.

## Acceptance criteria

- [ ] The contracts and records the ADR names exist, one public type per file, each with XML documentation; no
      event record carries a password, key or token (a test enumerates the event records' properties and fails
      on a name containing `Password`, `Secret` or `Token`).
- [ ] `ExchangeContext.Events` and `ExchangeContext.Faults` exist with the null defaults; a named test shows a
      context created without them publishes nothing and finds no fault.
- [ ] Named tests cover the recording publisher (order kept, safe under concurrent publishes) and the scripted
      fault plan (first matching rule returned, none when unscripted).
- [ ] `dotnet build Foundation/Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean, the whole solution
      builds, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
