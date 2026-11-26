# Surl.Protocol.Socks.UnitLibrary

The SOCKS server: what curl's `--socks4`, `--socks4a`, `--socks5`, `--socks5-hostname` and
`-x`/`--proxy` with a `socks4://`, `socks4a://`, `socks5://` or `socks5h://` URL talk to. Decided
in ADR-0121 (decisions 1 to 4 and 6). Built by BL-558 (SOCKS4 and SOCKS4a) and BL-559 (SOCKS5,
SOCKS5h and RFC 1929 accounts). BL-560 composes it in `Surl.Composition`
(`ProtocolServerRegistrations.Original`, over `ComposedProtocolServices.OriginConnector` and
`ProxyAuthenticationPolicy`), so `surl socks5://127.0.0.1:1080` and `-x socks5://...` serve it; the Unix
domain socket (BL-561) is still to come; the upstream cases' server half is built (BL-562, below). GSS-API (BL-765, ADR-0121 amendment 2) is built but no command-line option hands the server a keytab yet.

**Listen URL schemes:** `socks4`, `socks4a` - both serve SOCKS4 and SOCKS4a alike, and answer a
SOCKS5 greeting `05 FF`; `socks5`, `socks5h` - both serve SOCKS5 with every address type, and
answer a SOCKS4 request `00 5B`.

What exists:

- `SocksProtocolServer` - the `IConnectionProtocolServer`. Takes the `IOriginConnector` every
  origin is reached through (it never decides an address itself; the connector applies the
  allowed-origin rule) and, optionally, the proxy accounts as an `IAuthenticationPolicy`
  (`null` means the listener has none). Its remarks state every answer.
- `SocksExchange` - one curl connection: the version byte, which the listener's scheme serves or
  refuses, then SOCKS4's head-timeout-bounded request read, its refusals (`00 5B`), its grant
  (`00 5A`) and the relay. `Socks5Exchange` - SOCKS5 after its version byte: method selection,
  the RFC 1929 login, the request, the reply codes `00` to `08` and the relay.
- `Socks4RequestReader`, `Socks4Request`, `Socks4ReadOutcome` - the SOCKS4 request after its
  version byte, each name bounded at 255 bytes. `Socks5RequestReader`, `Socks5Request`,
  `Socks5ReadOutcome` - the SOCKS5 request. `Socks5Reply`, `Socks5ReplyCode` - every SOCKS5 and
  RFC 1929 reply's bytes. `SocksProxyAccounts` - whether a client may proceed without an account
  (no accounts, or the policy accepts an anonymous login unchecked: `--allow-anonymous`).
- `SocksGssApiLogin` - the Kerberos acceptor and service name that let a SOCKS5 listener select method `01`; `Socks5GssApiNegotiation` runs RFC 1961's two messages (context token, protection level) and answers `01 FF` on any failure.
- `SocksUpstreamTestCaseServer` and `SocksUpstreamTestCaseReplies` (BL-562) - the test seam `Surl.Conformance.UnitTests` composes for upstream test cases whose `<server>` names `socks4`, `socks5` or `socks5unix`: `SocksProtocolServer` over a connector that reaches only `127.0.0.1` (at the request's port, or the case's `backendport`) or fails as the case's `response` says, and, for `method 2`, accounts that accept the case's `user`/`password`. Any other `<servercmd>` word is `servercmd-unsupported:<word>`. Never compose it into the shipped `surl`.
- `SocksClientReader` - byte reads that keep any bytes curl sent after the request for the relay.
  `SocksRelay` - the two-way byte relay with half-closes.

Passwords are judged only by the policy, which refuses a clear password without
`--allow-plaintext-auth` and delays a wrong one; the log never holds an RFC 1929 password, and its `PLEN` byte is written as 4 before `****` (an empty password too), so it never shows the password's length either (ADR-0182 decision 1).

The exchange log's redactor (`SocksExchangeBytesRedactor`, ADR-0182 amendments 6 and 12) starts as `SocksHandshakeBytesRedactor`, which follows the client's handshake by its own length fields (it needs no server reply, only whether the listener has a keytab), and after the request's last byte, or a success reply if that comes first, becomes `SocksTunnelBytesRedactor`, which is shown the server's writes only after the success reply: a tunnel whose first byte is an ASCII letter or a digit from `1` to `9` (`SocksTextTunnelBytesRedactor`) is redacted line by line (`Surl.LineProtocol`'s `TunnelLinesRedactor`, which also ends a line at a bare line feed, over its `TunnelLineRedactionRule`, the rules the HTTP proxy shares (amendment 20); amendment 9: `PASS`/`ACCT` arguments and `Authorization`/`Proxy-Authorization` credentials, the whole value of each `HttpWithheldHeaderNames.ApiKeysAndSessionTokens` header and of its folded lines (`TunnelPasswordAndCredentialHeaderLineRule`; amendment 19), the IMAP `LOGIN` password (`TunnelImapLoginLineRule`, which reads words separated by spaces and tabs, and by spaces only), and SASL `AUTH`/`AUTHENTICATE` initial responses and the lines answering `334` or `+`) until the client sends the HTTP/2 preface (`SocksHttp2PrefaceFinder`; amendment 7). After a preface that opened the tunnel or followed an origin's `HTTP/1.1 101`, `Surl.Http2`'s `Http2RequestBytesRedactor` reads the frames, withholding the header block that carries a field of `HttpWithheldHeaderNames.All` (amendment 19); after any other preface every later byte is withheld (`SocksWithheldBytesRedactor`). A tunnel whose first byte is `0x10` or `0x30` goes to the redactor `Surl.LineProtocol`'s `TunnelBinaryLoginRedactors` chooses, which the HTTP proxy shares (amendment 23). `0x10` is an MQTT `CONNECT` (`TunnelMqttConnectRedactor`; amendment 10): the 3.1.1 or 5 password is logged as `00 04 ****` and the Remaining Length rewritten to match; MQTT 5 Authentication Data is not masked. `0x30` is LDAP (`TunnelLdapBindRedactor`; amendment 10): every bind's authentication, a simple password or the whole SASL credentials field, is logged as its tag, `04`, `****` (an empty one as received). Neither holds more than 64 KiB. Any other tunnel is logged byte for byte, so a login inside it (TLS's is ciphertext) is not masked. The event stream and `SocksUpstreamTestCaseServer` keep raw bytes.

`Protocols/Proxy/Surl.Protocol.Socks.UnitTests/Fixtures` holds exchanges recorded from pinned
upstream curl 8.21.0 with the recorder playing the proxy; its README says how each was recorded.

This library references `Surl.Protocol.Abstractions.UnitLibrary` (ADR-0121 decision 1),
`Surl.Kerberos.UnitLibrary` (ADR-0121 amendment 2), `Surl.Http2.UnitLibrary` (ADR-0182
amendment 7) and `Surl.LineProtocol.UnitLibrary` (ADR-0182 amendments 9, 20 and 23), each allowed by ADR-0002 decision 3. Referencing another protocol server is a
build break, and `Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener` here: curl's
connection comes from the listener seam and every origin connection from `IOriginConnector`, so the
tests drive the server through `InMemoryConnection` and `InMemoryOriginConnector`.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).

The fault point at the command stage (`SocksCommandFaults`) names the logged-in account (BL-1240): the RFC 1929 user name the policy accepted, or the Kerberos client principal of a GSS-API login, so a rule scoped with `account=` fires on `CONNECT`. An anonymous SOCKS5 session and SOCKS4 (its `USERID` is not an account) leave `Account` null.
