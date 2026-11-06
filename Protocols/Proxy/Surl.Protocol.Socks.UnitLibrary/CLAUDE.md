# Surl.Protocol.Socks.UnitLibrary

The SOCKS server: what curl's `--socks4`, `--socks4a` and `-x`/`--proxy socks4://` or
`socks4a://` talk to. Decided in ADR-0121 (decisions 1, 2 and 6). Built by BL-558 for SOCKS4 and
SOCKS4a; SOCKS5, SOCKS5h and RFC 1929 accounts (BL-559), the command line and composition
(BL-560), the Unix domain socket (BL-561) and the upstream cases' server half (BL-562) are still
to come, so nothing composes it yet.

**Listen URL schemes:** `socks4`, `socks4a` - both serve SOCKS4 and SOCKS4a alike.

What exists:

- `SocksProtocolServer` - the `IConnectionProtocolServer`. Takes the `IOriginConnector` every
  origin is reached through; it never decides an address itself (the connector applies the
  allowed-origin rule). Its remarks state every answer.
- `SocksExchange` - one curl connection: the version byte (`04` served, `05` answered `05 FF`,
  anything else closed without a reply), the head-timeout-bounded request read, the refusals
  (`00 5B`), the grant (`00 5A`) and the relay.
- `Socks4RequestReader`, `Socks4Request`, `Socks4ReadOutcome` - the request after its version
  byte, each name bounded at 255 bytes. `SocksClientReader` - byte reads that keep any bytes curl
  sent after the request for the relay. `SocksRelay` - the two-way byte relay with half-closes.

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
