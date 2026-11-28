# Surl.Protocol.Smtp.UnitLibrary

Phase 3. The SMTP server (RFC 5321) that receives the mail upstream curl sends, built as
[ADR-0053](../../../Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md)
decides, on the mail store and line machinery of
[ADR-0050](../../../Documentation/Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md),
with the logins of
[ADR-0049](../../../Documentation/Planning/Decisions/ADR-0049-the-mail-servers-sasl-and-apop-logins.md)
and a limit told from shutdown as
[ADR-0059](../../../Documentation/Planning/Decisions/ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md)
decides. Terms (mail store, owner, anonymous owner, discarded recipient, trace fields, CRLF
line, reply line, dot-stuffing, STARTTLS discard, SASL mechanism, SASL exchange) are the
glossary's, sections "Mail: SMTP, IMAP and POP3" and "Authentication".

**URL schemes answered:** `smtp`, and `smtps`, which `Surl.Composition` registers as an
`ImplicitTlsSchemeServer` over the `smtp` server: the server claims `smtp` only and tells
implicit TLS apart by `IConnection.TlsSession`, never by the scheme.

## What it holds

- `SmtpProtocolServer`: `IConnectionProtocolServer`, `IConnectionRefusalWriter`
  (`421 4.3.2 surl Too many connections, closing`) and `IExchangeBytesRedactingServer`, whose
  `CreateRedactor` returns a `LineBytesRedactor` over `SmtpLineRedactionRule`. Its
  constructor takes the `IAuthenticationPolicy`, the `IMailAuthenticationPolicy`
  (`Surl.Composition` passes the `smtp` scheme's one `AuthenticationPolicy` as both), the
  shared `MailboxStore`, and `isTlsUpgradeAvailable`, which `Surl.Composition` sets with a
  certificate (`--cert` or `--self-signed`) or under TLS-SRP (ADR-0122 decision 5;
  `ServerTlsComposition.IsTlsUpgradeAvailable`).
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
  with `MailboxStore.LookUpRecipient`, one that names no account discarded at delivery. With a
  `--mail-domain` configured (ADR-0102 decision 1), a recipient outside every domain
  (`RelayingDenied`) is answered `550 5.7.1 Relaying denied`, noted, and not counted; a named
  owner reached by several `RCPT`s, aliases or catch-alls gets one copy, also when two aliases
  each name an owner the other does not and share one (`MailRecipient.WithoutOwners` keeps the
  later alias's unreached owners only; BL-767). The anonymous owner gets one copy for each
  recipient: `SmtpMailTransaction.AddRecipient` adds it once for each accepted `RCPT`, a
  repeated address included.
  `DATA` streams into a `PendingMessage` from `MailboxStore.CreatePendingMessage`: first
  `SmtpTraceFields`, then the body as `CrlfLineReader.ReadDotStuffedBodyAsync` unstuffs it, so
  with a data directory the message goes straight into the store's pending file. A message past
  `--max-filesize` (trace fields and body together) or a peer that closes mid-body disposes it,
  deleting the pending file; otherwise `MailboxStore.DeliverAsync` stores it
  and `SaveChangesAsync` writes the index. `StoreFull` is answered `452 4.3.1`, `QuotaExceeded` (checked only after `DATA`, never at `RCPT`;
  ADR-0102 decision 1.4) `452 4.2.2 Mailbox full` with nothing delivered to any recipient,
  `MessageTooLarge` `552 5.3.4`, and `StorageFailed` `451 4.3.0 Local error in processing` with
  `Mail store: <reason>` noted; each stores nothing and the session goes on. An index save
  that throws after a delivered message is noted and still answered `250`.
  `SmtpMailTransaction.DeliverablePerOwner` splits `Deliverable` into one recipient per owner
  (one per recipient for the anonymous owner), each recording only the paths, in `RCPT` order,
  that resolved to that owner (the anonymous owner's, its own path alone); each delivery
  event's `DeliveredFor` is that list, with the option or without. With `--mail-delivered-to`
  (`MailboxStore.DeliveredToEnabled`) and at least one deliverable recipient, each owner has its
  own pending message, with `Delivered-To` lines ahead of `Return-Path` naming only those paths,
  so an alias's several owners share no copy and no owner reads another's address. The longest
  pending message decides `--max-filesize`, and they are delivered together through the
  `DeliverAsync` overload that takes `MailOwnerCopy` values (ADR-0053 Amendment 1). Without the
  option the owners share one pending message.
- `STARTTLS` (decision 5): `220`, then `CrlfLineReader.DiscardBuffered`, then
  `IConnection.UpgradeToTlsAsync`, and the session starts over; `454` only when no upgrade is
  available (`isTlsUpgradeAvailable` false: no certificate and no TLS-SRP), `503` on a TLS
  connection.
- Limits (decision 7, ADR-0059): `500` past `--max-line`, `421` past `--head-timeout`, and
  `552 5.3.4` for a `DATA` body that, with its trace fields, passes `--max-filesize`, each then
  closing, written within `LimitReplyWriteDeadline`. A `MAIL` `SIZE=` over `--max-filesize` is
  also answered `552 5.3.4` (`SmtpMailParameters.FindRefusal`, through `AnswerMailAsync`), but
  that one opens no transaction and the session stays open. An
  exchange cancelled for a limit (`ExchangeContext.IsCancelledForALimit`) is answered
  `421 4.4.2 surl Timeout, closing`, and shutdown ends it with no farewell.
- Command-stage faults (ADR-0124 decision 4, BL-796, BL-901; `SmtpSession.Faults.cs`): every recognized
  command asks `ExchangeContext.Faults` at `FaultStage.Command` with its verb, the account an
  `AUTH` logged in (null otherwise, and again after `STARTTLS`), the listen URL as
  `ReportedListenUrl` writes it, and at `RCPT` the recipient address as sent as the path. An
  `Error` rule's reply is written in place of the command's own (`DATA`'s in place of `354`,
  before the message), nothing is stored, a note is logged and one `FaultAppliedEvent` (target:
  the path, else the verb) is published; a rule with no reply answers
  `451 4.3.0 Local error in processing`. A `Delay` rule (`stage=command`) holds the answer for
  its `ms` on `ExchangeContext.TimeProvider` with `ExchangeContext.IdleClock` paused, and a
  `Drop` rule aborts the connection with no answer and ends the session, each through
  `CommandStageFaults.FindAndApplyWithClientOutcomeAsync` with one `FaultAppliedEvent`
  (ADR-0141, BL-901). A client that leaves during a `Delay` hold is sent nothing and the
  session ends (ADR-0141 amendment 3).
- `SmtpLineRedactionRule` (ADR-0183): wraps `Surl.LineProtocol`'s `SaslLineRedactionRule`, so
  the `AUTH` initial response and each answer to a `334 ` continuation, other than a lone `*`
  or an empty line, are logged as `****`. Message lines from a `354` reply to the ending `.`
  line are logged as they arrived, except that `LineBytesRedactor` writes any line over 64 KiB
  as `****`.
- `SmtpLogText`: renders peer bytes for the verbose notes of decision 8.
- `SmtpUpstreamTestCaseServer` (ADR-0080 decision 2): the SMTP server half of one upstream
  curl test case, never composed by the shipped `surl`. It is a separate class that implements
  only `IConnectionProtocolServer` and wraps a `SmtpProtocolServer`, built with the internal
  constructor that takes a `MailUpstreamTestCaseReplies` (`Surl.LineProtocol`), and with an
  empty `MailboxStore` of its own. `SmtpSession.UpstreamTestCase.cs` holds what the case
  changes: `REPLY welcome` replaces the greeting, a `REPLY`'d line is answered with the case's
  text and not run, `EHLO` lists `surl Hello`, then only the case's `CAPA` and `AUTH`, `MAIL`
  needs no login, a `DATA` body is recorded dot-stuffed in `UploadedBytes` and not delivered,
  `VRFY`, `EXPN` and `HELP` answer the case's data for the `EHLO` or `HELO` name when it has
  any, and every command line is recorded in `ReceivedBytes`. It also adds three refusals,
  after each of which the session goes on: a `RCPT` with no at sign is answered
  `501 Invalid address`; so is a `RCPT` or `VRFY` argument with a byte past ASCII when the
  `CAPA` does not list `SMTPUTF8`; and a `MAIL` `SIZE=` over the `SIZE` the `CAPA` advertises
  is answered `552 Message size too large`. Without a case the session is unchanged.

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
