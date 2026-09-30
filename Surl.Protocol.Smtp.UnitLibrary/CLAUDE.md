# Surl.Protocol.Smtp.UnitLibrary

Phase 3.

The SMTP server (RFC 5321): `EHLO`, `AUTH`, `MAIL FROM`, `RCPT TO`, `DATA`, `VRFY`,
`EXPN` and `STARTTLS`, receiving what upstream curl uploads.

**URL schemes answered:** `smtp`, `smtps`

Built (BL-198, ADR-0053): `SmtpProtocolServer` (`IConnectionProtocolServer` and
`IConnectionRefusalWriter`, scheme `smtp`) takes an `IAuthenticationPolicy` and the shared
`MailboxStore`. `SmtpSession` answers `EHLO`, `HELO`, `MAIL`, `RCPT`, `DATA`, `RSET`, `NOOP`,
`VRFY`, `EXPN`, `HELP` and `QUIT` from the fixed lines in `SmtpReplies`, reading with
`CrlfLineReader` and writing with `ReplyLineWriter` (`Surl.LineProtocol`). `MAIL` needs the
policy's `AcceptedUnchecked` for the login with no credentials (`--allow-anonymous`), else
`530`. A delivered message is `SmtpTraceFields` (`Return-Path`, `Received`) then the unstuffed
body, bounded as a whole by `--max-filesize` through `SmtpMessageBodyBuffer`.

Intent, not yet built: `STARTTLS` upgrades and its `EHLO` line (BL-199; until then
`STARTTLS` is `454`, or `503` on TLS), `AUTH` and its `EHLO` line (BL-200; until then
`502`), and the `smtps` registration and composition in `Surl.Console` (BL-207).

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
