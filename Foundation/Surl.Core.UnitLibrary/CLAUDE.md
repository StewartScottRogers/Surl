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

`EventPublishingDecryptedBytesSink` (ADR-0169; ADR-0182, amendments 1, 21, 22 and 37) notes the decrypted bytes a server
reports and publishes them raw. When the server redacts and its redactor is an `IStreamReceivedBytesRedactor`, one
instance per exchange takes every received `Decrypted` chunk of a stream, under a lock. Received `Decoded` text goes to a
fresh redactor's `RedactDecodedText` when it is an `IDecodedTextReceivedBytesRedactor`. Received `Decrypted` bytes with
no stream ID and every frame payload (`ReceivedFramePayload`) go to a fresh redactor's `RedactStreamlessBytes` or
`RedactFramePayload` when it is an `IStreamlessAndFramePayloadReceivedBytesRedactor`. Every other received chunk or
frame payload goes through a fresh redactor flushed at the chunk's end.
`Surl.Core.UnitTests` references `Surl.Http3` to prove with the real redactor that a split `HEADERS` frame and a secret
field of decoded header text are withheld, and that a datagram or `DATA` payload shaped like a secret `HEADERS` frame is
noted as received.

The identifier sources (ADR-0124, decision 6; BL-851): `CryptographicIdentifierRandom.Instance`
(stateless, `RandomNumberGenerator`) and `SeededIdentifierRandom(seed)`, whose `ForExchange(exchangeId)`
and `ForServer(name)` each return a fresh `IIdentifierRandom` - SHA-256 in counter mode
(`Sha256CounterIdentifierRandom`) over a label, the seed and the exchange ID or name - so one
exchange's identifiers never depend on what other exchanges drew. Non-secret identifiers only.

`ExchangeTlsLoginJudge` (ADR-0165, BL-1224) is the `ITlsLoginJudge` the engine gives each accepted
connection (`UseTlsLoginJudge`, once its `ExchangeContext` exists, so implicit TLS and STARTTLS alike).
`ShouldRefuseAsync` asks the fault plan at `FaultStage.Login` (exchange, listener, scheme, account) and, for a
`RefuseLogin` rule only, publishes `FaultAppliedEvent`; `Judged` then publishes `LoginJudgedEvent` with mechanism
`tls-srp` and `IsRefusedByFault` set when that refusal came first. No secret is in either event.

A `Drop` rule with `after-bytes` counts every byte the exchange sends on all its connections (ADR-0124
amendment, BL-1518/BL-1519): `ExchangeDropBudget` is one lock-guarded count shared by the control
connection and every data connection (`ExchangeDataConnections.DropBudget`), each wrapped in a
`DroppingAfterBytesConnection`. The write that reaches the count is cut at it, then every joined
connection is aborted and the note and one `FaultAppliedEvent` are written once. With `FaultRule.Close = Graceful`
(ADR-0188, BL-1613) each joined connection is instead disposed (lingering close, ADR-0021), last joined first so data goes
before control, no `Abort`, and the note reads `Exchange <id> cut after <N> bytes sent by fault rule <rule id>, closing
gracefully.`; a datagram flow ignores `Close` and behaves as a drop, except for an `IGracefulCutDatagramProtocolServer`
(HTTP/3; ADR-0188 amendment 5, BL-1801): its flow is a `GracefullyCutAfterBytesDatagramFlow` (whole datagrams counted as
`SilencedAfterBytesDatagramFlow` does, closing datagrams sent uncounted), the server closes its connection at the cut,
and the note is the graceful one. The `FaultAppliedEvent` of every `Accept`-stage drop carries `Close` (BL-1794), how the
engine ended it rather than what the rule asked: `Graceful` for either graceful cut, `Reset` for the reset cut, a drop
before any byte and every other datagram drop; a `Delay`'s event carries none.

An `Accept`-stage `Delay` hold ends when the client leaves (ADR-0141, amendment 2; BL-1521): the engine
races the delay against `IConnection.WatchForPeerCloseAsync` (64 KiB read-ahead, bytes kept for a client that
stays). A half-close or reset ends the exchange unserved with the note `Exchange <id>: the client
closed|reset the connection during the hold by fault rule <rule id>.`, aborts the connection after a reset,
and frees the `--max-connections` slots in `RunExchangeAsync`'s `finally`; the idle clock is restarted either
way. `IdleClockRestartingConnection` (without restarting the idle clock), `RecordingConnection` and
`DroppingAfterBytesConnection` forward the watch. A datagram flow has no peer to watch and is held in full.
