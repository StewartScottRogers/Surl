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
`--allow-plaintext-auth` and delays a wrong one; the log never holds a password.

`Protocols/Proxy/Surl.Protocol.Socks.UnitTests/Fixtures` holds exchanges recorded from pinned
upstream curl 8.21.0 with the recorder playing the proxy; its README says how each was recorded.

This library references `Surl.Protocol.Abstractions.UnitLibrary` only, as ADR-0121 decision 1's
row allows. Referencing another protocol server is a build break, and
`Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener` here: curl's
connection comes from the listener seam and every origin connection from `IOriginConnector`, so the
tests drive the server through `InMemoryConnection` and `InMemoryOriginConnector`.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
