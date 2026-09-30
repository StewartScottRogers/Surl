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

Built (BL-199): the constructor's `isStartTlsAvailable` (default `false`) says whether a
certificate is configured. With it, `EHLO` on a plaintext connection lists `STARTTLS` last,
and `STARTTLS` is answered `220`, discards every buffered byte after its line (noted when not
0), calls `IConnection.UpgradeToTlsAsync` and starts the session over: no hello, no login, no
transaction. Without it `STARTTLS` is `454`; on a TLS connection it is `503`. A failed
handshake's `TlsHandshakeException` goes to the engine. `smtps` is the same server on a
connection the engine has already made TLS: the server reads `connection.TlsSession`, never
the scheme.

Built (BL-200): the constructor also takes an `IMailAuthenticationPolicy` (ADR-0049 section 6;
`Surl.Console` will pass the same object as the `IAuthenticationPolicy`). `EHLO` lists
`AUTH <mechanisms>` last, asking `GetMailLoginOffer(connection.TlsSession)` afresh each time and
leaving the line out when it offers none. `AUTH` is allowed after `EHLO`, once, outside a
transaction (`503` otherwise, `501` with no mechanism); `SmtpAuthArgument` splits off and decodes
the initial response (`=` is empty). `StartSaslExchange` decides every step: a challenge is sent
as `334 <base64>`, the answer read with `CrlfLineReader.ReadSaslContinuationAsync`, `*` is `501`
cancelled, bad base64 `501`, and the end is `235`, `535`, `538` or `504`, with the step's
`CheckedLogin` note written first. A logged-in session may send mail without asking the policy,
and its `Received` field says `ESMTPA` (`ESMTPSA` over TLS); `STARTTLS` logs it out.

Intent, not yet built: the `smtps` registration and composition in `Surl.Console` (BL-207).

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
