---
id: BL-792
title: Match fault rules by listener, account and path and apply delay and dropped-connection faults in Surl.Core's serving engine
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-787]
touches: [Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-792 — Match fault rules by listener, account and path and apply delay and dropped-connection faults in Surl.Core's serving engine

## Goal

`Surl.Core` holds the fault plan every server and store consults - rules matched in order by stage, listener or
scheme, account and path, each with an optional count, changeable while serving - and the serving engine itself
applies the two protocol-neutral faults, delay and drop the connection, so every protocol, original and extended,
can be slowed or cut without any protocol change.

## Context

- Decision: BL-822's ADR decision 4 (kinds, scopes, stages, `times`, matching order, what a delay delays, when a
  drop happens - at accept, or after N bytes - and what it does to a TLS handshake).
- Contracts: BL-823 (`IFaultPlan`, `FaultRule`, `FaultPoint`, `FaultStage`, `ExchangeContext.Faults`,
  `FaultAppliedEvent`). Event stream: BL-787.
- Where: `Foundation/Surl.Core.UnitLibrary`. The plan (planning name `FaultPlan`, implementing `IFaultPlan`) is
  thread-safe: rules may be added, removed and cleared while exchanges run (BL-808 and the control endpoint do so);
  a rule's remaining count is decremented atomically, so `times=1` fires exactly once under concurrent exchanges.
  `ServingEngine` takes the plan (empty when not given), sets `ExchangeContext.Faults`, and at accept and on the
  stream applies delay (on the injected `TimeProvider`, never `Thread.Sleep`) and drop (abort the connection or
  flow; for a datagram flow, stop answering), publishing `FaultAppliedEvent` each time.
- Account-scoped and path-scoped rules are matched here but applied by the login check (BL-791), the stores
  (BL-788 to BL-790) and the servers (BL-793 to BL-798); the engine only applies listener-scoped delay and drop.
- Stand-in: the fault-injectable principle's matcher; publishes `FaultAppliedEvent`.
- Amended by ADR-0124 decision 4: the plan's method is `FindAsync(FaultPoint, CancellationToken)` returning
  `ValueTask<FaultRule?>` (synchronous when no hook is registered), so "`Find` calls" below means `FindAsync`;
  rules are tried in the order added, a rule's kind must be allowed at the point's stage, `path` matches equal or
  as a segment prefix, `times` is decremented when applied, at most 256 rules, IDs `f1`, `f2`, ...; an
  `Accept`-stage drop or delay acts before the TLS handshake.

## Acceptance criteria

- [ ] Named `FaultPlanTests` show: first matching rule wins; scope by listen URL, scheme, account and path each
      matched and not matched; `times=1` fires once under 32 concurrent `Find` calls; add, remove and clear while
      in use.
- [ ] Named `ServingEngineTests` with fake listeners and `InMemoryConnection` show: a drop rule on one listener
      closes its connection before the server reads a byte while another listener serves; a delay rule holds the
      exchange until the fake `TimeProvider` advances by the rule's delay; each publishes a `FaultAppliedEvent`;
      a datagram flow under a drop rule gets no answer.
- [ ] With no plan given, every existing `Surl.Core.UnitTests` test passes unchanged.
- [ ] `dotnet build Foundation/Surl.Core.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Core` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Amended by ADR-0124 (BL-822): `FindAsync`, stage check, matching and TLS rules.
- 2026-11-05: Backlog -> Doing.
