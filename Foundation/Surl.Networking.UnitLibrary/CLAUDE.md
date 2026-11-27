# Surl.Networking.UnitLibrary

The production transports behind the seams in `Surl.Protocol.Abstractions`: accepting TCP
and Unix domain socket connections and UDP datagrams on the addresses a listen URL names;
server-side TLS on those connections (certificates and keys, client-certificate
verification for upstream curl's `--cert`, ALPN) through `SslStream`, or through
`Surl.TlsSrp` or `Surl.Tls13Stream` in the cases below; FTP data connections; and a
proxy's connections to origins. No proxy protocol server is here: the HTTP `CONNECT` and
HTTPS proxy is `Surl.Protocol.HttpProxy`, and the SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h
proxy is `Surl.Protocol.Socks`.

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
  stop), `AcceptFailureClassifier` (which accept failures belong to one client) and
  `StreamConnection` (the `IConnection` state rules over any `Stream`).
- Accept failures (ADR-0022, BL-057). `AcceptRace` absorbs an accept that failed for one
  client - `ConnectionReset`, `ConnectionAborted`, `HostDown`, `HostUnreachable`,
  `NetworkUnreachable`, or an `AcceptedSocketLostException` (accepted, but gone before
  `NoDelay` and its endpoints could be read; the socket is already released) - and accepts
  again. Any other `SocketException` is `IOException`, so `TcpConnectionListener.AcceptAsync`
  throws only when the listening socket itself failed, on cancellation, or after disposal.
- The members of `TcpConnectionListener` and `SocketTransportControl`, which touch a
  socket, carry `[ExcludeFromCodeCoverage]` with a comment naming the socket call, and are
  exercised by the `[TestCategory("Integration")]` tests on `127.0.0.1` and `[::1]`, port 0.
- An IPv6 listening socket is IPv6-only on every platform, so `[::]` never also claims IPv4.
- Lingering close (ADR-0021, BL-056). `StreamConnection.DisposeAsync`, after FIN, reads and
  discards what the client still sends - from the raw transport, below any TLS - until it
  half-closes or resets or `StreamConnection.LingeringCloseTime` (2 s, on the injected
  `TimeProvider`) passes, so bytes the server never read do not turn the close into a
  reset. Not after `Abort`, and not when completing writes failed.
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
- QUIC over UDP (ADR-0082, decisions 5 and 6; BL-381). A QUIC connection rides one
  `UdpDatagramListener` flow, one per remote endpoint (curl never migrates and the server
  disables migration), so the listener needs nothing QUIC-specific; a flow counts as one
  connection against `--max-connections` and `--max-connections-per-address`, which
  `Surl.Core`'s serving engine enforces, refusing through the server's
  `IDatagramRefusalWriter` and noting the refusal outside any exchange (ADR-0028).
  `UdpDatagramListenerConnectionLimitTests` drive the engine over this listener on fake sockets
  to show it, which is why `Surl.Networking.UnitTests` references `Surl.Core.UnitLibrary`.
  `SharedPortListenerPair` (public) - `StartAsync(ListenUrl, ServerTlsSettings?, CancellationToken)`
  binds a `TcpConnectionListener` and then a `UdpDatagramListener` on the same addresses and the
  port the TCP one got (an `https` listen URL serving HTTP/3). With port 0 a taken UDP port
  releases both and the pair is tried again, up to `MaxBindAttempts` (2048, enough to cross Windows' adjacent excluded UDP blocks, since rebinding walks consecutive TCP ports; BL-1088) times in all; any other
  UDP failure, or a taken UDP port with a fixed port, releases the TCP listener and is thrown as
  the `ListenerBindException` it was. The UDP listener's listen URL names the shared port as its
  port. Fast-tested over fake starts; `SharedPortListenerPairIntegrationTests` bind `127.0.0.1:0`.
- Unix domain sockets (ADR-0094, decisions 3, 5 and 6; BL-547). `UnixSocketConnectionListener`
  (public) - `StartAsync(ListenUrl, ServerTlsSettings?, CancellationToken)` listens on the
  listen URL's `UnixSocket`: a socket file, or on Linux an abstract name. Refused before any
  socket is made, by `UnixSocketBindRules` (fast-tested on every platform's arm): an abstract
  name off Linux (`AbstractUnixSocketUnsupported`), a path or name past `sun_path` in UTF-8
  bytes - 107 on Linux and Windows, 103 on macOS (`UnixSocketPathTooLong`), anything already at
  the path, never deleted (`AddressInUse`), and a directory that is not there
  (`AddressNotAvailable`). The file is made `0600` between bind and listen off Windows, and
  deleted at stop only while its write time is still the one the bind gave it. The bind goes
  through `UnixSocketFileEndPoint`, because `Socket.Dispose` deletes the file of a socket bound
  to a `UnixDomainSocketEndPoint` whatever stands there by then. Each connection's local
  endpoint is the listener's `UnixDomainSocketEndPoint`; a client that bound no name (curl) is
  `UnnamedUnixSocketPeer.Instance`. No `NoDelay` (it does not exist on `AF_UNIX`). The members
  touching the socket or file are covered by
  `UnixSocketConnectionListenerTests`, fast tests on a socket file under `Path.GetTempPath()`; only `MakeOwnerOnly`, `RemoteEndPointOf` and `AcceptOrDropClient` stay excluded, each with the reason no fast test on Windows reaches it.
- `SocketListenerFactory` (public) - implements `IListenerFactory` by delegating
  to `TcpConnectionListener.StartAsync` (or `UnixSocketConnectionListener.StartAsync` when the
  listen URL's `UnixSocket` is set) and `UdpDatagramListener.StartAsync`; it is what the
  composition root hands the serving engine. It holds only the optional `ServerTlsSettings`
  it passes to every connection listener and a `TimeProvider`. Both one-line members are
  excluded from coverage and exercised by `SocketListenerFactoryTests` (Integration). Its `StartSharedPortListenersAsync`
  (an `https` listen URL with `--http3`) binds through `SharedPortListenerPair`, so a port-0 pair
  whose UDP port is taken is retried; it too is excluded and exercised by
  `SharedPortListenerPairIntegrationTests`.
- Server-side TLS (ADR-0010). `ServerTlsSettings` (public; `Surl.Composition` makes at most
  one for each server start, so a process running several servers can hold several) holds the
  certificate every listener given it serves - re-imported through PKCS#12 by
  `ServerCertificateImport` so Schannel can serve it, so its key must be exportable - its
  intermediates, and the `--cacert` trust anchors; with anchors, every handshake requires a
  client certificate that `ClientCertificateVerifier` chains to exactly those anchors, with
  no revocation check and the time from the `TimeProvider`. `ThrowawayServerCertificate`
  (public) makes the certificate served without `--cert`. `TcpConnectionListener.StartAsync`
  takes the settings and gives each connection a `ServerTlsHandshake` with the ALPN IDs
  `TlsApplicationProtocols.ForListener` names for the listen URL (`h2` then `http/1.1` for
  `https`, `http/1.1` for `wss`, none otherwise; ADR-0081 decision 2), and an `https`
  listen URL with `ListenUrl.IsProxy` - the HTTPS proxy - offers `h2` then `http/1.1` too
  (ADR-0088 Amendment 9, which supersedes Amendment 2's `http/1.1`-only offer). `ServerTlsHandshakeHttpsProxyTests` shake hands as curl does with such a
  listener and serve one request through `Surl.Protocol.HttpProxy`, which is why
  `Surl.Networking.UnitTests` references that library. `StreamConnection.UpgradeToTlsAsync` runs it: a failed handshake is
  `TlsHandshakeException`, and a failed or cancelled one leaves the connection unusable, so
  disposing it writes nothing; `CompleteWritesAsync` on a secured connection sends
  close_notify before FIN.
- TLS-SRP (ADR-0122 decision 5; BL-567). `ServerTlsSettings.ForTlsSrp(accounts, random, certificate)`
  makes settings whose `TlsSrpAccounts` is set. `certificate`, a `TlsSrpServerCertificate`, is
  optional (`null` by default): it signs the SRP key exchange, so without it only the unsigned
  suites are served and with it the certificate-signed ones too (ADR-0122 decision 2).
  `ServerTlsComposition.ComposeTlsSrp` in `Surl.Composition` passes one when `--cert` or
  `--self-signed` gives a certificate. Under these settings `ServerTlsHandshake` runs
  `Surl.TlsSrp`'s `TlsSrpServer` over the transport instead of `SslStream`, on every listener and
  every `UpgradeToTlsAsync`. The session is TLS 1.2, the agreed SRP suite (IANA numbers, as
  `TlsCipherSuite` has them), ALPN `http/1.1` or none, SNI, no client certificate, and
  `TlsSession.SrpUserName`. A `TlsAlertException` or an `IOException` (a client alert, the client
  closing) is `TlsHandshakeException`. `CompleteWritesAsync` sends close_notify through
  `TlsSrpStream.ShutdownAsync`. `StreamConnectionTlsSrpTests` replay upstream curl's recorded
  bytes, linked from `Cryptography/Surl.TlsSrp.UnitTests/Fixtures`. This is why the project
  references `Surl.TlsSrp.UnitLibrary`.
- The login judge (ADR-0165, decision 2; BL-1223). `StreamConnection.UseTlsLoginJudge` keeps an
  `ITlsLoginJudge`; `UpgradeToTlsAsync` passes it to `ServerTlsHandshake.AuthenticateAsync`, which
  wraps it in `TlsSrpLoginJudgeAdapter` for `TlsSrpServer`. An `SslStream` or TLS 1.3 handshake never
  calls it. `StreamConnectionTlsLoginJudgeTests` cover it.
- The peek and the read-ahead buffer (ADR-0113, decision 6; BL-734; ADR-0141 amendment 2, decision 2;
  BL-1530). `StreamConnection` keeps one transport read in flight, never cancelled, and the bytes it
  has read ahead in order. `PeekByteAsync` starts a one-byte read and waits for it up to `wait` on the
  injected `TimeProvider`; a byte arriving after the wait is kept for the next peek or read.
  `WatchForPeerCloseAsync(maxBufferedBytes, token)` reads ahead until the peer half-closes
  (`PeerClosed`), the read throws `IOException` (`PeerReset`; the faulted read stays, so the next
  `ReadAsync` throws it too, after the bytes read ahead) or `maxBufferedBytes` are held
  (`BufferFull`); cancelling the token throws `OperationCanceledException` and loses nothing. The
  next `ReadAsync` returns the read-ahead bytes first, in order. `UpgradeToTlsAsync` refuses while a
  read is still waiting and otherwise hands the handshake a `ReadAheadPrefixStream` - the read-ahead
  bytes, then the transport - so the ClientHello arrives whole. `DisposeAsync` waits for the read in
  flight (within the lingering time) before discarding. `StreamConnectionPeekTests` and
  `StreamConnectionWatchTests` (a scripted stream: bytes then FIN, bytes then reset) cover it.
- TLS minimums (ADR-0006, section 4; BL-048). `ServerTlsSettings.AcceptedVersions` is a
  `TlsVersionRange` (public): a lowest and a highest of TLS 1.0, 1.1, 1.2, 1.3, TLS 1.2 to
  1.3 by default; a lowest above the highest is `ArgumentException`.
  For `SslStream` only (`ServerTlsSettings.CreateAuthenticationOptions`): the handshake enables
  the range's `AcceptedProtocols`, refuses renegotiation (`AllowRenegotiation = false`) and never
  sets `CipherSuitesPolicy`, so its cipher suites are the operating system's. TLS-SRP ignores
  `AcceptedVersions`: it is TLS 1.2 only, with `Surl.TlsSrp`'s SRP suites, and `TlsSrpStream`
  answers a renegotiation attempt (a handshake record after the handshake) with a
  `no_renegotiation` warning. The hand-built `Tls13ServerStream` is TLS 1.3, which has no
  renegotiation, with `Surl.Tls13`'s cipher suites; it serves only when the range includes
  TLS 1.3, and the range's `Lowest` decides which ClientHellos it takes (the TLS 1.3 bullet
  below). The head timeout is the caller's: `Surl.Core` passes `UpgradeToTlsAsync` a token
  cancelled at `ExchangeLimits.HeadTimeout` and writes the log note.
  `Foundation/Surl.Networking.UnitTests/Fixtures` holds the upstream curl recordings showing its
  defaults complete with renegotiation off.
- Reading the TLS option files (ADR-0010, sections 3 and 5), at startup before any listener
  binds. `ServerCertificateFileLoader.Load` reads `--cert` as `ServerCertificateFormat`
  `Pem`, `Der` or `P12` and `--key` as `ServerKeyFormat` `Pem` or `Der`, with `--pass`, into a
  `LoadedServerCertificate` (certificate with an exportable key, intermediates in file
  order) for `ServerTlsSettings`; `ServerPrivateKeyImport` joins the key (PKCS#8, encrypted
  PKCS#8, PKCS#1, SEC1) and refuses any key but RSA 2048+ and ECDSA P-256/384/521.
  `ClientTrustAnchorFileLoader.Load` reads `--cacert` (PEM blocks or one DER certificate).
  Every failure is `TlsFileLoadException` carrying a `TlsFileLoadFailure` -
  `ServerCertificateUnusable`, `CaCertificateNotFound`, `CaCertificateUnreadable` - which
  `Surl.Composition`'s `ServerTlsComposition.DescribeTlsFileFailure` maps to
  `CertificateProblem` (58), `FailedInit` (2) and `CaCertificateBadFile` (77). Parsing the
  `--cert-type`/`--key-type` words and refusing `--key` with `P12` is the command line's job;
  the loader takes the enums. Tests write generated certificates and keys under
  `Path.GetTempPath()` (`TemporaryTlsFiles`).
- The TLS tests run real `SslStream` handshakes over `InMemoryDuplexStream` (test project)
  with certificates made by `CertificateRequest`, so they are fast tests on every platform;
  `TcpConnectionListenerTlsTests` (Integration) repeats one over a loopback socket.
- FTP data connections (ADR-0052, decisions 5, 6 and 9; BL-175). `SocketDataConnectionOpener`
  (public, constructed with the control listener's `ServerTlsSettings` and an optional
  `TimeProvider`) implements `IDataConnectionOpener`. `StartPassiveListenerAsync` binds the
  control connection's local address (as IPv4 when IPv4-mapped) on an ephemeral port and
  returns a `SocketPassiveDataListener`: its `AcceptAsync` hands out the first connection whose
  address is the control connection's peer (`DataConnectionPeer`, IPv4-mapped compared as IPv4),
  resets every other connection unseen and goes on waiting, is `TimedOut` at the timeout (on the
  `TimeProvider`), `Unreachable` when the listening socket fails, absorbs one client's accept
  failure through `AcceptRace` (ADR-0022), and stops listening once it has handed its
  connection out (a second `AcceptAsync` is `InvalidOperationException`). `ConnectActiveAsync`
  refuses (`Refused`) any target but the peer's address on port 1024 or above without
  connecting, is `TimedOut` at the timeout and `Unreachable` on a `SocketException`. Every data
  connection is a `StreamConnection` (lingering close included) whose `UpgradeToTlsAsync` uses
  the server's settings with no ALPN. A bind failure or a non-IP control end point is
  `Unreachable`. Only `SocketDataConnectionSockets` (behind `IDataConnectionSockets` and
  `IPassiveDataSocket`) touches a socket, excluded from coverage and exercised by
  `SocketDataConnectionOpenerIntegrationTests`; the rules are fast-tested over
  `FakeDataConnectionSockets`.
- All of that runs over `IDatagramSocket`, so the fast tests drive it with a fake; only
  `UdpDatagramSocket` and the public `StartAsync` touch a socket, excluded from coverage and
  exercised by the `[TestCategory("Integration")]` tests.
- Proxy origin connections (ADR-0088, decision 2; BL-551). `SocketOriginConnector` (public,
  constructed with the `AllowedOriginRule` and an optional `TimeProvider`) implements
  `IOriginConnector`: it resolves the host with `Dns` (`HostNotFound` on failure or no
  address), keeps only the addresses the rule admits - resolving before checking, so DNS
  rebinding cannot pass the rule (`NotAllowed` when none is left, without connecting) - and
  connects to them in the resolver's order until one accepts; when all fail the last failure
  is reported, `Refused` for a refused connect and `Unreachable` for any other socket error.
  The timeout covers resolution and every connect (`TimedOut`); the caller's cancellation is
  `OperationCanceledException`. Each origin connection is a `StreamConnection` with no TLS of
  its own. `SocketOriginSockets` (behind `IOriginSockets`) makes the calls, reusing
  `SocketDataConnectionSockets.ConnectAsync`; the rules are fast-tested over
  `FakeOriginSockets`, and `SocketOriginConnectorLoopbackTests` are fast tests on real
  loopback sockets (a listener reached by `127.0.0.1` and `localhost`, a closed port, a
  `.invalid` name, a refused origin), which BL-551 asked for outside `Integration`.
- TLS client connections to an operator-named host (ADR-0149 decisions 5 and 11; BL-1068).
  `TlsClientConnector` (public, constructed with the extra trust-anchor certificates and an
  optional `TimeProvider`) resolves and connects as `SocketOriginConnector` does but with **no
  allowed-origin rule**, then returns a `TlsClientConnection` (`Stream`, `IsSecured`,
  `StartTlsAsync`): handshake at once (`ldaps`) or later on the open stream (StartTLS). TLS 1.2
  and 1.3 only, no client certificate, no renegotiation, no revocation check. `TlsServerIdentityCheck`
  accepts a certificate only when its chain builds to a system root or one of the given
  certificates (`X509ChainTrustMode.CustomRootTrust`) and it names the host; no option skips
  either. Failures are `TlsClientException` with a `TlsClientFailure` (`HostNotFound`, `Refused`,
  `Unreachable`, `TimedOut`, `UntrustedCertificate`, `CertificateNameMismatch`,
  `HandshakeFailed`); messages name host, port and reason, never bytes the caller sent. A failed
  or cancelled handshake drops the transport. Tests: `TlsClientConnectorTests`, over in-memory
  pipes with one loopback-socket case.
- TLS 1.3 where `SslStream` cannot serve it (ADR-0157 decisions 1 to 3 and amendment 1;
  BL-1147). `ServerTlsHandshake` looks for TLS-SRP first: settings with `TlsSrpAccounts` go to
  `TlsSrpServer` and none of this applies. Otherwise, when `ITls13Support`
  (`PlatformTls13Support`: macOS only) says `SslStream` has no TLS 1.3 server, the settings serve
  a certificate and `AcceptedVersions` includes TLS 1.3, it reads the first record
  (`ClientHelloPeek`) and chooses by it:
  - A parsed ClientHello whose `supported_versions` lists TLS 1.3 and nothing from SSL 3.0 to
    TLS 1.2 goes to `Surl.Tls13Stream`'s `Tls13ServerStream`.
  - On a listener limited to TLS 1.3 (`AcceptedVersions.Lowest` is TLS 1.3), every parsed
    ClientHello that lists TLS 1.3 goes to `Tls13ServerStream`, whatever else it lists. A parsed
    one that does not (one with no `supported_versions` included) gets a fatal
    `protocol_version` alert and the handshake is `TlsHandshakeException`.
  - Everything else goes to `SslStream` with the bytes read replayed by `ReplayedPrefixStream`,
    whose zero-length read returns at once while bytes remain (SslStream starts with one).
    Whether or not the listener is limited to TLS 1.3, that includes whatever `ClientHelloPeek`
    cannot parse, with no alert from this project first: another record type, a record over 16384
    bytes, a record cut short, or a fragment `Surl.Tls13`'s `ClientHello.Read` refuses (such as
    a ClientHello split across records, a `legacy_version` other than `0x0303`, or compression
    other than null).

  `Tls13ServerStream` gets the bytes read through its `alreadyRead`; `TlsSession` is filled from
  the stream, its client certificate the verified leaf; with `--cacert` the stream requests a
  client certificate and `ClientCertificateVerifier.IsChainTrusted` verifies it, the chain's
  intermediates as extra store only (BL-1470). Without TLS-SRP, where any of the three
  conditions for reading the first record is false, the handshake is `SslStream`'s and nothing is
  read ahead. On such a connection `CompleteWritesAsync` sends close_notify through
  `Tls13ServerStream.ShutdownAsync` before FIN (BL-1887; `StreamConnectionTls13Tests`). This is
  why the project references `Surl.Tls13Stream.UnitLibrary`; tests use
  `ClientHelloBytes` and fake `ITls13Support`.
