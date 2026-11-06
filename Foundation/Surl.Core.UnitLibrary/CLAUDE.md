# Surl.Core.UnitLibrary

Phase 1.

The serving engine: starts a listener for each listen URL, hands each accepted connection
or datagram flow to the protocol server its scheme names, enforces connection limits and
timeouts, and shuts down cleanly. Time comes from an injected `TimeProvider`; transports
come from `Surl.Networking` through the seams in `Surl.Protocol.Abstractions`.

It also holds the instance's event stream (`SurlEventStream`) and fault plan (`FaultPlan`,
ADR-0124 decision 4). The engine applies only `Accept`-stage `Delay` and `Drop` rules itself
(before any byte and before an implicit TLS handshake, publishing `FaultAppliedEvent`);
login, store and command faults are found in the same plan through `ExchangeContext.Faults`
and applied by the code that owns that stage. Each exchange's `ExchangeDeadlines` is its
`ExchangeContext.IdleClock`, so a server holding a command-stage delay pauses the idle timeout
(ADR-0141).

`FaultPlan.AddOverride(FaultOverrideScope, FaultOverride)` (BL-811, ADR-0124 decision 5) puts a
`Surl.Api` response override in the plan's order under the next `f<n>`. While the plan holds no
override `FindAsync` completes synchronously under the lock as before; with one, it snapshots the
entries, asks each matching override outside the lock (an entry removed meanwhile is skipped), and
returns the answer under the override's ID, or goes on to the next entry when it answers null. The
plan does not bound or catch the decision: `Surl.Api`'s `ResponseOverrideHook` does. `Rules` lists
rules only. `FaultPlan.StageOf` is public so the override can check its answer's stage.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and no protocol server.
Protocol servers receive what it provides through the contracts in Abstractions.

The identifier sources (ADR-0124, decision 6; BL-851): `CryptographicIdentifierRandom.Instance`
(stateless, `RandomNumberGenerator`) and `SeededIdentifierRandom(seed)`, whose `ForExchange(exchangeId)`
and `ForServer(name)` each return a fresh `IIdentifierRandom` - SHA-256 in counter mode
(`Sha256CounterIdentifierRandom`) over a label, the seed and the exchange ID or name - so one
exchange's identifiers never depend on what other exchanges drew. Non-secret identifiers only.
