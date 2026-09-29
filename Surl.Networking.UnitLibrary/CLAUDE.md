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
- `UdpDatagramListener` (public) - `StartAsync(ListenUrl, CancellationToken)` binds every
  address a listen URL names on one UDP port, the same way, and implements
  `IDatagramListener`. `DatagramDemultiplexer` reads the listen sockets and sorts datagrams
  by (listen socket, remote endpoint): a known peer's datagram goes to its flow, a new
  peer's opens a `DemultiplexedDatagramFlow`. A flow's `MoveToNewLocalPortAsync` binds a
  fresh ephemeral port on the listen address (RFC 1350's new transfer identifier); from then
  on the flow reads that socket itself, drops other endpoints' datagrams, and what its peer
  still sends to the listen port is dropped without opening a new flow. At most 64 opened
  flows wait for `AcceptFlowAsync` and 64 datagrams wait in a flow's listen-port inbox;
  beyond that datagrams are dropped, as a full socket buffer drops them. Disposing the
  listener stops new flows, but the listen sockets close only once no flow handed out still
  sends from them. `ConnectionReset` on a receive (Windows' report of an ICMP
  port-unreachable) is skipped; any other receive failure on a listen socket ends
  `AcceptFlowAsync` with `IOException`.
- `SocketListenerFactory` (public) - implements `IListenerFactory` by delegating
  to `TcpConnectionListener.StartAsync` and `UdpDatagramListener.StartAsync`; it is what the
  composition root hands the serving engine. It holds only the optional `ServerTlsSettings`
  it passes to every connection listener. Both one-line members are excluded from coverage
  and exercised by `SocketListenerFactoryTests` (Integration).
- Server-side TLS (ADR-0010). `ServerTlsSettings` (public, one per process) holds the
  certificate every listener serves - re-imported through PKCS#12 by
  `ServerCertificateImport` so Schannel can serve it, so its key must be exportable - its
  intermediates, and the `--cacert` trust anchors; with anchors, every handshake requires a
  client certificate that `ClientCertificateVerifier` chains to exactly those anchors, with
  no revocation check and the time from the `TimeProvider`. `ThrowawayServerCertificate`
  (public) makes the certificate served without `--cert`. `TcpConnectionListener.StartAsync`
  takes the settings and gives each connection a `ServerTlsHandshake` with the ALPN IDs
  `TlsApplicationProtocols` names for the scheme (`http/1.1` for `https` and `wss`, none
  otherwise). `StreamConnection.UpgradeToTlsAsync` runs it: a failed handshake is
  `TlsHandshakeException`, and a failed or cancelled one leaves the connection unusable, so
  disposing it writes nothing; `CompleteWritesAsync` on a secured connection sends
  close_notify before FIN. Versions are the operating system's defaults until BL-048.
  Reading the `--cert`, `--key` and `--cacert` files is BL-066.
- The TLS tests run real `SslStream` handshakes over `InMemoryDuplexStream` (test project)
  with certificates made by `CertificateRequest`, so they are fast tests on every platform;
  `TcpConnectionListenerTlsTests` (Integration) repeats one over a loopback socket.
- All of that runs over `IDatagramSocket`, so the fast tests drive it with a fake; only
  `UdpDatagramSocket` and the public `StartAsync` touch a socket, excluded from coverage and
  exercised by the `[TestCategory("Integration")]` tests.
