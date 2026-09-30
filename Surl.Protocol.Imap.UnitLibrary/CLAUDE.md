# Surl.Protocol.Imap.UnitLibrary

Phase 3.

The IMAP server (RFC 9051, and RFC 3501 for IMAP4rev1): mailboxes, `SELECT`, `FETCH`,
`SEARCH`, `APPEND`, `STARTTLS` and SASL authentication, as upstream curl uses them.

**URL schemes answered:** `imap`, `imaps`

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference
the horizontal libraries in ADR-0002 decision 3's table, as later ADRs amend it - nothing
else. Referencing another protocol server is a
build break, and `Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
