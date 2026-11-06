# Surl.Protocol.Abstractions.UnitLibrary

The contracts every other project depends on. ADR-0004 (the listener seam and the exchange
context) is their specification; where this file and the ADR disagree, the ADR wins.

What it holds:

- `SurlExitCode` - the exit codes the `surl` process returns.
- `ListenUrl` - what `surl` was asked to listen on, with `BoundPort` once a listener has bound,
  and `UnixSocket`, a `UnixSocketAddress` (a path, or a Linux abstract name, with its
  `unix://` form) to listen on instead of a TCP port (ADR-0094, decision 6). A connection from
  a Unix socket client that bound no name has `UnnamedUnixSocketPeer.Instance` (`unix peer`)
  as its remote endpoint.
- The transports a protocol server receives: `IConnection` (stream-oriented) and
  `IDatagramFlow` (TFTP, and HTTP/3 over QUIC).
- The protocol server contracts: `IProtocolServer`, `IConnectionProtocolServer` and
  `IDatagramProtocolServer`.
- What a server is told about one exchange: `ExchangeContext`, and where its events go,
  `IExchangeLog` and `IExchangeLogFactory`.
- The hardening limits every server enforces (ADR-0006, section 6): `ExchangeLimits`, on
  `ExchangeContext.Limits`, and the optional refusal contracts a server implements to
  answer a connection or flow past a connection limit, `IConnectionRefusalWriter` and
  `IDatagramRefusalWriter` with `ConnectionRefusal`.
- How a server tells a limit's cancellation from shutdown (ADR-0059):
  `ExchangeContext.ShutdownToken`, set by the engine, and `ExchangeContext.IsCancelledForALimit`.
- The listener seam `Surl.Networking` implements: `IListenerFactory`,
  `IConnectionListener`, `IDatagramListener`, `SharedPortListeners` (both bound on one port by
  `IListenerFactory.StartSharedPortListenersAsync`, whose default starts TCP then UDP on its port;
  ADR-0082 decision 6), and a failure to bind,
  `ListenerBindException` with `ListenerBindFailure`.
- Test doubles every protocol test project reaches through its reference to this library:
  `InMemoryConnection` (replays an inbound byte script, records every byte written) and
  `RecordingExchangeLog` (records every log call as an `ExchangeLogEntry`). They are
  production code and held to the same coverage gates.

- The server-side TLS contract (ADR-0010): `IConnection.TlsSession` and
  `IConnection.UpgradeToTlsAsync`, `TlsSession`, `TlsHandshakeException`, and
  `TlsSchemes.IsImplicitTls`. `InMemoryConnection` stands in for implicit TLS (an initial
  session) and for an upgrade (`UpgradeRequested`, a configurable session, or a failure).
- The peek (ADR-0113, decision 6; BL-734): `IConnection.PeekByteAsync(wait, token)` returns the
  client's next byte without consuming it, or `null` when none came within `wait` or the peer
  half-closed, so a server can tell a TLS ClientHello (`0x16`) from plain text before
  `UpgradeToTlsAsync`. It is a default interface member that throws `NotSupportedException`, so
  the many test doubles that implement `IConnection` need not; a wrapper that forwards
  `ReadAsync` must forward it too. `InMemoryConnection` peeks the script; past its end it returns
  `null` at once when the client half-closes, and otherwise after `wait` on its `peekClock`.
- The authentication contract (ADR-0032, section 6): `IAuthenticationPolicy` (a password
  login, `PasswordLogin` judged as a `PasswordLoginVerdict`, one
  `IHttpAuthenticationSession` per HTTP connection, and `NamesAccount`, a default interface member
  matching nothing, which says whether a name is a named account's so the Azure Storage server can
  read a path-style target, ADR-0109 decision 2, BL-725), `HttpAuthenticationRequest` and
  `HttpAuthenticationVerdict` with `HttpAuthenticationOutcome` (and, for a login that binds
  the body, `IHttpRequestBodyCheck`, asked with the body's SHA-256 once it is read,
  ADR-0045), a refused verdict's `HttpAuthenticationRefusal` and a request's
  `AwsSigV4Service`, which makes Signature Version 4 for that service its only method (the S3
  server's, ADR-0108 Amendment 1), and a request's `AzureStorage`, an `AzureStorageScope` (the
  account a shared access signature names, the decoded resource path, the permission letters
  that grant the operation and the peer's address), which makes Azure's bearer token, Shared Key
  and shared access signatures its only methods (the Azure Storage server's, ADR-0109 decision 3;
  BL-717, which added the refusals from `MissingRequestDate` to `PermissionNotGranted`; BL-711 added
  `ExpiryOutOfRange` and `ConflictingCredentials` for presigned URLs, which also use `SignatureExpired`),
  an accepted verdict's `ChunkSignatures`, the `IHttpChunkSignatureChain` an `aws-chunked`
  `STREAMING-AWS4-HMAC-SHA256-PAYLOAD` body's chunks are checked against (ADR-0108 decision 4.5), and `CheckedLogin`, the
  credentials checked and the answer, whose `Note` a server writes to the verbose log
  (`Login accepted: <method> <user>`, ADR-0032 section 8). `Surl.Authentication`
  implements it; `AnonymousAuthenticationPolicy` accepts every login and lets every request
  proceed, and is the test double protocol tests share.
- The SASL login contract (ADR-0049, section 6, made protocol-neutral by ADR-0072 decision
  4), beside `IAuthenticationPolicy`: `ISaslAuthenticationPolicy` (the mechanisms a
  connection offers, asked with a `SaslOfferRequest`, and one `ISaslExchange` per login
  started from a `SaslExchangeStart`), each step a `SaslLoginStep` with its
  `SaslLoginOutcome`, and an accepted step's `ISaslSecurityLayer` when the server said it
  `CanCarrySecurityLayer` and the mechanism negotiated one, with any data its success carries
  (`AdditionalSuccessData`, such as LDAP `DIGEST-MD5`'s `rspauth`). `IMailAuthenticationPolicy`
  extends it with the mail-only members: the `MailLoginOffer` a mail server advertises and
  POP3's `ApopLogin`. The server owns the framing (base64, and the security layer's 4-byte
  lengths); the policy owns every mechanism. `AnonymousAuthenticationPolicy` implements it
  too: it offers `PLAIN` and the clear-password login, and ends every exchange
  `AcceptedUnchecked`, with no security layer, in the fewest steps (the initial response, or
  one empty challenge).
- The SSH login contract (ADR-0051, section 7), beside `IAuthenticationPolicy`:
  `ISshAuthenticationPolicy` judges an `SshNoneLogin`, an `SshPasswordLogin` (`password` or
  `keyboard-interactive`) and an `SshPublicKeyLogin` (with its `SshPublicKeyProof`: a query,
  or a signature the server has already verified or not), each as an `SshLoginVerdict` with
  its `SshLoginOutcome`. The server owns the RFC 4252 framing and the signature check; the
  policy owns the accounts, keys, delay and note. `AnonymousAuthenticationPolicy` implements
  it too: every login `AcceptedUnchecked` and every public-key query `KeyAcceptable`.
- The SMB login contract (ADR-0073, decision 3), beside `IAuthenticationPolicy`:
  `ISmbAuthenticationPolicy` judges an `SmbNtlmV1Login` (the user and domain as sent, the
  server challenge, the LM and NT responses and the TLS session; its `ToString` shows only
  the user and domain) as an `SmbLoginVerdict` with its `SmbLoginOutcome`. The server owns
  the SMB framing and the challenge; the policy owns the accounts, the NTLMv1 check, the
  delay and the note. `AnonymousAuthenticationPolicy` implements it too: every login
  `AcceptedUnchecked`.

- The FTP data-connection seam (ADR-0052, decision 9): `IDataConnectionOpener` (a passive
  listener, `IPassiveDataListener`, or an active connection, each an `IConnection`), a failure
  as `DataConnectionException` with its `DataConnectionFailure`, and
  `ExchangeContext.DataConnections`, whose default `RefusingDataConnectionOpener` refuses every
  request with `Unavailable`. `Surl.Networking` implements it over sockets. Its test double is
  `InMemoryDataConnections`: a script of passive listeners (each an
  `InMemoryPassiveDataListener` handing out one scripted `InMemoryConnection`, or none so the
  accept times out) and active connections or failures, with every request recorded as a
  `PassiveListenerRequest` or `ActiveConnectionRequest`.

- The proxies' origin seam (ADR-0088, decision 2; BL-551): `IOriginConnector` opens a
  connection to an origin named by host (DNS name or IP literal without brackets) and port,
  each failure an `OriginConnectionException` with its `OriginConnectionFailure`
  (`HostNotFound`, `NotAllowed`, `Refused`, `Unreachable`, `TimedOut`). `AllowedOriginRule`
  is the value the composition root builds and hands to `Surl.Networking`'s implementation:
  `LoopbackOnly` (the default) or `EveryAddress`, widened by `WithRange(network, prefix)` and
  narrowed by `WithProxyEndPoint` (the proxy's own listening end point, an unspecified address
  refusing its port on every address), IPv4-mapped compared as IPv4. Its test double is
  `InMemoryOriginConnector`: per-origin scripts (host compared without regard to case) of
  connections or failures, an unscripted origin `Refused`, every request recorded as an
  `OriginConnectionRequest`; it applies no rule, so a refused origin is scripted `NotAllowed`.

- The stand-in contracts (ADR-0124, decisions 1, 4 and 5; BL-823). The event stream:
  `ISurlEventPublisher` (`IsPublishingBytes`, `Publish`), the abstract record `SurlEvent`
  (`Sequence` and `At`, stamped by the stream, not the publisher) and its records
  `ExchangeStartedEvent`, `ExchangeBytesEvent` (with `ExchangeDirection`), `ExchangeEndedEvent`,
  `LoginJudgedEvent`, `StoreChangedEvent` (with `StoreKind` and `StoreChange`; a `Renamed`
  change carries its old name in `PreviousName`, ADR-0129),
  `FaultAppliedEvent` and `HookFailedEvent` (with `HookFailure`); no event carries a password,
  key or token. The fault plan: `IFaultPlan.FindAsync(FaultPoint, CancellationToken)` returning
  `ValueTask<FaultRule?>`, with `FaultPoint`, `FaultRule`, `FaultKind` and `FaultStage`. Both reach
  a server through `ExchangeContext.Events` and `ExchangeContext.Faults`, whose defaults
  `DiscardingSurlEventPublisher` (publishes nothing, asks for no bytes) and `EmptyFaultPlan`
  (finds nothing) keep every server as it was. Test doubles: `RecordingSurlEventPublisher`
  (keeps every event in order, stamps nothing, safe under concurrent publishes) and
  `ScriptedFaultPlan` (the first given rule whose stage and set scope attributes equal the
  point's, ordinally; never spends `Times`; records every point asked). `Surl.Core`'s
  `SurlEventStream` and `FaultPlan` implement them (BL-787, BL-792).

- Command-stage delay and drop (ADR-0141; BL-901): `IIdleClock` (`Pause`, `Restart`) reaches a
  server as `ExchangeContext.IdleClock`, default `NoIdleClock.Instance` (does nothing); the engine
  sets the exchange's own. `CommandStageFaults.FindAndApplyAsync(context, connection, point, target)`
  asks the plan and applies a `Delay` (hold `ms` on the context's clock with the idle clock paused)
  or a `Drop` (`IConnection.Abort()`), each noted and published once as a `FaultAppliedEvent`, and
  hands any other kind back. `RecordingIdleClock` is its test double. `ReportedListenUrl.Format` is
  the listen URL as the instance reports it, for events and a rule's `listener`.

- The seedable identifier source (ADR-0124, decision 6; BL-851): `IIdentifierRandom.NextBytes`,
  for non-secret generated identifiers only - never keys, nonces, challenges, salts or tokens. It
  reaches a server through `ExchangeContext.Identifiers`, whose default, the internal
  `RandomNumberGeneratorIdentifierRandom`, draws from `RandomNumberGenerator`, so every server
  behaves as before. `Surl.Core` holds the public sources: `CryptographicIdentifierRandom` and
  `SeededIdentifierRandom`.

- The datagram flow's test double (ADR-0082, decision 5): `InMemoryDatagramFlow` hands a
  server its first datagram and then the client's datagrams one per receive (given up front,
  or delivered with `DeliverFromClient` while the server runs), records every datagram sent
  in `SentDatagrams`, and waits past the end until cancelled or disposed. The HTTP/3 server's
  tests use it; TFTP's tests keep their own `ScriptedDatagramFlow` (BL-037, in
  `Surl.Protocol.Tftp.UnitTests`), whose silences advance a fake clock.

This library references nothing. Never construct a `Socket`, `TcpListener`, `UdpClient` or
`SslStream` here. `SurlExitCode` reuses upstream curl's `CURLE_*` number wherever a
server-side meaning carries over; add values, never renumber one. Every contract lands
with an ADR, and a change to one touches every protocol server, so contracts land first
and the protocols fan out after.
