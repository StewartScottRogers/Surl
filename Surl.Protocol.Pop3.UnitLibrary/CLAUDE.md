# Surl.Protocol.Pop3.UnitLibrary

Phase 3.

The POP3 server (RFC 1939): `USER` and `PASS`, `APOP`, SASL, `LIST`, `RETR`, `DELE` and
`STLS`, with dot-stuffing, as upstream curl uses them.

**URL schemes answered:** `pop3`, `pop3s`

Built (BL-205, ADR-0056): `Pop3ProtocolServer` (`IConnectionProtocolServer` and
`IConnectionRefusalWriter`, scheme `pop3`) takes an `IAuthenticationPolicy`, an
`IMailAuthenticationPolicy`, the shared `MailboxStore`, `isStlsAvailable` and an optional
`RandomNumberGenerator`. `Pop3Session` greets `+OK surl ready`
and answers `CAPA`, `USER`, `PASS`, `STAT`, `LIST`, `UIDL`, `RETR`, `TOP`, `DELE`, `RSET`, `NOOP`
and `QUIT` from the fixed lines in `Pop3Replies`, reading with `CrlfLineReader` and writing with
`ReplyLineWriter` and `DotStuffedBodyWriter` (`Surl.LineProtocol`). `CAPA` offers `USER` before a
login only when the mail policy offers a clear password; `PASS` goes to `CheckPasswordLoginAsync`,
and an accepted login takes the owner's maildrop lock before its `+OK` (`-ERR [IN-USE]` when
another session holds it). A maildrop command before a login needs the policy's
`AcceptedUnchecked` for the login with no credentials (`--allow-anonymous`), else
`-ERR [AUTH] Authentication required`. `Pop3Maildrop` holds the view fixed at the login and the
`DELE` marks; only `QUIT` after a login removes the marked messages, and the lock is released
however the session ends. `Pop3TopSection` cuts what `TOP` sends.

Built (BL-206, ADR-0056 decisions 2, 3, 4 and 8): the constructor's `isStlsAvailable` (set by
`Surl.Console` when a certificate is configured) makes `CAPA` advertise `STLS` on a plaintext
connection and `STLS` answer `+OK Begin TLS negotiation`, throw away what was pipelined after it
(`CrlfLineReader.DiscardBuffered`) and upgrade; the session starts over with the `USER` forgotten.
Without it `STLS` is `-ERR STLS not available`; over TLS `-ERR Already using TLS`. `CAPA` adds
`SASL <mechanisms>` from `GetMailLoginOffer`. When the offer has `IsApopOffered` as the connection
opens, the greeting carries a timestamp `<16 hex digits.unix seconds@surl>` from the injected
`RandomNumberGenerator` and the context's `TimeProvider`, and `APOP` goes to
`CheckApopLoginAsync` with it. `AUTH <mechanism> [<initial response>]` frames the policy's
`ISaslExchange` (`+ ` continuations, base64, `=`, `*`); bare `AUTH` lists the offered mechanisms.
`pop3s` is implicit TLS told apart by `connection.TlsSession`; the server claims only `pop3`.

Intent, not yet built: the `pop3s` registration through `ImplicitTlsSchemeServer` and the
composition in `Surl.Console` (BL-209).

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
