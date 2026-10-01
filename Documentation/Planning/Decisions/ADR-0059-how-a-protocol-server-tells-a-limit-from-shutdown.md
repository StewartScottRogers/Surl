# ADR-0059 — How a protocol server tells a limit's cancellation from shutdown

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-234 (FR-043).
- **Amends:** [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) section 6, where it
  says the idle timeout and maximum duration reach a server only as the cancellation of
  `ExchangeContext.CancellationToken`. Nothing else ADR-0006 says is changed.

## Context

ADR-0006 section 5 has the FTP and SMTP servers answer the idle timeout and the maximum
exchange duration with `421`, and the IMAP server with `* BYE`; ADR-0053 decision 7 pins SMTP's
`421 4.4.2 surl Timeout, closing`. `Surl.Core`'s `ExchangeDeadlines` cancels
`ExchangeContext.CancellationToken` for any of three reasons - shutdown, the idle timeout, the
maximum duration - and records which one fired first, but only for its own log note. A server
saw a cancelled token and nothing else: it could not tell a limit, which deserves a farewell,
from shutdown, where the engine has already waited out its grace period and the exchange should
end with nothing more written. BL-230 answered FTP's case by writing `421` on every
cancellation, shutdown included.

Measured behaviour of upstream curl is not in question here: curl answers a `421` to `MAIL` with
exit 55 (ADR-0053 row 17). The choice is only how the reason reaches the server.

## Decision

1. **`ExchangeContext.ShutdownToken`** (new, `Surl.Protocol.Abstractions`): an `init` member,
   `CancellationToken.None` unless the engine sets it, cancelled when the engine gives up on
   every exchange at shutdown. `ServingEngine` sets it for every exchange, connection and flow
   alike, from `ExchangeDeadlines.ShutdownToken`. It cancels `CancellationToken` too; a server
   reads it only to tell shutdown from a limit.
2. **`ExchangeContext.IsCancelledForALimit`**: `CancellationToken` is cancelled and
   `ShutdownToken` is not. A server that catches the exchange's cancellation under that condition
   writes its protocol's farewell, then completes writes.
3. **The farewell's write window is the server's own**: the one-second limit-reply deadline of
   ADR-0006 section 5, linked to `ShutdownToken` and never to `CancellationToken`, which is
   already cancelled. A shutdown during any limit reply still cuts it off, as before. The engine
   already awaits `ServeAsync` to its end after cancelling the exchange (BL-230), so no engine
   change is needed beyond setting the token.
4. **Shutdown ends the exchange with no farewell.** The cancellation escapes `ServeAsync` as
   before, and the engine notes `Exchange <n> cancelled at shutdown.`
5. **An enum of reasons is not exposed.** A token is what every server already handles, costs
   nothing on AOT, and answers the one question a server has. `Surl.Core`'s internal
   `ExchangeCancellation` keeps naming the reason in the engine's own note.

A reason that fires first and is followed by shutdown before the server looks reads as
shutdown, so the farewell is skipped; the exchange was ending either way.

## Consequences

- `SmtpProtocolServer` answers a limit's cancellation `421 4.4.2 surl Timeout, closing` and
  closes, and ends with no farewell at shutdown (BL-234).
- A server tested with a hand-built `ExchangeContext` and no `ShutdownToken` treats every
  cancellation as a limit's; tests of shutdown set both tokens.
- `FtpProtocolServer` (BL-230) still writes `421 Timeout, closing` at shutdown as well; moving it
  onto `IsCancelledForALimit` and IMAP's `* BYE` onto the same seam are follow-up tasks.
