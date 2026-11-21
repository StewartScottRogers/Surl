# ADR-0141 — How a server applies a command-stage delay or drop

- **Status:** Accepted
- **Date:** 2026-11-06
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-06, in BL-901.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 4 (the fault model: `delay` and
  `drop` default to the `Accept` stage, and `stage=command` moves them to "each server at a command"; "the
  server's own idle and head timeouts do not run during" a delay), [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md)
  section 1 (the idle timeout), and the chosen-error ADRs of BL-793 to BL-798, which apply only the `error` kind.
- **Acted on by:** BL-901 (SMTP, IMAP, POP3). The other server families follow in their own tasks.

## Context

`FaultPlan` already hands a server a `Delay` or `Drop` rule written with `stage=command` at a
`FaultStage.Command` point, but no server applied one: the mail servers asked the plan at each command and
ignored every kind but `Error`. Two things were missing from `Surl.Protocol.Abstractions`. A server could not
stop the engine's idle clock while it held a command on purpose - only the engine reached
`ExchangeDeadlines.PauseIdleClock` - so a delay longer than the idle timeout would have cut the exchange off as
idle. And every server would have written the same hold, note, event and abort for itself. A third, smaller
duplication: the listen URL as the instance reports it was formatted by the engine's internal
`ServingEngine.FormatListenUrl` and by a copy in each mail library.

## Decision

1. **The idle clock is a seam.** `IIdleClock` (`Pause`, `Restart`) reaches a server as
   `ExchangeContext.IdleClock`. Its default, `NoIdleClock.Instance`, does nothing, so a context built without
   the engine behaves as before. The engine sets the exchange's own `ExchangeDeadlines`, which implements it
   explicitly over `PauseIdleClock` and `RestartIdleClock`. The maximum exchange duration keeps running during
   a hold: a test that asks for a delay longer than the exchange may last gets the duration limit, as it would
   from a slow real server. `RecordingIdleClock` is the test double: it counts pauses and restarts and
   completes `FirstPause`, so a test can wait for a hold to begin.
2. **One helper applies both kinds.** `CommandStageFaults.FindAndApplyAsync(context, connection, point,
   target)` asks `ExchangeContext.Faults` at the point and:
   - `Delay`: notes `<verb> held for <s> s by fault rule <id>.`, publishes one `FaultAppliedEvent` (kind
     `Delay`, stage `Command`, the target), pauses the idle clock, waits the rule's `ms` (none when unset) on
     `ExchangeContext.TimeProvider` and the exchange's token, and restarts the idle clock however the wait
     ends. The server then answers the command as usual.
   - `Drop`: notes `<verb> dropped the connection, unanswered, by fault rule <id>.`, publishes one
     `FaultAppliedEvent` (kind `Drop`), and calls `IConnection.Abort()` - the reset the engine's own
     `Accept`-stage drop uses. The server sends nothing more and ends the session. `after-bytes` has no
     meaning at a command and is ignored there.
   - Any other kind is handed back untouched, so a server answers a chosen `Error` in its own shape as before.
   The event's target is what the chosen error's is: the path the point names, or else the verb.
3. **Where a mail server asks.** Exactly where it asked for a chosen error (BL-796): after the state's login
   check, before the store is touched; IMAP's `APPEND` before its `+` continuation, so a drop sends no `+` and
   a delay holds it. No new point is asked, so a rule that matched an `Error` before matches the same commands
   as a `Delay` or `Drop` now.
4. **`ReportedListenUrl` lives in `Surl.Protocol.Abstractions`.** `ReportedListenUrl.Format(ListenUrl)` is the
   one implementation; the engine and the three mail servers use it, and the mail libraries' copies are gone.
   The HTTP, SSH, SMB, Dict, Gopher and Telnet fault classes still hold their own `FormatListenUrl`; each moves
   to the shared one when its family's command-stage delay and drop task touches it.

## Consequences

- A test can write `--fault 'delay;stage=command;command=RETR;ms=500'` or `'drop;stage=command;command=SELECT'`
  and get a slow or cut-off mail command, with no idle-timeout surprise and one event per application.
- Other server families apply the same two kinds by calling the same helper at their command points; nothing in
  `Surl.Core` or the abstractions changes for them.
- No byte Surl sends changed: a delay sends the usual answer later and a drop sends none. Upstream curl's
  reaction is its own timeout or a connection error, measured where a conformance test needs it.

## Amendment 1 â€” the HTTP, RTSP and WebSocket servers (BL-902)

- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-11, in BL-902.
- **Acted on by:** BL-902 (HTTP, including HTTP/2 and HTTP/3, RTSP, WebSocket). [ADR-0134](ADR-0134-how-the-http-rtsp-and-websocket-servers-answer-a-chosen-error.md) decision 5 is superseded: these servers now apply `delay` and `drop` at the command stage.

1. **A helper without a connection.** `CommandStageFaults.FindAndApplyAsync(context, point, target)` does what
   the connection overload does except that a `Drop` is noted, published and handed back without an
   abort; the connection overload calls it and then aborts. A server that cannot reach the connection from
   where it asks, or that must not abort a connection other exchanges share, ends the dropped exchange itself.
2. **Where the three servers ask.** At the point they asked for a chosen error (ADR-0134 decision 1 to 3), through
   `HttpCommandFaults.FindAndApplyAsync` (the renamed `FindChosenErrorAsync`), which builds the same
   `FaultPoint`, using `ReportedListenUrl` in place of its own copy. `Delay` holds the request and returns no rule,
   so the request is answered as without the rule; `Drop` and `Error` are returned; any other kind is noted as
   not applied. The WebSocket upgrade is held and dropped before its `101`; a drop sends not one byte.
3. **How a drop ends the exchange.**
   - RTSP and WebSocket call `IConnection.Abort()` and end the connection.
   - HTTP/1.x: `HttpAnswer` gains `DropsExchange` (default `false`, so no other caller changes). The answerer
     returns one for a drop and `Http1AnswerWriter` aborts the connection and writes nothing.
   - HTTP/2 and HTTP/3 **reset the request's stream** with `INTERNAL_ERROR` (`H2 INTERNAL_ERROR` and `H3_INTERNAL_ERROR`,
     `0x102`) before any header block, and the connection carries on. Aborting would cut every other stream a test has
     in flight on the connection, which a fault on one request must not do; `rule` scope (`path`, `command`,
     `account`) is how a test picks the request, and a stream reset is how a multiplexed server drops one request
     (the same reset the writers already give a file that cannot be sent whole).
4. **Idle clock and delay.** The delay pauses `ExchangeContext.IdleClock` as ADR-0141 decision 1 says, so the idle
   timeout does not fire during it; the maximum exchange duration keeps running. The head timeout is not running once
   the head was read, which is where the command is asked.
5. **Not measured against upstream curl byte for byte.** A delayed request sends the usual answer later, and a drop
   sends no byte, so no new byte is pinned; the tests replay the recorded request bytes of the existing fixtures.

## Amendment 2 — a delay's hold ends when the client leaves (BL-1520)

- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-21, in BL-1520.
- **Acted on by:** BL-1529 (the contract member and the command-stage helper), BL-1530 (`Surl.Networking`), BL-1531 (`BytePacedConnection`), BL-1521 (the engine's `Accept` hold and its wrappers), BL-1532 (HTTP/1.x, RTSP and WebSocket command holds).
- **Why:** a client that cancels a request held by a `delay` rule kept its `--max-connections` slot until the delay ended, up to 600000 ms (ADR-0124 decision 4), because nothing reads the connection during a hold. A test must get its slot back (the stand-in rule), so with `MaxConnectionsPerAddress = 1` the next request must be admitted, not refused `503`.

1. **A hold ends when the client closes or resets.** At the `Accept` stage (the engine) and at the `Command` stage (a server using the connection overload of `CommandStageFaults.FindAndApplyAsync`), a `Delay` hold ends as soon as the peer half-closes (FIN) or resets. A real slow server would notice at its next write; Surl notices at once because a test must get its slot back and cannot wait ten minutes. A peer's half-close is treated as leaving even if the client still waits for an answer (a request followed by FIN): it cannot be told from a cancelled request, and the stand-in rule wins. The hold's length is unchanged for a client that stays, and shutdown, the maximum duration and the delay itself still end it as before.
2. **The member that sees it:** `IConnection.WatchForPeerCloseAsync(int maxBufferedBytes, CancellationToken)` in `Surl.Protocol.Abstractions.UnitLibrary`, returning `ValueTask<PeerWatchOutcome>` (`NotWatched`, `PeerClosed`, `PeerReset`, `BufferFull`). It is a default interface member returning `NotWatched` at once, so every existing connection and test double behaves as before and the hold runs its full length. `PeekByteAsync` cannot do it: it returns `null` both for "nothing yet" and for "half-closed", and returns the first byte, not the close, once the request has arrived, which is exactly when a cancelling client has already sent it.
   - `StreamConnection` (`Surl.Networking.UnitLibrary`) implements it by reading ahead from the transport into a bounded buffer of at most `maxBufferedBytes` (64 KiB, as `LineBytesRedactor`'s bound): a read of 0 is `PeerClosed`, an `IOException` is `PeerReset`, a full buffer is `BufferFull` (the watch stops, TCP back-pressure holds the rest, and the hold carries on unwatched). The in-flight transport read is never cancelled, as `PeekByteAsync`'s is not, so cancelling the watch (the delay ended) loses nothing.
   - **No byte is lost to a server that is still served.** The read-ahead bytes are returned by the next `ReadAsync` first, in order, and `UpgradeToTlsAsync` hands them to the handshake ahead of the transport (generalising ADR-0113 decision 6's one peeked byte); `DisposeAsync`'s lingering close reads them as already read. A watch after a peek keeps the peeked byte first.
   - Wrappers forward it: Core's `IdleClockRestartingConnection` (without restarting the idle clock, which the hold keeps paused), `RecordingConnection`, `DroppingAfterBytesConnection`, and `BytePacedConnection`; a wrapper that does not forward falls back to `NotWatched`. `InMemoryConnection` implements it over its script.
3. **What is logged and published.** The hold's note and its one `FaultAppliedEvent` are written at the start, as now. When it ends early the exchange log adds `Exchange <id>: the client closed the connection during the hold by fault rule <rule id>.` for a half-close and `Exchange <id>: the client reset the connection during the hold by fault rule <rule id>.` for a reset (at a command stage the exchange ID is `ExchangeContext.ExchangeId`). Nothing new is published and `ExchangeEndedEvent` does not change (its counts are what was received and sent): it is published once, as for any exchange, when the exchange ends, which is now at the early end. The idle clock is restarted however the hold ends.
4. **What happens next.** At `Accept` the engine does not call the server or start an implicit TLS handshake: the exchange ends, the connection is closed (aborted after a reset, closed gracefully after a half-close), and the slot is released in `RunExchangeAsync`'s `finally` as for any exchange. At a command stage the helper returns the rule as for a completed delay and the server's next read sees the half-close or reset itself (the read-ahead bytes first), so it ends its session in its own way.
5. **Which servers change.** Every server whose command-stage hold goes through the connection overload gets it from BL-1529 once BL-1530 lands (SMTP, IMAP and POP3, through BL-1531's wrapper). HTTP/1.x, RTSP and WebSocket hold through `HttpCommandFaults`, which calls the overload without a connection; BL-1532 passes the exchange's connection for those. HTTP/2 and HTTP/3 stay as they are on purpose: a stream shares its connection with others (amendment 1 decision 3), a client cancelling one request resets that stream, not the connection, and a connection-level read-ahead would steal the frames the connection's own reader needs.
6. **Configuration.** No new rule attribute: configuration only grows, and the default (a hold that ends when the client leaves) is the behaviour a test wants. A test that needs a hold to outlive its client has no use for one.
7. **Not measured against upstream curl.** No byte Surl sends changes; a client that stays is answered as before after the same delay.
## Amendment 3 — a server sends nothing when the client left during the hold (BL-1538)

- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-21, in BL-1538.
- **Acted on by:** BL-1540 (the contract), BL-1541 (HTTP/1.x), BL-1542 (RTSP), BL-1543 (WebSocket upgrade), BL-1544 (SMTP), BL-1545 (IMAP), BL-1546 (POP3).
- **Why:** amendment 2 decision 4 left the server's "next act" after an early-ended hold to the server. For servers whose next act is a write (HTTP, RTSP, WebSocket, SMTP, IMAP, POP3) that write still happens, a write after the peer's FIN usually succeeds, and `RecordingConnection` records the answer as sent. A test whose client timed out under a `delay` rule then sees `200 OK` in the exchange record for a client that was gone (Surly cookbook bug report of 2026-11-21, batch 8, item 2: a lead, not evidence). The stand-in rule says the record must tell the truth.

1. **The server writes nothing.** After a hold ended by `PeerClosed` or `PeerReset` the server does not write its answer and ends the exchange, as it does after a `drop` but with no abort for a half-close: a half-close closes the connection gracefully, a reset aborts it. This follows amendment 2 decision 1: a half-close is the client leaving even if it still waits for an answer.
2. **How the helper says so.** `CommandStageFaults` gains `FindAndApplyWithClientOutcomeAsync(ExchangeContext, IConnection, FaultPoint, string)`, returning `CommandStageFaultResult(FaultRule? Rule, PeerWatchOutcome ClientLeft)`; `ClientLeft` is `PeerClosed` or `PeerReset` when the hold ended because the client left and `NotWatched` otherwise (a completed hold, a `Drop`, no match). A server opts in by calling it and ending its session when `ClientLeft` is not `NotWatched`. The existing overloads, and so every server that does not opt in, behave exactly as amendment 2 decision 4 says (BL-1540). The servers that change are HTTP/1.x (through `HttpCommandFaults`, BL-1541), RTSP, the WebSocket upgrade, SMTP, IMAP and POP3. HTTP/2 and HTTP/3 stay out (amendment 2 decision 5). Servers whose next act after the hold is a read already see the half-close or reset and need no change.
3. **What a test observes.** The log already says `Exchange <id>: the client closed the connection during the hold by fault rule <rule id>.` (or `reset`); nothing new is logged or published. The one `FaultAppliedEvent` is unchanged and `ExchangeEndedEvent` reports what was received and sent, now with nothing sent for the held command's answer, so "the client left during the hold and nothing was sent" is asserted from the log note, that event and the `Surl.Api` / `surl-mcp` exchange record showing no response bytes. No new event type, field or option.
4. **No byte a staying client receives changes.** A client that stays is answered after the same delay with the same bytes; only an exchange whose client has already half-closed or reset loses its answer. Not measured against upstream curl: no byte Surl sends to a client that stays changes.
