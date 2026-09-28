# Surl.Protocol.Smtp.UnitLibrary

Phase 3.

The SMTP server (RFC 5321): `EHLO`, `AUTH`, `MAIL FROM`, `RCPT TO`, `DATA`, `VRFY`,
`EXPN` and `STARTTLS`, receiving what upstream curl uploads.

**URL schemes answered:** `smtp`, `smtps`

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
