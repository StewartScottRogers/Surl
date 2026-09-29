# ADR-0022 — An accept failure of one client does not stop the listener

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29

## Context

ADR-0004, section 6 defines `IConnectionListener.AcceptAsync`. `Surl.Core`'s serving
engine treats any exception from it other than its own cancellation as a failure of the
whole listener: it stops every listener, drains the exchanges and rethrows, and
`Surl.Console` exits with `InternalError` (125, ADR-0005). That is right for a listening
socket that is broken. It was wrong for one client: a client that resets its connection
after the handshake but before the server's `accept` returns makes `accept` fail -
`SocketError.ConnectionReset` on Windows, `ECONNABORTED` (`SocketError.ConnectionAborted`)
on Linux and macOS - and until BL-057 that one client stopped the server. The same
happened when the accept succeeded but the client was gone before `TcpConnectionListener`
could set `NoDelay` on the socket or read its endpoints.

Nothing here is a byte upstream curl sends or expects, so there was nothing to measure:
upstream curl sees only whether the server is still listening for its next connection.

## Decision

1. `AcceptFailureClassifier.IsPerConnection(SocketError)` in `Surl.Networking` names the
   accept failures that belong to one client:
   - `ConnectionReset` - Windows' `accept` and `AcceptEx`: "an incoming connection was
     indicated, but was subsequently terminated by the remote peer prior to accepting the
     call";
   - `ConnectionAborted` - `ECONNABORTED` from `accept(2)` on Linux and macOS, the same
     event;
   - `HostDown`, `HostUnreachable`, `NetworkUnreachable` - pending network errors of the
     new connection that Linux's `accept(2)` passes on and tells the caller to treat like
     `EAGAIN`, by retrying.

   Every other `SocketError` is listener-fatal. The rest of Linux's pending-error list is
   left out on purpose: `ENETDOWN` is also Windows' "the network subsystem has failed",
   and `EOPNOTSUPP` is also "this socket cannot accept", so retrying them could loop on a
   dead listener; `EPROTO`, `ENOPROTOOPT` and `ENONET` have no `SocketError` of their own.
2. A socket that was accepted but whose `NoDelay` or endpoints could not be read is
   released at once and reported as `AcceptedSocketLostException`, which is always
   per-connection: the listening socket did its job.
3. `AcceptRace` absorbs a per-connection failure: it starts another accept on that source
   and goes on waiting, within the same `AcceptNextAsync` call. A listener-fatal
   `SocketException` is still thrown as `IOException`, and cancellation and disposal are
   unchanged. `TcpConnectionListener.AcceptAsync` therefore throws only for a failure of
   the listener itself, for cancellation, or after disposal.
4. Nothing is logged for an absorbed failure. `Networking` has no log seam, and a client
   that gave up before it was accepted never became an exchange for the log to describe.

## Consequences

- A client resetting mid-handshake no longer ends `surl` with 125.
- A per-connection error that repeats forever is retried forever, but each retry waits in
  a fresh `accept`, so it does not spin; only a listener-fatal error ends the loop.
- The serving engine is unchanged: it still stops on whatever `AcceptAsync` throws, which
  now means the listener really failed.
- The decision is fast-tested without a socket (`AcceptFailureClassifierTests`,
  `AcceptRaceTests`), as ADR-0004, section 8 asks.
