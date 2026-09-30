# Surl.Protocol.Imap.UnitLibrary

Phase 3. The IMAP4rev1 server (RFC 3501; not IMAP4rev2, ADR-0055 decision 1) that serves the
mail store to upstream curl, built as
[ADR-0055](../Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md)
decides - its decisions 16 to 18 record the details BL-202, BL-203 and BL-204 settled - on the
mail store and line machinery of
[ADR-0050](../Documentation/Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md),
with the logins of
[ADR-0049](../Documentation/Planning/Decisions/ADR-0049-the-mail-servers-sasl-and-apop-logins.md)
and a limit told from shutdown as
[ADR-0059](../Documentation/Planning/Decisions/ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md)
decides. Terms (mail store, owner, anonymous owner, mailbox, UID and UIDVALIDITY, system flags,
view, selected mailbox, literal, CRLF line, reply line, STARTTLS discard, SASL mechanism, SASL
exchange) are the glossary's, sections "Mail: SMTP, IMAP and POP3" and "Authentication".

**URL schemes answered:** `imap`, and `imaps` through `Surl.Console`'s
`ImplicitTlsSchemeServer`: the server claims `imap` only and tells implicit TLS apart by
`IConnection.TlsSession`, never by the scheme.

## What it holds

- `ImapProtocolServer`: `IConnectionProtocolServer` and `IConnectionRefusalWriter`
  (`* BYE surl Too many connections, closing`). Its constructor takes the
  `IAuthenticationPolicy`, the `IMailAuthenticationPolicy` (`Surl.Console` passes one
  `AuthenticationPolicy` as both), the `MailboxStore` the SMTP server delivers into, and
  `isStartTlsAvailable`, which `Surl.Console` sets when `--cert` or `--self-signed` is given.
- `ImapSession` (with `ImapSession.Authentication.cs` and `ImapSession.Changes.cs`): one
  connection, its state and its responses from `ImapResponses`. `ImapCommandReader` reads each
  command, its synchronizing literals checked before the `+` continuation and the whole bounded
  by `--max-line` (decision 9); `ImapCapabilities` writes the capability list for the
  connection's state (decision 2). It answers `CAPABILITY`, `NOOP`, `LOGOUT`, `ID`, `LOGIN`,
  `AUTHENTICATE`, `STARTTLS`, `NAMESPACE`, `SELECT`, `EXAMINE`, `LIST`, `LSUB`, `STATUS`,
  `CHECK`, `CLOSE`, `UNSELECT`, `FETCH`, `SEARCH`, `APPEND`, `CREATE`, `DELETE`, `RENAME`,
  `SUBSCRIBE`, `UNSUBSCRIBE`, `STORE`, `COPY`, `MOVE`, `EXPUNGE` and the `UID` forms of
  `FETCH`, `SEARCH`, `STORE`, `COPY`, `MOVE` and `EXPUNGE`. `IDLE` and the other extensions of
  decision 2's list are not offered.
- Logins (decision 10): `LOGIN` through `IAuthenticationPolicy.CheckPasswordLoginAsync`;
  `AUTHENTICATE` frames `IMailAuthenticationPolicy.StartSaslExchange` (base64, `=`, `*`, `+ `
  continuations read by `CrlfLineReader.ReadSaslContinuationAsync`). A command that reads or
  changes a mailbox before any login asks the policy once about the login with no credentials,
  and only `--allow-anonymous` lets it on. A login opens the owner's `MailView`.
- Mailboxes (decisions 5 and 6): `ImapSelectedMailbox` keeps the session's numbering of the
  selected mailbox and reports other sessions' expunges and additions; `ImapMailboxName` reads
  and writes names in modified UTF-7 with `/` as the hierarchy delimiter; `ImapMailboxList`
  answers `LIST` and `LSUB`.
- Fetch and search (decisions 4 and 7): `ImapFetchRequest`, `ImapSequenceSet`, `ImapSection`
  and `ImapFetchResponse`; the RFC 5322 and MIME reader lives here, not in the store:
  `ImapBodyPart` reads a message's header, fields and parts, `ImapStructureWriter` writes
  `ENVELOPE`, `BODY` and `BODYSTRUCTURE`, and `ImapDataWriter` keeps every message byte inside a
  literal or a quoted string. `ImapSearchParser` reads every RFC 3501 search key.
- Changes (decisions 7 and 8): `APPEND` stops `ImapCommandReader` at its message literal
  (`ImapCommandReadOutcome.AppendMessage`), checks the login, the mailbox and `--max-filesize`
  before it sends `+`, and streams the bytes into the store's `PendingMessage`, bounded by the
  idle timeout rather than `--max-line`; `STORE`, `COPY`, `MOVE`, `EXPUNGE`, `CREATE`, `DELETE`
  and `RENAME` change the store in one step each.
- `STARTTLS` (decision 11): `OK Begin TLS negotiation now`, then
  `CrlfLineReader.DiscardBuffered`, then `IConnection.UpgradeToTlsAsync`; `BAD STARTTLS not
  available` without a certificate, `BAD Already using TLS` on a TLS connection.
- Limits (decision 12, ADR-0059): an exchange cancelled for its idle timeout or maximum
  duration (`ExchangeContext.IsCancelledForALimit`) is answered `* BYE surl Timeout, closing`
  and closed, within `LimitReplyWriteDeadline` on a deadline linked to `ShutdownToken`, never to
  the exchange's token; shutdown ends it with no `BYE`.

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
`Record-CurlExchange.ps1 -Imap`, and never from the Curl port (ADR-0003). Pinned upstream
curl's proof against a live `surl` lives in `Surl.Conformance.UnitTests`
(`UpstreamCurlReadsMailFromSurlOverImapTests`, the rows of ADR-0055 decision 15).
