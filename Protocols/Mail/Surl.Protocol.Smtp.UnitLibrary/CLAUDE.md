# Surl.Protocol.Smtp.UnitLibrary

Phase 3. The SMTP server (RFC 5321) that receives the mail upstream curl sends, built as
[ADR-0053](../Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md)
decides, on the mail store and line machinery of
[ADR-0050](../Documentation/Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md),
with the logins of
[ADR-0049](../Documentation/Planning/Decisions/ADR-0049-the-mail-servers-sasl-and-apop-logins.md)
and a limit told from shutdown as
[ADR-0059](../Documentation/Planning/Decisions/ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md)
decides. Terms (mail store, owner, anonymous owner, discarded recipient, trace fields, CRLF
line, reply line, dot-stuffing, STARTTLS discard, SASL mechanism, SASL exchange) are the
glossary's, sections "Mail: SMTP, IMAP and POP3" and "Authentication".

**URL schemes answered:** `smtp`, and `smtps` through `Surl.Console`'s
`ImplicitTlsSchemeServer`: the server claims `smtp` only and tells implicit TLS apart by
`IConnection.TlsSession`, never by the scheme.

## What it holds

- `SmtpProtocolServer`: `IConnectionProtocolServer` and `IConnectionRefusalWriter`
  (`421 4.3.2 surl Too many connections, closing`). Its constructor takes the
  `IAuthenticationPolicy`, the `IMailAuthenticationPolicy` (`Surl.Console` passes one
  `AuthenticationPolicy` as both), the shared `MailboxStore`, and `isTlsUpgradeAvailable`, which
  `Surl.Console` sets when `--cert` or `--self-signed` is given.
- `SmtpSession`: one connection. The greeting, then each CRLF line read with `CrlfLineReader`
  and answered with `ReplyLineWriter` (`Surl.LineProtocol`) from the fixed lines in
  `SmtpReplies`, split by `SmtpCommandLine`: `EHLO` (`SIZE`, `8BITMIME`, `SMTPUTF8`,
  `PIPELINING`, `ENHANCEDSTATUSCODES`, then `STARTTLS` and `AUTH <mechanisms>` when offered),
  `HELO`, `MAIL`, `RCPT`, `DATA`, `RSET`, `NOOP`, `VRFY` and `EXPN` (`252` whatever is asked),
  `HELP`, `QUIT`, `STARTTLS` and `AUTH` (decisions 1 to 5).
- Logins (decision 3): `MAIL` without a login asks `IAuthenticationPolicy` once about the login
  with no credentials, and only `--allow-anonymous`'s `AcceptedUnchecked` lets it on (`530`
  otherwise). `AUTH` - after `EHLO`, once, outside a transaction - splits its argument with
  `SmtpAuthArgument` and runs `IMailAuthenticationPolicy.StartSaslExchange`, each challenge
  sent as `334 <base64>` and each answer read by `CrlfLineReader.ReadSaslContinuationAsync`,
  ending `235`, `535`, `538`, `504` or `501`, the step's `CheckedLogin` note written first.
  `STARTTLS` logs the session out.
- Mail (decisions 4 and 6): `SmtpPathArgument` and `SmtpMailParameters` read `MAIL FROM` and
  `RCPT TO`; `SmtpMailTransaction` keeps up to `MaxRecipients` (100) recipients, each looked up
  with `MailboxStore.LookUpRecipient`, one that names no account discarded at delivery.
  `DATA` streams into a `PendingMessage` from `MailboxStore.CreatePendingMessage`: first
  `SmtpTraceFields`, then the body as `CrlfLineReader.ReadDotStuffedBodyAsync` unstuffs it, so
  with a data directory the message goes straight into the store's pending file. A message past
  `--max-filesize` (trace fields and body together) or a peer that closes mid-body disposes it,
  deleting the pending file; otherwise `MailboxStore.Deliver(..., PendingMessage)` stores it
  and `SaveChangesAsync` writes the index. `StoreFull` is answered `452 4.3.1`,
  `MessageTooLarge` `552 5.3.4`, and `StorageFailed` `451 4.3.0 Local error in processing` with
  `Mail store: <reason>` noted; each stores nothing and the session goes on. An index save
  that throws after a delivered message is noted and still answered `250`.
- `STARTTLS` (decision 5): `220`, then `CrlfLineReader.DiscardBuffered`, then
  `IConnection.UpgradeToTlsAsync`, and the session starts over; `454` without a certificate,
  `503` on a TLS connection.
- Limits (decision 7, ADR-0059): `500` past `--max-line`, `421` past `--head-timeout`, `552`
  past `--max-filesize`, each then closing, written within `LimitReplyWriteDeadline`; an
  exchange cancelled for a limit (`ExchangeContext.IsCancelledForALimit`) is answered
  `421 4.4.2 surl Timeout, closing`, and shutdown ends it with no farewell.
- `SmtpLogText`: renders peer bytes for the verbose notes of decision 8.

## References

`Surl.Protocol.Abstractions.UnitLibrary`, `Surl.LineProtocol.UnitLibrary` and
`Surl.MailStore.UnitLibrary`, the last two horizontal libraries ADR-0050 decision 1 adds to
ADR-0002 decision 3's table.

## Rules

Reference nothing beyond the list above without an ADR that adds the library to ADR-0002
decision 3's table. Referencing another protocol server is a build break, and
`Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1 -Smtp`, and never from the Curl port (ADR-0003). Pinned upstream
curl's proof against a live `surl` lives in `Surl.Conformance.UnitTests`
(`UpstreamCurlSendsMailToSurlOverSmtpTests`, the rows of ADR-0053 decision 10).
