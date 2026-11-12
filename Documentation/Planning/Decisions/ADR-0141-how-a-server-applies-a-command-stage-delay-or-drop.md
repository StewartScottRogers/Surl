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
