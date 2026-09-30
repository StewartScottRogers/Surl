# Surl.Protocol.Pop3.UnitLibrary

Phase 3. The POP3 server (RFC 1939) that serves each owner's `INBOX` to upstream curl as a
maildrop, built as
[ADR-0056](../Documentation/Planning/Decisions/ADR-0056-how-the-pop3-server-answers-upstream-curl.md)
decides, on the mail store, the maildrop lock and the line machinery of
[ADR-0050](../Documentation/Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md),
with the logins of
[ADR-0049](../Documentation/Planning/Decisions/ADR-0049-the-mail-servers-sasl-and-apop-logins.md).
Terms (mail store, owner, anonymous owner, UID and UIDVALIDITY, maildrop, maildrop lock, view,
CRLF line, reply line, dot-stuffing, STARTTLS discard, SASL mechanism, SASL exchange, APOP
timestamp) are the glossary's, sections "Mail: SMTP, IMAP and POP3" and "Authentication".

**URL schemes answered:** `pop3`, and `pop3s` through `Surl.Console`'s
`ImplicitTlsSchemeServer`: the server claims `pop3` only and tells implicit TLS apart by
`IConnection.TlsSession`, never by the scheme.

## What it holds

- `Pop3ProtocolServer`: `IConnectionProtocolServer` and `IConnectionRefusalWriter`
  (`-ERR surl Too many connections, closing`). Its constructor takes the
  `IAuthenticationPolicy`, the `IMailAuthenticationPolicy` (`Surl.Console` passes one
  `AuthenticationPolicy` as both), the `MailboxStore` the SMTP server delivers into,
  `isStlsAvailable`, which `Surl.Console` sets when `--cert` or `--self-signed` is given, and an
  optional `RandomNumberGenerator` for the `APOP` timestamp.
- `Pop3Session`: one connection, in the AUTHORIZATION state until a login and the TRANSACTION
  state after it. It greets `+OK surl ready`, followed by an `APOP` timestamp only when the
  offer's `IsApopOffered` holds as the connection opens (`--auth apop`), then answers each CRLF
  line read with `CrlfLineReader`, split by `Pop3CommandLine`, from the fixed lines in
  `Pop3Replies`, written with `ReplyLineWriter` (decisions 1 to 4): `CAPA`, `USER`, `PASS`,
  `APOP`, `AUTH`, `STLS`, `STAT`, `LIST`, `UIDL`, `RETR`, `TOP`, `DELE`, `RSET`, `NOOP` and
  `QUIT`.
- Logins (decisions 6 and 7): `USER`/`PASS` through `IAuthenticationPolicy.CheckPasswordLoginAsync`,
  `USER` offered in `CAPA` only when the mail policy offers a clear password; `APOP` through
  `IMailAuthenticationPolicy.CheckApopLoginAsync` with the greeting's timestamp; `AUTH` frames
  `StartSaslExchange` (`+ ` continuations, base64, `=`, `*`), and bare `AUTH` lists the offered
  mechanisms. An accepted login takes the owner's maildrop lock (`MailboxStore.LockMaildrop`)
  before its `+OK`; a locked maildrop is `-ERR [IN-USE]`. A maildrop command before any login
  asks the policy once about the login with no credentials, and only `--allow-anonymous` lets
  it on (`-ERR [AUTH] Authentication required` otherwise).
- The maildrop (decision 5): `Pop3Maildrop` holds the view fixed at the login and the `DELE`
  marks; `RETR` and `TOP` write through `DotStuffedBodyWriter`, `Pop3TopSection` cutting what
  `TOP` sends; `UIDL` gives `<uidvalidity>.<uid>`. Only `QUIT` after a login removes the marked
  messages, and the lock is released however the session ends.
- `STLS` (decision 8): `+OK Begin TLS negotiation`, then `CrlfLineReader.DiscardBuffered`, then
  `IConnection.UpgradeToTlsAsync`, and the session starts over with the `USER` forgotten;
  `-ERR STLS not available` without a certificate, `-ERR Already using TLS` on a TLS
  connection.
- Limits (decision 9): `-ERR Command line too long, closing` past `--max-line` and
  `-ERR Timeout waiting for a command, closing` past `--head-timeout`, each written within
  `LimitReplyWriteDeadline`, then a close; the idle timeout and maximum duration close the
  connection with no reply.

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
`Record-CurlExchange.ps1 -Pop3`, and never from the Curl port (ADR-0003). Pinned upstream
curl's proof against a live `surl` lives in `Surl.Conformance.UnitTests`
(`UpstreamCurlReadsMailFromSurlOverPop3Tests`, the rows of ADR-0056 decision 12).
