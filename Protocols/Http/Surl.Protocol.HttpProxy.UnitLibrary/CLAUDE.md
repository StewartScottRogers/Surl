# Surl.Protocol.HttpProxy.UnitLibrary

The HTTP proxy server: what curl's `-x`/`--proxy` and `-p`/`--proxytunnel` talk to. Decided in
ADR-0088 (decisions 1 to 6 and 7's limits) and its Amendments 1 to 4. Built by BL-552, BL-554 (the
HTTPS proxy), BL-553 (`Proxy-Authorization`) and BL-594 (`ftp://` forwarding); the upstream cases' server half
(BL-556) is built too. `surl` composes the proxy for its `--proxy` listen URLs (`Surl.Composition`'s
`ProxyComposition`, BL-1003).

**The upstream test cases' proxy half** (ADR-0080 decision 2; BL-556) is a test seam the shipped `surl` never
composes: `Surl.Conformance.UnitTests` composes `HttpProxyUpstreamTestCaseServer` with one case's
`HttpProxyUpstreamTestCaseReplies` (its `<data*>` and `<connect*>` parts by name, and its `<servercmd>` lines; only
`Testnum` is accepted, since no case naming `http-proxy` or `https-proxy` has another) and an `IOriginConnector`. It
reads each request head with `Surl.HttpMessage`'s reader and its body as framed, then sends the part
`HttpProxyUpstreamTestCasePartNumber` picks (a copy of the HTTP half's rules: the protocol libraries may not reference each
other) verbatim; `swsbounce` and `swsclose` act as in the HTTP half. A `CONNECT` whose part is a `2xx` response opens a
tunnel to `127.0.0.1` at the `CONNECT`'s port, whatever host it names, as upstream's `sws` does, and relays with
`ProxyTunnel`; any other part is just sent. `HttpProxyUpstreamTestCaseRecordingConnection` records what the proxy reads, on
each connection until its tunnel opens, as `HttpProxyUpstreamTestCaseReplies.ReceivedBytes`: the case's `<proxy>`.

**Proxy login** (Amendment 3) is `HttpProxyExchange.Login.cs`: one `IHttpAuthenticationSession`
per curl connection, from the `IAuthenticationPolicy` the two-argument constructor takes (the
one-argument constructor passes `AnonymousAuthenticationPolicy`: no proxy account, no login).
Every request and `CONNECT` is judged after the `Host` check and before anything else, with
`Authorization` removed and `Proxy-Authorization` read as `Authorization`
(`ProxyFieldRules.ProxyLoginFields`), and as the target what curl puts in a Digest `uri`: a
`CONNECT`'s authority, an absolute-form request's path and query. A challenge is `407` with one
`Proxy-Authenticate` per value and keeps the connection; `Forbidden` is `403` and closes.

**Fault rules** (ADR-0160): `ProxyCommandFault` asks the plan once per `CONNECT`, forwarded request and
HTTP/2 stream, after the login and before the origin connection, with scheme `http-proxy` or
`https-proxy`; a delay holds, a drop sends nothing (HTTP/2: the stream is reset), and a chosen error is
the proxy's own answer with the rule's status. A chosen `407`, whatever its reason phrase, carries one
`Proxy-Authenticate` per value of the login session's `CreateChallenges()` (Amendment 2, BL-1725), on
HTTP/2 too, NTLM and Negotiate sent whole with no stream reset; a session offering none leaves it without
the field and the exchange log notes it. No other chosen status, `401` included, gains a challenge.

**The HTTPS proxy** is this same server on a connection the serving engine secured before
`ServeAsync` (an `https` listen URL with `ListenUrl.IsProxy`, whose listener offers ALPN
`h2` then `http/1.1`, ADR-0088 Amendment 9). `HttpProxyExchange` notes the TLS once before the first request; a session that
agreed any ALPN but `h2` or `http/1.1` is closed unwritten (Amendment 2). `HttpProxyProtocolServerTlsTests`
drive it with an `InMemoryConnection` given an initial `TlsSession`.

**HTTP/2** (BL-940; what curl's `--proxy-http2` asks for): a connection whose TLS agreed ALPN `h2`
is served by `Surl.Http2`'s `Http2ConnectionServer` with `Http2ProxyStreamHandler`
(`HttpProxyExchange.Http2.cs`, which notes the `h2` choice, a preface that is not one and a head
timeout). Each stream is judged by the connection's proxy login first; a `CONNECT` stream
(RFC 9113 section 8.5) is answered `:status 200` and relayed in `DATA` frames by `Http2ProxyTunnel`;
any other `:scheme http` stream is forwarded to its origin over HTTP/1.1 on its own origin
connection and the response relayed back on the stream (a `101` from the origin is `502`; another
scheme is `501`). `AlreadyReadBytesConnection` hands `Http2ConnectionServer` the bytes read past the
connection preface. `HttpProxyProtocolServerHttp2Tests` drive it
with a `TlsSession` whose `ApplicationProtocol` is `h2`, replaying bytes recorded from pinned
upstream curl in `Fixtures/https-proxy-h2-*`.

**Listen URL schemes:** `http`, `https` - but the proxy is chosen by `--proxy`, not by scheme.

What exists:

- `HttpProxyProtocolServer` - the `IConnectionProtocolServer`, `IConnectionRefusalWriter`
  (`503`) and `IExchangeBytesRedactingServer`. Takes the `IOriginConnector` every origin is reached
  through; it never decides an address itself (the connector applies the allowed-origin rule). Its
  remarks state every answer.
- `HttpProxyConnectionBytesRedactor` - the exchange-log redactor `CreateRedactor` returns
  (ADR-0182 Amendment 8): until the client's HTTP/2 preface it hands the bytes to
  `HttpProxyRequestOrTunnelBytesRedactor`; a preface where a request may start hands the rest to
  `Surl.Http2`'s `Http2RequestBytesRedactor`, which withholds the `DATA` payloads of a `CONNECT`
  stream (Amendment 11). Both it and the request head redactor are given
  `HttpWithheldHeaderNames.ApiKeysAndSessionTokens`, so every request through the proxy has the
  API-key and session-token fields withheld beside the credential fields (Amendment 18).
- `HttpProxyRequestOrTunnelBytesRedactor` - masks HTTP/1.x request heads until the bytes are a
  tunnel's text: a line where a request may start that begins with a letter, `1`-`9` or a line feed
  and is no CR LF request line, a `CONNECT` head line without CR LF, or what follows a `CONNECT`
  head declaring a body. From there `HttpProxyTunnelLinesRedactor` judges every line with
  `HttpProxyTunnelLineRedactionRule`, which combines
  `HttpProxyTunnelPasswordAndCredentialHeaderLineRule`, `HttpProxyTunnelImapLoginLineRule` and
  `Surl.LineProtocol`'s `SaslLineRedactionRule`, so FTP and mail logins in a `CONNECT` tunnel are
  masked (Amendment 16). The headers tunnelled text masks are those of
  `HttpWithheldHeaderNames.All`: the credential fields after the scheme word, the API-key and
  session-token fields whole (Amendment 18).
- `HttpProxyWithheldBytesRedactor` - takes over when the preface starts anywhere else: one `****`
  for every later byte.
- `HttpProxyExchange` - one curl connection: reads request heads with `Surl.HttpMessage`'s
  `HttpConnectionReader`, keeps one `OriginLink` between requests. `HttpProxyExchange.Forwarding.cs`
  forwards absolute-form `http://` requests (origin-form, `Host` from the authority, bodies,
  `Expect: 100-continue`, interim responses, `101`, the one reconnect, the relayed response);
  `HttpProxyExchange.Ftp.cs` fetches an `ftp://` `GET` over FTP (Amendment 4) through
  `FtpControlConnection` (the control connection's commands and replies) and `FtpFetch` (the
  login, path, type and transfer command read from the request), the data connection opened
  passively to the origin's own host, and answers `200` chunked, `502` or `504`;
  `HttpProxyExchange.Connect.cs` answers `CONNECT` with exactly
  `HTTP/1.1 200 Connection established\r\n\r\n` and tunnels.
- `ProxyTarget` - absolute-form and authority-form targets. `ProxyFieldRules` - the fields removed
  and added in each direction (`Via: 1.1 surl`). `OriginLink`, `OriginResponseHead`,
  `OriginResponseBody` - reading the origin's response. `ProxyBodyCopier` - length, chunked and
  close-delimited bodies. `ProxyTunnel` - the two-way byte relay with half-closes.
  `HttpProxyStatus` - `502`, `504`, the `CONNECT` reply and ADR-0088 decision 2's failure table.

`Protocols/Http/Surl.Protocol.HttpProxy.UnitTests/Fixtures` holds exchanges recorded from pinned
upstream curl 8.21.0 with the recorder playing the proxy; its README says how each was recorded.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.HttpMessage.UnitLibrary`,
and the horizontal `Surl.Http2.UnitLibrary`, `Surl.Hpack.UnitLibrary` (ADR-0086) and
`Surl.LineProtocol.UnitLibrary` (ADR-0182 Amendment 16) only. ADR-0088 also allows `Surl.Authentication.UnitLibrary`, but the proxy reaches its checks
through `IAuthenticationPolicy` as the HTTP server does, so it needs no reference (Amendment 3).
Referencing another protocol server is a build break, and
`Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener` here: curl's
connection comes from the listener seam and every origin connection from `IOriginConnector`, so the
tests drive the server through `InMemoryConnection` and `InMemoryOriginConnector`.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
