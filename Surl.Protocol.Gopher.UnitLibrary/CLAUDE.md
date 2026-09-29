# Surl.Protocol.Gopher.UnitLibrary

Phase 1, alongside HTTP.

The Gopher server (RFC 1436): serves menus and documents from the content store for the
selector upstream curl sends.

**URL schemes answered:** `gopher` today (`GopherProtocolServer.Schemes`); `gophers`, the
same server over TLS, joins it with the TLS contract (ADR-0010).

`GopherProtocolServer` answers one selector per connection, as ADR-0012 decides: the
selector is read as a percent-encoded path and mapped by `Surl.Content`'s `ContentStore`,
files are sent byte for byte, directories as RFC 1436 menus, and anything the store
refuses or does not have as one fixed error menu. Its fixtures and the commands that
recorded them are in `Surl.Protocol.Gopher.UnitTests/Fixtures/README.md`.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference
the horizontal libraries ADR-0002 lists (`Surl.Content.UnitLibrary`,
`Surl.Cryptography.UnitLibrary`) - nothing else. Referencing another protocol server is a
build break, and `Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
