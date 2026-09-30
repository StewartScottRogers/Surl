# Surl.Protocol.Imap.UnitLibrary

Phase 3.

The IMAP4rev1 server (RFC 3501; not IMAP4rev2, ADR-0055 decision 1), as upstream curl uses it.
ADR-0055 decides every response. `ImapProtocolServer` answers `LOGIN`, `CAPABILITY`, `NOOP`,
`LOGOUT`, `ID`, `NAMESPACE`, `SELECT`, `EXAMINE`, `LIST`, `LSUB`, `STATUS`, `CHECK`, `CLOSE` and
`UNSELECT` from the shared mail store (BL-201), `FETCH`, `UID FETCH`, `SEARCH` and
`UID SEARCH` (BL-202), and `APPEND`, `CREATE`, `DELETE`, `RENAME`, `SUBSCRIBE`, `UNSUBSCRIBE`,
`STORE`, `COPY`, `MOVE`, `EXPUNGE` and their `UID` forms (BL-203, in `ImapSession.Changes.cs`), and
`STARTTLS` and `AUTHENTICATE` (BL-204, in `ImapSession.Authentication.cs`). `Surl.Console`
registers it (BL-208) with the mail store the SMTP server delivers into, sets the constructor's `isStartTlsAvailable` when a
server certificate is configured and wraps it in `ImplicitTlsSchemeServer` for `imaps`: the server
claims `imap` only and tells implicit TLS apart by `IConnection.TlsSession`.

`AUTHENTICATE` frames ADR-0049's SASL exchange - base64, `=`, `*`, the `+ ` continuations read
through `CrlfLineReader.ReadSaslContinuationAsync` - and `IMailAuthenticationPolicy` decides every
step. `STARTTLS` discards what `CrlfLineReader` buffered after its line before the upgrade.
ADR-0055 decision 18 records the details BL-204 settled, among them that curl 8.21.0 remembers
`LOGINDISABLED` across `STARTTLS`.

`APPEND`'s message literal is not read like other literals: `ImapCommandReader` stops at it
(`ImapCommandReadOutcome.AppendMessage`), the session checks the login, the mailbox and
`--max-filesize` before it sends `+`, and `ReadAppendMessageAsync` then streams the bytes into the
store's `PendingMessage`, bounded by the idle timeout rather than `--max-line` (ADR-0055,
decisions 8 and 9). ADR-0055 decision 17 records the details BL-203 settled.

An exchange the engine cancels for its idle timeout or maximum duration
(`ExchangeContext.IsCancelledForALimit`) is answered `* BYE surl Timeout, closing` and closed;
every limit response is written on a one-second deadline linked to `ShutdownToken`, never to the
exchange's token, and shutdown ends with no `BYE` (ADR-0059, BL-247).

The RFC 5322 and MIME structure reader `FETCH` and `SEARCH` need lives here, not in the store
(ADR-0055, decision 4): `ImapBodyPart` reads a message's header, fields and parts,
`ImapSection` names the bytes of a `BODY[<section>]`, `ImapStructureWriter` writes `ENVELOPE`,
`BODY` and `BODYSTRUCTURE`, and `ImapDataWriter` keeps every message byte inside a literal or a
quoted string. ADR-0055 decision 16 records the details BL-202 settled.

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
