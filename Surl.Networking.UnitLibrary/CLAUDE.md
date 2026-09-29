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
- All of that runs over `IDatagramSocket`, so the fast tests drive it with a fake; only
  `UdpDatagramSocket` and the public `StartAsync` touch a socket, excluded from coverage and
  exercised by the `[TestCategory("Integration")]` tests.
