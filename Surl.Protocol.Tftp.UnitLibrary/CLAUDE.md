# Surl.Protocol.Tftp.UnitLibrary

Phase 1, alongside HTTP.

The TFTP server (RFC 1350) over a datagram flow: read requests with the `blksize`, `tsize`
and `timeout` options (RFC 2347 to RFC 2349) upstream curl sends, served from the content
store. Its transport is a datagram flow, not a connection.

**URL schemes answered:** `tftp` (`TftpProtocolServer.Schemes`)

`TftpProtocolServer` answers the request that opened a flow from a new transfer port, as
ADR-0013 decides: `TftpReadRequest` parses the RRQ, `TftpFileName` maps its file name onto
a content-store request path, `TftpNegotiation` answers its options, and
`TftpReadTransfer` sends the OACK and DATA blocks in lock step, retransmitting on the
exchange's `TimeProvider`. Missing and refused files get ERROR 1; a write request gets
ERROR 2, until BL-054 accepts uploads behind `--allow-uploads`. Its fixtures and the
commands that recorded them are in `Surl.Protocol.Tftp.UnitTests/Fixtures/README.md`.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference
the horizontal libraries ADR-0002 lists (`Surl.Content.UnitLibrary`,
`Surl.Cryptography.UnitLibrary`) - nothing else. Referencing another protocol server is a
build break, and `Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with datagrams measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
