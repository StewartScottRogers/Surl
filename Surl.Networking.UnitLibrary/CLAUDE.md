# Surl.Networking.UnitLibrary

Phase 1.

The production transports behind the seams in `Surl.Protocol.Abstractions`: accepting TCP
connections and UDP datagrams on the addresses a listen URL names; server-side TLS through
`SslStream` (certificates and keys, client-certificate verification for upstream curl's
`--cert`, ALPN); and the proxy servers upstream curl's proxy options talk to - HTTP
`CONNECT`, HTTPS proxy, SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h.

This is the only project that constructs `Socket`, `TcpListener`, `UdpClient` or
`SslStream`. Keep that code as thin as it can be, so that everything above it stays
testable without a network. It references `Surl.Protocol.Abstractions.UnitLibrary` and no
protocol server.

## What is here

- `TcpConnectionListener` (public) - `StartAsync(ListenUrl, CancellationToken)` binds every
  address a listen URL names on one port and implements `IConnectionListener`. Bind and
  resolution failures are thrown as `ListenerBindException` carrying a
  `ListenerBindFailure`; this project never picks a `SurlExitCode` (ADR-0004, section 6).
- The decisions behind it, fast-tested with no socket (ADR-0004, section 8):
  `ListenAddressResolver` (IP literal or every resolved address), `ListenerBinder` (one
  port across every address, all-or-nothing, port 0 retried when a later address finds it
  taken), `BindFailureClassifier` (`SocketError` to `ListenerBindFailure`),
  `AcceptRace<TAccepted>` (accept from several listening sockets, lose nothing on cancel or
  stop) and `StreamConnection` (the `IConnection` state rules over any `Stream`).
- The members of `TcpConnectionListener` and `SocketTransportControl`, which touch a
  socket, carry `[ExcludeFromCodeCoverage]` with a comment naming the socket call, and are
  exercised by the `[TestCategory("Integration")]` tests on `127.0.0.1` and `[::1]`, port 0.
- An IPv6 listening socket is IPv6-only on every platform, so `[::]` never also claims IPv4.
