# Surl.Protocol.Imap.UnitLibrary

Phase 3. The IMAP4rev1 server (RFC 3501; not IMAP4rev2, ADR-0055 decision 1) that serves the
mail store to upstream curl, built as
[ADR-0055](../../../Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md)
decides - its decisions 16 to 18 record the details BL-202, BL-203 and BL-204 settled - on the
mail store and line machinery of
[ADR-0050](../../../Documentation/Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md),
with the logins of
[ADR-0049](../../../Documentation/Planning/Decisions/ADR-0049-the-mail-servers-sasl-and-apop-logins.md)
and a limit told from shutdown as
[ADR-0059](../../../Documentation/Planning/Decisions/ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md)
decides. Terms (mail store, owner, anonymous owner, mailbox, UID and UIDVALIDITY, system flags,
view, selected mailbox, literal, CRLF line, reply line, STARTTLS discard, SASL mechanism, SASL
exchange) are the glossary's, sections "Mail: SMTP, IMAP and POP3" and "Authentication".

**URL schemes answered:** `imap`, and `imaps`, which `Surl.Composition` registers as an
`ImplicitTlsSchemeServer` wrapping the `imap` server: this server claims `imap` only and tells
implicit TLS apart by `IConnection.TlsSession`, never by the scheme.

## What it holds

- `ImapProtocolServer`: `IConnectionProtocolServer`, `IConnectionRefusalWriter`
  (`* BYE surl Too many connections, closing`) and `IExchangeBytesRedactingServer` (log redaction,
  below). Its constructor takes the
  `IAuthenticationPolicy`, the `IMailAuthenticationPolicy` (`Surl.Composition` passes the `imap`
  scheme's one `AuthenticationPolicy` as both), the `MailboxStore` the SMTP server delivers into,
  and `isTlsUpgradeAvailable`, which `Surl.Composition` sets from
  `ServerTlsComposition.IsTlsUpgradeAvailable`: true with a certificate (`--cert` or
  `--self-signed`) or under TLS-SRP (ADR-0122 decision 5).
- `ImapSession` (with `ImapSession.Authentication.cs` and `ImapSession.Changes.cs`): one
  connection, its state and its responses from `ImapResponses`. `ImapCommandReader` reads each
  command, its synchronizing literals checked before the `+` continuation and the whole bounded
  by `--max-line` (decision 9); `ImapCapabilities` writes the capability list for the
  connection's state (decision 2). It answers `CAPABILITY`, `NOOP`, `LOGOUT`, `ID`, `LOGIN`,
  `AUTHENTICATE`, `STARTTLS`, `NAMESPACE`, `SELECT`, `EXAMINE`, `LIST`, `LSUB`, `STATUS`,
  `CHECK`, `CLOSE`, `UNSELECT`, `FETCH`, `SEARCH`, `APPEND`, `CREATE`, `DELETE`, `RENAME`,
  `SUBSCRIBE`, `UNSUBSCRIBE`, `STORE`, `COPY`, `MOVE`, `EXPUNGE` and the `UID` forms of
  `FETCH`, `SEARCH`, `STORE`, `COPY`, `MOVE` and `EXPUNGE`. While the store holds a folder use
  (`MailboxStore.HoldsAnyFolderUse`), `SPECIAL-USE CREATE-SPECIAL-USE` are offered after `MOVE`,
  before and after a login (ADR-0055, Amendment 1). `IDLE` and the other extensions decision 2
  says are not advertised, `SPECIAL-USE` apart, are not offered.
- Log redaction (ADR-0183 decision 5, settled by its amendment 1): `CreateRedactor` wraps one
  `ImapLineRedactionRule` per exchange in a `LineBytesRedactor`, and the exchange log shows `****`
  for what the rule withholds: the password of `LOGIN` (atom, quoted string or literal, a literal
  masked once for each line it spans) and, through a `SaslLineRedactionRule`, an `AUTHENTICATE`
  initial response and each client line that answers a `+ ` continuation (a lone `*` and an empty
  line are logged as sent). Other literals, an `APPEND` message included, are logged unchanged, and
  the `+` that invites a literal does not count as a SASL continuation; after a synchronizing
  literal marker, a first server line that does not start with `+` cancels the literal. A run of
  more than 64 KiB with no CR LF is `****` whatever it holds, because `LineBytesRedactor` holds no
  more.
- Redaction of lines the server may refuse (ADR-0183 amendments 3 to 5 and 7, ADR-0182 amendment
  33): a line is a `LOGIN` when its first or second word is `LOGIN` (any case, words split by spaces
  or tabs), and when both are, both arguments after the second are withheld. When the first word is
  `AUTHENTICATE` and the second `AUTH` or `AUTHENTICATE`, the third and fourth words are withheld.
  Nothing follows the mask on a `LOGIN` password's line with a tab among its arguments (a literal
  that line announces is withheld too), or on an initial response's line with a tab, a quote or a
  bare carriage return.
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
- Shared folders and quotas (BL-677, ADR-0102 decisions 1.4 and 1.6): a member's `LIST` shows
  the store's `Shared/<name>` folders under a `\Noselect \HasChildren` `Shared`, and its
  `NAMESPACE` names `(("Shared/" "/"))` as the shared namespace (a non-member's stays `NIL NIL`).
  `APPEND` asks `MailboxStore.CheckQuotaForAppend` from the literal's `{n}` after
  `--max-filesize` and before `+`; that, and `QuotaExceeded` from `APPEND`, `COPY` or `MOVE`, is
  `NO [OVERQUOTA] Mailbox is over its quota`. `SharedFolderProtected` is `NO [NOPERM] A shared
  folder cannot be deleted` (`renamed`, `created under` for `RENAME` and `CREATE`).
- Upstream test cases (BL-878, ADR-0080 decision 2): `ImapUpstreamTestCaseServer` is this server
  composed with a `MailUpstreamTestCaseReplies` (`ImapProtocolServer.UpstreamTestCaseReplies`,
  internal) and an empty anonymous store. `ImapSession.UpstreamTestCase.cs` holds what the case
  changes: the `REPLY welcome` greeting, `REPLY`'d lines answered and not run, `CAPABILITY` from
  `CAPA` and `AUTH`, `FETCH`/`LIST`/`LSUB`/`EXAMINE`/`STATUS`/`SEARCH` from the case's data with
  `POSTFETCH`, and `APPEND` recorded in `UploadedBytes`. With no case the session is unchanged.
  The shipped `surl` never composes it; `Surl.Conformance.UnitTests` does (BL-879).
- Command-stage faults (ADR-0124 decision 4, BL-796, BL-901; `ImapSession.Faults.cs`): every recognized
  command its state allows - after the state's login check, so a fault never skips one - asks
  `ExchangeContext.Faults` at `FaultStage.Command` with its verb (a `UID` command's own verb,
  `FETCH` for `UID FETCH`), the account `LOGIN` or `AUTHENTICATE` logged in, the listen URL as
  `ReportedListenUrl` writes it, and as the path the mailbox the command names (`SELECT`,
  `EXAMINE`, `STATUS`, `CREATE`, `DELETE`, `RENAME`, `SUBSCRIBE`, `UNSUBSCRIBE`, `APPEND`) or
  else the selected one. An `Error` rule is answered with the tagged rule reply (`NO ...` or
  `BAD ...`), `APPEND`'s in place of `+`; the store is untouched, a note is logged and one
  `FaultAppliedEvent` (target: the mailbox, else the verb) is published. A rule with no reply
  answers `NO [UNAVAILABLE] Local error in processing`. A `Delay` rule (`stage=command`) holds the answer for its `ms` on `ExchangeContext.TimeProvider` with `ExchangeContext.IdleClock` paused, and a `Drop` rule aborts the connection with no answer and ends the session, each through `CommandStageFaults.FindAndApplyWithClientOutcomeAsync` with one `FaultAppliedEvent` (ADR-0141, BL-901). A client that closes or resets during a `Delay` hold gets no answer and the session ends (ADR-0141 amendment 3, BL-1545).
- `STARTTLS` (decision 11): `OK Begin TLS negotiation now`, then
  `CrlfLineReader.DiscardBuffered`, then `IConnection.UpgradeToTlsAsync`; `BAD STARTTLS not
  available` only when no upgrade is available (neither a certificate nor TLS-SRP), `BAD Already
  using TLS` on a TLS connection.
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
