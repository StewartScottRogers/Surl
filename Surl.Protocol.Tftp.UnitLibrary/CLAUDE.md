# Surl.Protocol.Tftp.UnitLibrary

Phase 1, alongside HTTP.

The TFTP server (RFC 1350) over UDP: read and write requests with the `blksize`, `tsize`
and `timeout` options (RFC 2347 to RFC 2349) upstream curl sends, serving the content
store. Its transport is a datagram channel, not a connection.

**URL schemes answered:** `tftp`

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
