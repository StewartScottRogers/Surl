# Surl.Protocol.Abstractions.UnitLibrary

The contracts every other project depends on. ADR-0004 (the listener seam and the exchange
context) is their specification; where this file and the ADR disagree, the ADR wins.

What it holds:

- `SurlExitCode` - the exit codes the `surl` process returns.
- `ListenUrl` - what `surl` was asked to listen on, with `BoundPort` once a listener has bound.
- The transports a protocol server receives: `IConnection` (stream-oriented) and
  `IDatagramFlow` (TFTP).
- The protocol server contracts: `IProtocolServer`, `IConnectionProtocolServer` and
  `IDatagramProtocolServer`.
- What a server is told about one exchange: `ExchangeContext`, and where its events go,
  `IExchangeLog` and `IExchangeLogFactory`.
- The hardening limits every server enforces (ADR-0006, section 6): `ExchangeLimits`, on
  `ExchangeContext.Limits`, and the optional refusal contracts a server implements to
  answer a connection or flow past a connection limit, `IConnectionRefusalWriter` and
  `IDatagramRefusalWriter` with `ConnectionRefusal`.
- The listener seam `Surl.Networking` implements: `IListenerFactory`,
  `IConnectionListener`, `IDatagramListener`, and a failure to bind,
  `ListenerBindException` with `ListenerBindFailure`.
- Test doubles every protocol test project reaches through its reference to this library:
  `InMemoryConnection` (replays an inbound byte script, records every byte written) and
  `RecordingExchangeLog` (records every log call as an `ExchangeLogEntry`). They are
  production code and held to the same coverage gates.

- The server-side TLS contract (ADR-0010): `IConnection.TlsSession` and
  `IConnection.UpgradeToTlsAsync`, `TlsSession`, `TlsHandshakeException`, and
  `TlsSchemes.IsImplicitTls`. `InMemoryConnection` stands in for implicit TLS (an initial
  session) and for an upgrade (`UpgradeRequested`, a configurable session, or a failure).

Still to come: the in-memory datagram flow (BL-037, with the TFTP server).

This library references nothing. Never construct a `Socket`, `TcpListener`, `UdpClient` or
`SslStream` here. `SurlExitCode` reuses upstream curl's `CURLE_*` number wherever a
server-side meaning carries over; add values, never renumber one. Every contract lands
with an ADR, and a change to one touches every protocol server, so contracts land first
and the protocols fan out after.
