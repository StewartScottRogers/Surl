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
- How a server tells a limit's cancellation from shutdown (ADR-0059):
  `ExchangeContext.ShutdownToken`, set by the engine, and `ExchangeContext.IsCancelledForALimit`.
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
- The authentication contract (ADR-0032, section 6): `IAuthenticationPolicy` (a password
  login, `PasswordLogin` judged as a `PasswordLoginVerdict`, and one
  `IHttpAuthenticationSession` per HTTP connection), `HttpAuthenticationRequest` and
  `HttpAuthenticationVerdict` with `HttpAuthenticationOutcome` (and, for a login that binds
  the body, `IHttpRequestBodyCheck`, asked with the body's SHA-256 once it is read,
  ADR-0045), and `CheckedLogin`, the
  credentials checked and the answer, whose `Note` a server writes to the verbose log
  (`Login accepted: <method> <user>`, ADR-0032 section 8). `Surl.Authentication`
  implements it; `AnonymousAuthenticationPolicy` accepts every login and lets every request
  proceed, and is the test double protocol tests share.
- The mail login contract (ADR-0049, section 6), beside `IAuthenticationPolicy`:
  `IMailAuthenticationPolicy` (the `MailLoginOffer` a mail server advertises, one
  `ISaslExchange` per `AUTH`/`AUTHENTICATE` started from a `SaslExchangeStart`, and POP3's
  `ApopLogin`), each step a `MailLoginStep` with its `MailLoginOutcome`. The server owns the
  base64 framing; the policy owns every mechanism. `AnonymousAuthenticationPolicy` implements
  it too: it offers `PLAIN` and the clear-password login, and ends every exchange
  `AcceptedUnchecked` in the fewest steps (the initial response, or one empty challenge).
- The SSH login contract (ADR-0051, section 7), beside `IAuthenticationPolicy`:
  `ISshAuthenticationPolicy` judges an `SshNoneLogin`, an `SshPasswordLogin` (`password` or
  `keyboard-interactive`) and an `SshPublicKeyLogin` (with its `SshPublicKeyProof`: a query,
  or a signature the server has already verified or not), each as an `SshLoginVerdict` with
  its `SshLoginOutcome`. The server owns the RFC 4252 framing and the signature check; the
  policy owns the accounts, keys, delay and note. `AnonymousAuthenticationPolicy` implements
  it too: every login `AcceptedUnchecked` and every public-key query `KeyAcceptable`.

- The FTP data-connection seam (ADR-0052, decision 9): `IDataConnectionOpener` (a passive
  listener, `IPassiveDataListener`, or an active connection, each an `IConnection`), a failure
  as `DataConnectionException` with its `DataConnectionFailure`, and
  `ExchangeContext.DataConnections`, whose default `RefusingDataConnectionOpener` refuses every
  request with `Unavailable`. `Surl.Networking` implements it over sockets. Its test double is
  `InMemoryDataConnections`: a script of passive listeners (each an
  `InMemoryPassiveDataListener` handing out one scripted `InMemoryConnection`, or none so the
  accept times out) and active connections or failures, with every request recorded as a
  `PassiveListenerRequest` or `ActiveConnectionRequest`.

There is no in-memory datagram flow here: TFTP is its only user, so its tests drive the
server through their own hand-written `ScriptedDatagramFlow` (BL-037, in
`Surl.Protocol.Tftp.UnitTests`).

This library references nothing. Never construct a `Socket`, `TcpListener`, `UdpClient` or
`SslStream` here. `SurlExitCode` reuses upstream curl's `CURLE_*` number wherever a
server-side meaning carries over; add values, never renumber one. Every contract lands
with an ADR, and a change to one touches every protocol server, so contracts land first
and the protocols fan out after.
