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
completed: 2026-11-05
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

- [x] Named `FaultPlanTests` show: first matching rule wins; scope by listen URL, scheme, account and path each
      matched and not matched; `times=1` fires once under 32 concurrent `Find` calls; add, remove and clear while
      in use.
- [x] Named `ServingEngineTests` with fake listeners and `InMemoryConnection` show: a drop rule on one listener
      closes its connection before the server reads a byte while another listener serves; a delay rule holds the
      exchange until the fake `TimeProvider` advances by the rule's delay; each publishes a `FaultAppliedEvent`;
      a datagram flow under a drop rule gets no answer.
- [x] With no plan given, every existing `Surl.Core.UnitTests` test passes unchanged.
- [x] `dotnet build Foundation/Surl.Core.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Core` keeps 100% line and branch coverage.

## Notes

Delivered (2026-11-05, dark factory lane 7). Choices taken within ADR-0124 decision 4, recorded here because
each is a detail the ADR leaves open rather than a new decision:

- `FaultPlan` (Surl.Core) implements `IFaultPlan`: `Add` (assigns `f1`, `f2` ... whatever `Id` the rule had,
  never reused even after `Remove` or `Clear`), `Remove(id)`, `Clear()`, `Rules` (snapshot, `Times` = what is
  left), `MaxRules` 256. One lock guards the list, so a find, its count decrement and every change are atomic;
  `FindAsync` completes synchronously.
- `Add` refuses a rule whose stage its kind does not act at (`Delay`/`Drop`: Accept or Command; `RefuseLogin`:
  Login; `Error`: Command; `OutOfSpace`: StoreWrite), `times` < 1, negative `after-bytes`, a `Delay` without `ms`
  or with `ms` outside 0 to 600000. It does not check an `error` rule's `reply` shape: that check belongs with
  the reader (BL-785) and the run-time adders (BL-808, the control endpoint).
- `times` is spent when the rule is found - the finder is the one that applies it - so `times=1` fires once.
  A spent rule stays in `Rules` with `Times` 0 until removed.
- `listener` matching: exact text, or both parsed as absolute URLs with scheme and host equal ignoring case and
  equal port; a rule port of 0 (the listen URL as given with port 0) matches any bound port of that scheme and
  host, since the point carries only the bound form. `scheme` and `command` ignore case; `account` is ordinal
  (as `AccountBook`); `path` is equal or a segment prefix (`/uploads` matches `/uploads/a`, not `/uploads2`).
- Engine: a new last constructor parameter `IFaultPlan faults` (the old overloads pass `EmptyFaultPlan.Instance`),
  set as every exchange's `ExchangeContext.Faults`. It asks at `Accept` (exchange ID, bound listen URL, scheme)
  before any byte and before an implicit TLS handshake. Only `Delay` and `Drop` are applied there; any other kind
  a custom plan returns is ignored by the engine.
- Delay waits on the injected `TimeProvider` with the idle clock paused (`ExchangeDeadlines.PauseIdleClock`) and
  restarted after; shutdown and the maximum exchange duration still cancel it.
- Drop without `after-bytes` (or 0) aborts the connection unserved, or leaves a flow unanswered (the server is
  never called). With `after-bytes` N: a connection lets the server's plaintext writes through until N bytes,
  cuts the crossing write at N, aborts, and fails later I/O with `IOException`; a flow sends whole datagrams
  while the total stays within N and then discards every datagram the server sends.
- Each application notes it in the exchange log and publishes `FaultAppliedEvent` (stage Accept, target the
  bound listen URL); an `after-bytes` drop publishes when it happens.
- Command-stage delay and drop are found by the servers through `ExchangeContext.Faults` (BL-793 to BL-798).

Tests: `FaultPlanTests`, `ServingEngineTests.Faults.cs`, `DroppingAfterBytesConnectionTests`,
`SilencedAfterBytesDatagramFlowTests`, two `ExchangeDeadlinesTests`; `ManualTimeProvider.TimersCreated(n)`
added for the delay tests. Surl.Core: 244 tests, 100% line and branch, worst CRAP 10.

## Log

- 2026-11-05: Created.
- 2026-11-05: Amended by ADR-0124 (BL-822): `FindAsync`, stage check, matching and TLS rules.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Core's FaultPlan matches rules by stage, listener, scheme, account, path and command with atomic times; ServingEngine applies Accept-stage delay and drop (after-bytes too) to connections and flows and publishes FaultAppliedEvent
