# ADR-0021 — A lingering close for every TCP connection

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29

## Context

ADR-0004, section 2 says `IConnection.DisposeAsync` closes gracefully: it completes
writes (FIN) if the server did not, then releases the transport. Until BL-056,
`StreamConnection` released the socket straight after FIN. When the client had sent bytes
the server never read - a request body a refusal did not read, a pipelined request -
Windows and Linux answer the close with a TCP reset, and a client that has not yet read
the tail of the reply loses it.

Measured on 2026-09-29 with pinned upstream curl 8.21.0 (win-x64 reference build,
SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`) and
`Record-CurlExchange.ps1`, which BL-056 gave a `-CloseUnread` switch. curl sent a
4 MiB body (`-sS -o <file> -w %{size_download} -H Expect: --data-binary @<file>`); the
recorder answered after the head with `413 Content Too Large`, `Content-Length: 200000`,
`Connection: close` and a 200000-byte body:

| Server close | Runs | Exit codes | stderr | Bytes curl received |
| --- | --- | --- | --- | --- |
| At once, body unread (`-RespondAfterBodyBytes 0 -CloseUnread`) | 3 | 0, 56, 56 | `curl: (56) Recv failure: Connection was reset` | 200000, 102323, 65203 |
| After reading until curl stops (`-RespondAfterBodyBytes 0`) | 3 | 0, 0, 0 | (none) | 200000 each time |

With a lingering close curl read the whole reply, then stopped sending and closed its end
within about 50 ms of the reply. One run of each is kept in
`Surl.Networking.UnitTests/Fixtures/` (`lingering-close-off`, `lingering-close-on`).

## Decision

1. **Every TCP connection lingers when it is disposed**, in `Surl.Networking`'s
   `StreamConnection`, below every protocol server: once FIN has been sent, it reads and
   discards what the client still sends until the client half-closes (a read returns 0) or
   resets, or until `StreamConnection.LingeringCloseTime` passes, and only then releases
   the transport. It is the transport's close, so every protocol gets it without asking;
   a server that must read the unread bytes itself to answer correctly (HTTP's BL-061)
   still does.
2. **The bound is 2 seconds**, timed on an injected `TimeProvider`
   (`TimeProvider.System` from the listener), with no byte limit. Discarded bytes pass
   through one 4096-byte buffer, so the cost is time, not memory, and the time is capped.
   Upstream curl stopped sending within 50 ms of the reply in every measured run; two
   seconds leaves room for a slow link without letting a peer that never stops hold the
   connection much past its exchange. It is a fixed constant, not an `ExchangeLimits`
   member: BL-024's limits (ADR-0006) are what a peer may do during an exchange, and this
   is how the transport ends one; a later task can make it an option if a need is
   measured.
3. **The drain reads below TLS.** On a secured connection it reads the raw transport after
   close_notify and FIN, so a peer's own close_notify or a garbled record ends nothing
   early and throws nothing.
4. **No linger when there is nothing to close gracefully**: not after `Abort` (which
   resets on purpose), and not when completing writes failed (the peer already reset, or
   the TLS handshake never finished). When the bound runs out with bytes still arriving,
   the close may still be a reset; that is the accepted cost of the bound.
5. **Disposal still never throws** (ADR-0004, section 2): a reset during the drain and
   the end of the lingering time are both swallowed.

## Consequences

- A connection whose client keeps it open after the reply (keep-alive with nothing more
  to send) now takes up to two seconds to be released once the server is done; it still
  counts against ADR-0006's connection limits until then.
- `DisposeAsync` on a connection over a stream that never ends and never honours
  cancellation would wait for that stream; every stream Surl uses (`NetworkStream`,
  the tests' `InMemoryDuplexStream`) honours the token.
- Fast tests: `StreamConnectionTests.DisposeAsync_ClientSentUnreadBytes_*`,
  `DisposeAsync_PeerNeverHalfCloses_StopsDiscardingAtTheLingeringCloseTime` (on
  `ManualTimeProvider`), `DisposeAsync_PeerResetsWhileLingering_*`, and the two
  `DoesNotLinger` cases. Integration: `TcpConnectionListenerTests.DisposeAsync_ClientSentBytesTheServerNeverRead_ClientStillReadsTheWholeReply`
  on `127.0.0.1:0`, which fails with a reset when the drain is removed.
