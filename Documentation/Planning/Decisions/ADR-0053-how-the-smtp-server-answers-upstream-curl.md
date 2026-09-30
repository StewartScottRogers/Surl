# ADR-0053 — How the SMTP server answers upstream curl

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-186 (FR-043).
- **Amends:** nothing. [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) section 5's SMTP
  column, [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)'s
  "Protocol servers not yet built" SMTP row, [ADR-0049](ADR-0049-the-mail-servers-sasl-and-apop-logins.md)
  (the logins) and [ADR-0050](ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md)
  (the store, recipients and the line machinery) are applied as written. No contract in
  `Surl.Protocol.Abstractions` changes.

## Context

`surl smtp://...` and `surl smtps://...` are to be the server upstream curl's `smtp://` and
`smtps://` uploads talk to. ADR-0050 decided the mail store the message lands in, how a `RCPT TO`
address maps to an owner, and the line machinery; ADR-0049 decided the SASL logins and their
failure words. What was left, and what this ADR decides from measurement, is every other reply
`Surl.Protocol.Smtp` sends: the greeting, the `EHLO` capabilities per TLS state, each command's
reply code and text, whether a login is needed before `MAIL`, `VRFY` and `EXPN`, `STARTTLS`,
the trace fields added to a stored message, ADR-0006's limits in SMTP's words, the verbose notes
and the help category, so BL-198, BL-199, BL-200 and BL-207 are built without a question and
BL-210 knows what to prove.

### What upstream curl 8.21.0 does (measured)

- **Build:** the Windows reference build, `C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
  `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0
  (x86_64-w64-mingw32) libcurl/8.21.0 Schannel ...` (`UpstreamCurlBuilds.json`).
- **Tool:** `Record-CurlExchange.ps1 -Smtp` (with `-Tls` for `smtps://`), 2026-09-29, on
  `127.0.0.1:18025`, with `-SmtpReply` overrides where a row names one. BL-186 extended the script
  with `-SmtpMaxMessageBytes` (stop reading a message body part-way, send the `DATADONE` reply,
  default `552 5.3.4 Message exceeds the size limit`, and close under curl); its comment-based help
  describes it.
- `mail.txt` is `From: a@x`, `To: b@y`, `Subject: hi`, an empty line, `hello`, `.dot line`, every
  line ending CRLF (53 bytes). `mail-lf.txt` is the same kind of message with bare LF line ends
  (34 bytes). `M` below is `--mail-from a@x --mail-rcpt b@y -T mail.txt`, and every URL is
  `smtp://127.0.0.1:18025/c` unless said. Every session opens with the recorder's greeting and
  its default `EHLO` reply (a multi-line `250` advertising `AUTH PLAIN LOGIN CRAM-MD5`, `STARTTLS`,
  `SIZE 1000000`, `8BITMIME`, `SMTPUTF8`) unless a row overrides them, and curl ends a completed
  session with `QUIT`; those lines are left out. `>` is curl, `<` the recorder.

| # | curl arguments (after `-sS`) | Overrides | What curl sent, in order (excerpt) | Exit | stderr / stdout |
| --- | --- | --- | --- | --- | --- |
| 1 | `M` | | `EHLO c`, `MAIL FROM:<a@x> SIZE=53`, `RCPT TO:<b@y>`, `DATA`, the body with `.dot line` sent as `..dot line`, `.` | 0 | |
| 2 | `M`, URL `smtp://127.0.0.1:18025/` and with no path at all | | `EHLO mail.txt`: the tool appends the upload's file name to a URL that ends in `/` or has no path, and curl sends the path as the `EHLO` domain | 0 | |
| 3 | `-X HELP`, URL with no path and no upload | | `EHLO Stewart-Rogers-AI-PC`: with no path at all curl sends its own host name | 0 | the `HELP` reply |
| 4 | `M`, URL `.../client.example` | | `EHLO client.example` | 0 | |
| 5 | `M` with a second `--mail-rcpt c@z` | | `RCPT TO:<b@y>`, `RCPT TO:<c@z>`, then `DATA` | 0 | |
| 6 | as 5 | second `RCPT` `550 5.1.1 no` | `RCPT TO:<c@z>`, then `QUIT`: no `DATA` | 55 | `curl: (55) RCPT failed: 550` |
| 7 | as 6 with `--mail-rcpt-allowfails` | as 6 | both `RCPT`s, `DATA`, the body | 0 | |
| 8 | `--mail-rcpt-allowfails M` (one recipient) | `RCPT=550 5.1.1 no` | `RCPT TO:<b@y>`, `QUIT` | 55 | `curl: (55) RCPT failed: 550 (last error)` |
| 9 | `--mail-rcpt-allowfails`, `--mail-rcpt b@y --mail-rcpt 'Bob <c@z>'` | `RCPT=250 2.1.5 Recipient OK`, then `501 5.1.3 Invalid recipient address` | `RCPT TO:<b@y>`, `RCPT TO:<Bob <c@z>` (curl wraps the argument in `<>` as given), `DATA` | 0 | |
| 10 | `--mail-rcpt 'Bob <b@y>'` (no `--mail-rcpt-allowfails`) | `RCPT=501 5.1.3 Invalid recipient address` | `RCPT TO:<Bob <b@y>`, `QUIT` | 55 | `curl: (55) RCPT failed: 501` |
| 11 | `M` | `MAIL=552 5.3.4 Message size exceeds fixed maximum message size` | `MAIL FROM:<a@x> SIZE=53`, `QUIT` | 55 | `curl: (55) MAIL failed: 552` |
| 12 | `M` | `EHLO` advertising `SIZE 10` | `MAIL FROM:<a@x> SIZE=53`, then the whole message: curl does not check `SIZE` itself | 0 | |
| 13 | `M` | `DATADONE=552 5.3.4 too big`, `452 4.3.1 Insufficient system storage` or `451 4.3.0 local error` | the whole body and `.` | 8 | `curl: (8) Weird server reply` |
| 14 | `M`, a 7200016-byte message | `-SmtpMaxMessageBytes 1000` | `MAIL FROM:<a@x> SIZE=7200016`, `DATA`, the first 1024 body bytes; the recorder answered `552 5.3.4 Message exceeds the size limit` and closed while curl was sending | 55 | `curl: (55) Send failure: Connection was reset` |
| 15 | `M` | `-SmtpMaxMessageBytes 10` | the first body line; `552` and close after curl had sent the whole (small) body | 8 | `curl: (8) Weird server reply` |
| 16 | `M` | `MAIL=530 5.7.0 Authentication required` | `MAIL ...`, `QUIT` | 55 | `curl: (55) MAIL failed: 530` |
| 17 | `M` | `MAIL=421 4.4.2 Timeout waiting for a command`; `MAIL=500 5.5.6 Command line too long`; `MAIL=555 5.5.4 Unsupported parameter` | `MAIL ...`, `QUIT` | 55 | `curl: (55) MAIL failed: 421` (`500`, `555`) |
| 18 | `M` | `RCPT=452 4.5.3 Too many recipients` | `RCPT ...`, `QUIT` | 55 | `curl: (55) RCPT failed: 452` |
| 19 | `M` | `DATA=554 5.5.1 No valid recipients` | `DATA`, `QUIT` | 55 | `curl: (55) DATA failed: 554` |
| 20 | `M` | `GREETING=421 4.3.2 Too many connections` | nothing | 8 | `curl: (8) Got unexpected smtp-server response: 421` |
| 21 | `M` | `EHLO=502 5.5.1 no` (also `500 5.5.2 no`) | `EHLO c`, then `HELO c`, then `MAIL FROM:<a@x>` **without** `SIZE=`, and the rest as 1 | 0 | |
| 22 | `M` | `EHLO=550 no`, `HELO=550 no` | `EHLO c`, `HELO c`, then hangs up without `QUIT` | 9 | `curl: (9) Remote access denied: 550` |
| 23 | `--mail-rcpt b@y -X VRFY` (and `--mail-rcpt b@y` with no upload) | | `VRFY b@y`, `QUIT` | 0 | stdout: the `VRFY` reply as sent, CRLF |
| 24 | as 23 | `VRFY=252 2.1.5 Cannot verify the user, but will accept the message` | `VRFY b@y` | 0 | stdout: that line |
| 25 | as 23 | `VRFY=550 no` | `VRFY b@y` | 8 | `curl: (8) Command failed: 550` |
| 26 | `-X VRFY` (no `--mail-rcpt`) | `VRFY=501 5.5.4 Syntax: VRFY <address>` | `VRFY` with no argument | 8 | `curl: (8) Command failed: 501` |
| 27 | `--mail-rcpt list -X EXPN` | `EXPN=252 2.1.5 Cannot expand the list, but will accept the message` | `EXPN list SMTPUTF8`: curl adds `SMTPUTF8` to `EXPN` whenever the server advertises it | 0 | stdout: that line |
| 28 | as 27 | `EXPN=502 5.5.1 no` | `EXPN list SMTPUTF8` | 8 | `curl: (8) Command failed: 502` |
| 29 | `-X HELP`, and no option at all (no upload, no `--mail-rcpt`) | a one-line `214`, and a multi-line `214-Commands:` ... `214 End` | `HELP`: curl with nothing to send sends `HELP` | 0 | stdout: every reply line as sent |
| 30 | `-X NOOP`, `-X RSET` | `250 2.0.0 OK`, `250 2.0.0 Reset` | `NOOP`, `RSET` | 0 | stdout: the reply |
| 31 | `--mail-rcpt b@y -X RSET` | | `RSET b@y`: `-X` takes `--mail-rcpt` as its argument | 0 | stdout: the reply |
| 32 | `--mail-rcpt b@y -T mail.txt` (no `--mail-from`) | | `MAIL FROM:<> SIZE=53` | 0 | |
| 33 | `--mail-from a@x --mail-rcpt postmaster --mail-rcpt bob -T mail.txt` | | `RCPT TO:<postmaster>`, `RCPT TO:<bob>`: curl sends a local part with no domain as given | 0 | |
| 34 | `--mail-from ä@x --mail-rcpt ü@y -T mail.txt` | | `MAIL FROM:<\xE4@x> SIZE=53 SMTPUTF8`, `RCPT TO:<\xFC@y>`: curl adds `SMTPUTF8` to `MAIL` for a non-ASCII address when advertised; the Windows tool passed the arguments in the ANSI code page, not UTF-8 | 0 | |
| 35 | as 34 | `EHLO` without `SMTPUTF8` | `MAIL FROM:<\xE4@x> SIZE=53`: no `SMTPUTF8`, the same bytes | 0 | |
| 36 | `M` | `EHLO` advertising `8BITMIME` only | `MAIL FROM:<a@x>`: curl sends no `BODY=` parameter | 0 | |
| 37 | `-u user:secret --mail-auth a@x M` | `EHLO` advertising `AUTH CRAM-MD5` | `AUTH CRAM-MD5`, then `MAIL FROM:<a@x> AUTH=<a@x> SIZE=53`; without `-u` (no login) no `AUTH=` is sent | 0 | |
| 38 | `-T mail-lf.txt`, bare LF line ends | | the body sent as it is, bare LFs and all, `.dot` **not** stuffed (it follows a bare LF), then CRLF `.` CRLF appended since the body did not end in CRLF: `...hello\n.dot\n\r\n.\r\n` | 0 | |
| 39 | `--crlf -T mail-lf.txt` | | every LF sent as CRLF, `..dot` stuffed | 0 | |
| 40 | `-k --ssl-reqd M` | | `EHLO c`, `STARTTLS` (`220`, TLS 1.2 handshake), `EHLO c` again, `MAIL`, ... | 0 | |
| 41 | `-k --ssl-reqd M` | `STARTTLS=454 4.7.0 TLS not available` | `EHLO c`, `STARTTLS`, then hangs up | 64 | `curl: (64) STARTTLS denied, code 454` |
| 42 | `-k --ssl M` | as 41 | `STARTTLS`, then `MAIL` ... in plaintext | 0 | `Warning: --ssl is an insecure option, consider --ssl-reqd instead` |
| 43 | `-k --ssl-reqd M` | `EHLO` without `STARTTLS` | `EHLO c`, then hangs up | 64 | `curl: (64) STARTTLS not supported.` |
| 44 | `-k M`, URL `smtps://127.0.0.1:18025/c`, `-Tls` | | TLS from the first byte, then as 1 | 0 | |
| 45 | `-u user:secret --login-options AUTH=PLAIN M` | `EHLO` advertising `AUTH CRAM-MD5` | nothing after `EHLO` | 67 | `curl: (67) Login denied` |
| 46 | as 45 | `EHLO` advertising `AUTH CRAM-MD5 PLAIN`, `AUTH=538 5.7.11 Encryption required for requested authentication mechanism` | `AUTH PLAIN` | 67 | `curl: (67) Login denied` |
| 47 | `-u user:secret M` | `EHLO` with no `AUTH` line, `MAIL=530 5.7.0 Authentication required` | no `AUTH`; `MAIL ...` | 55 | `curl: (55) MAIL failed: 530` |

**The decided replies, measured.** Every reply decision 1 pins was then served to curl through
`-SmtpReply` (`GREETING=220 surl ESMTP ready`, the `EHLO` replies of decision 2 for each TLS
state, `HELO=250 surl Hello`, `MAIL=250 2.1.0 Sender OK`, `RCPT=250 2.1.5 Recipient OK`,
`DATADONE=250 2.0.0 Message accepted`, `STARTTLS=220 2.0.0 Ready to start TLS`, `QUIT=221 2.0.0
Bye`, and decision 1's `VRFY`, `EXPN`, `HELP`, `NOOP`, `RSET`, `530`, `501`, `452` and `555`
texts), and curl completed each exchange as rows 1, 5, 9 to 11, 16 to 18, 21, 23 to 30, 40 and 44
say: exit 0 for `-u user:secret M` (logging in with `CRAM-MD5`), for `-k --ssl-reqd -u
user:secret M` (the second `EHLO` answered with the TLS capability list), for `-k -u user:secret
M` over `smtps://`, and for `M` with `EHLO` refused and `HELO` answered.

Nothing curl 8.21.0 sends in these sessions is `BDAT`, `ETRN`, `TURN`, `ATRN`, `SEND`, `SOML`,
`SAML` or `VERB`, nor any `RCPT` parameter; its `MAIL` parameters are `SIZE=` (when advertised),
`AUTH=` (after a login, with `--mail-auth`) and `SMTPUTF8` (when advertised and an address is not
ASCII). curl never pipelines: it waits for each reply.

## Decision

### 1. The greeting and the reply table

Every reply is `<code> <text>\r\n`, or a multi-line `<code>-<text>` ... `<code> <text>` reply
(RFC 5321 section 4.2.1), written through ADR-0050 decision 8's reply-line writer, so no byte a
peer sent can reach a reply unescaped. The text is fixed text from this table (ADR-0006 section
3): no address, domain or argument the peer sent is ever echoed. **Enhanced status codes** (RFC
3463, advertised as `ENHANCEDSTATUSCODES`, RFC 2034) start the text of every reply except the
greeting and the replies to `EHLO` and `HELO`, as RFC 2034 section 4 says; they are sent after
`HELO` too, which RFC 2034 allows and curl ignores. A command is its first word, matched without
regard to case; its argument is the rest of the line after one space.

| Command | Reply |
| --- | --- |
| (greeting) | `220 surl ESMTP ready` - `surl` in place of a host name, no version (ADR-0006 section 3) |
| `EHLO <domain>` | decision 2's multi-line `250`; resets any mail transaction (RFC 5321 section 4.1.4) |
| `HELO <domain>` | `250 surl Hello`; resets any mail transaction |
| `EHLO`, `HELO` with no argument | `501 Syntax: EHLO <domain>` (`HELO`) |
| `STARTTLS` | decision 5 |
| `AUTH <mechanism> [<initial response>]` | ADR-0049 section 7's table; decision 3 for when it is allowed |
| `MAIL FROM:<reverse-path> [<parameters>]` | `250 2.1.0 Sender OK`; decisions 3 and 4 for the refusals |
| `RCPT TO:<forward-path> [<parameters>]` | `250 2.1.5 Recipient OK`; decision 4 |
| `DATA` | `354 End data with <CR><LF>.<CR><LF>`, then decision 6 |
| `RSET` | `250 2.0.0 Reset`: the mail transaction is dropped; the `EHLO` and the login stay |
| `NOOP [<anything>]` | `250 2.0.0 OK` |
| `VRFY <string>` | `252 2.1.5 Cannot verify the user, but will accept the message` |
| `EXPN <string> [SMTPUTF8]` | `252 2.1.5 Cannot expand the list, but will accept the message` |
| `VRFY`, `EXPN` with no argument | `501 5.5.4 Syntax: VRFY <address>` (`EXPN <list>`) |
| `HELP [<anything>]` | `214 2.0.0 Commands: EHLO HELO STARTTLS AUTH MAIL RCPT DATA RSET NOOP VRFY EXPN HELP QUIT` |
| `QUIT` | `221 2.0.0 Bye`, then the connection is closed (`CompleteWritesAsync`) |
| `RSET`, `DATA`, `STARTTLS`, `QUIT` with an argument | `501 5.5.4 Syntax: <COMMAND> takes no argument` |
| `BDAT`, `ETRN`, `TURN`, `ATRN`, `SEND`, `SOML`, `SAML`, `VERB` | `502 5.5.1 Command not implemented` |
| anything else | `500 5.5.2 Command not recognized` |
| a line holding a bare CR or LF, or a byte outside 0x20 to 0x7E before its first space | `500 5.5.2 Command not recognized` |

- **`VRFY` and `EXPN` answer `252` whatever is asked** (RFC 5321 section 3.5.3), before and after
  a login, so no peer can enumerate accounts, as ADR-0050 decision 5 answers every recipient
  alike. `252` is a success to curl (rows 24, 27: exit 0, the line on stdout); `550` or `502`
  would be exit 8 (rows 25, 28). `EXPN`'s trailing `SMTPUTF8` (row 27) is accepted and ignored.
- **`HELP`** is one fixed line, the same whether or not `STARTTLS` and `AUTH` are available now:
  it lists the commands the server understands, not what the session may do.
- **A bare CR or LF** (ADR-0050 decision 8: a command line ends only at CRLF) makes the line
  unrecognisable, answered as above; the session goes on.
- **Sequencing** (RFC 5321 section 4.1.4), each `503` leaving the session as it was:

  | Command | Refused with |
  | --- | --- |
  | `MAIL` before `EHLO` or `HELO` | `503 5.5.1 Send EHLO or HELO first` |
  | `MAIL` inside a transaction | `503 5.5.1 Sender already given` |
  | `RCPT` before `MAIL` | `503 5.5.1 Send MAIL first` |
  | `DATA` with no recipient accepted | `503 5.5.1 Send RCPT first` |
  | `AUTH` before `EHLO` (none, or `HELO` only; RFC 4954 needs `EHLO`) | `503 5.5.1 Send EHLO first` |
  | `AUTH` after a login | `503 5.5.1 Already authenticated` |
  | `AUTH` inside a transaction | `503 5.5.1 AUTH not permitted during a mail transaction` |

### 2. The `EHLO` capabilities, per TLS state

```
250-surl Hello
250-SIZE <max>
250-8BITMIME
250-SMTPUTF8
250-PIPELINING
250-ENHANCEDSTATUSCODES
250-STARTTLS
250 AUTH <mechanisms>
```

- In this order, each line present only as below, the last present one written `250 ` and the
  rest `250-`. The first line names the server `surl` and echoes nothing of the client's domain.
- **`SIZE <max>`** (RFC 1870): `<max>` is `ExchangeLimits.MaxUploadBytes` in decimal
  (`--max-filesize`, default 104857600); `SIZE 0` when it is 0, which RFC 1870 reads as no fixed
  maximum. Always present.
- **`8BITMIME`** (RFC 6152) and **`SMTPUTF8`** (RFC 6531): always present. The message is stored
  as its bytes (ADR-0050 decision 3), so 8-bit bodies and UTF-8 headers need nothing done to
  them; addresses are read as decision 4 says.
- **`PIPELINING`** (RFC 2920): always present. ADR-0050 decision 8's reader keeps pipelined bytes
  buffered and every command is answered in order, so pipelining costs nothing; curl does not
  pipeline. `STARTTLS` discards what is buffered after it (decision 5), and a `DATA` whose `354`
  is sent reads the body from the same buffer.
- **`ENHANCEDSTATUSCODES`** (RFC 2034): always present, since decision 1 sends them.
- **`STARTTLS`** (RFC 3207): only on a plaintext connection whose server can upgrade (decision 5),
  never once the connection is TLS (after `STARTTLS`, and on `smtps://`).
- **`AUTH <mechanisms>`** (RFC 4954): `IMailAuthenticationPolicy.GetMailLoginOffer(connection.TlsSession).SaslMechanisms`
  in the order given, space-separated, asked afresh for every `EHLO` (so after `STARTTLS` the
  offer grows, ADR-0049 section 2); the line is left out when the list is empty. With the default
  `--auth` set that is `AUTH CRAM-MD5` over plaintext and `AUTH CRAM-MD5 OAUTHBEARER XOAUTH2 PLAIN
  LOGIN` over TLS (ADR-0049 section 2), the two measured above.
- `DSN`, `CHUNKING`, `BINARYMIME`, `REQUIRETLS` and `DELIVERBY` are not advertised: nothing
  delivers onward, curl sends none of them, and `BDAT` is `502`.

### 3. Logins, and what a login unlocks

- **`MAIL` needs a login unless `--allow-anonymous` is given** (ADR-0032: secure by default).
  A session that has not logged in with `AUTH` asks the policy once, at its first `MAIL`, with
  `IAuthenticationPolicy.CheckPasswordLoginAsync(new PasswordLogin(scheme, null, null,
  connection.TlsSession))` - the login that carries no credentials, as MQTT's `CONNECT` without a
  user name is - and keeps the verdict for the session:
  - `AcceptedUnchecked` (`--allow-anonymous`): `MAIL` proceeds, with no login note;
  - anything else (`RefusedAnonymous`): `530 5.7.0 Authentication required`, the session goes on
    (curl exits 55, row 16 and 47).
  **Why:** a server that took mail from anyone by default would be an open relay's front door and
  let any peer fill the store; with accounts configured, curl's `-u user:secret` logs in with
  `CRAM-MD5` over plaintext by default (ADR-0049 section 2), so a login costs a curl user one
  option. `--allow-anonymous` is the testing loosening that makes `curl --mail-rcpt b@y -T m
  smtp://...` work with no `-u`, delivering to ADR-0050 decision 2's anonymous owner.
- **`AUTH`** runs ADR-0049's exchange through `IMailAuthenticationPolicy.StartSaslExchange` and
  answers in ADR-0049 section 7's words; `Accepted` and `AcceptedUnchecked` log the session in
  (the account ADR-0049 names, or none), and the login lasts until `STARTTLS` or the end of the
  connection. `AUTH` is allowed only after `EHLO`, once, outside a transaction (decision 1's
  `503`s). With no account configured every `AUTH` is refused (`535`) and every `MAIL` gets `530`
  (ADR-0032 "Protocol servers not yet built", criterion 2).
- **The login decides nothing about recipients or the sender.** Recipients map as ADR-0050
  decision 5 says whoever is logged in; the reverse-path is not checked against the account (there
  is no relaying to protect), and `MAIL`'s `AUTH=` parameter (RFC 4954 section 5, row 37) is
  accepted and ignored.
- `VRFY`, `EXPN`, `HELP`, `NOOP`, `RSET`, `EHLO`, `HELO`, `STARTTLS`, `AUTH` and `QUIT` need no
  login.

### 4. `MAIL` and `RCPT`: paths and parameters

- **A path** is `<` ... `>` directly after `FROM:` or `TO:` (the colon matched without regard to
  case, no space before `<`, as curl sends it; one space after the colon is tolerated, RFC 5321
  section 3.3 notwithstanding, as common servers do). `MAIL FROM:<>` is the null reverse-path and
  is accepted (row 32). Inside the brackets: an optional source route (`@a,@b:`) that is ignored,
  then a local part (a dot-string, or a quoted string whose backslash escapes are removed), then
  optionally `@` and a domain or address literal, which is ignored (ADR-0050 decision 5). **A local
  part with no domain is accepted** (row 33: curl sends `--mail-rcpt bob` as `<bob>`), since the
  domain is ignored anyway. The path's bytes are read as UTF-8; the local part matches an account
  as ADR-0050 decision 5 says.
- **Refused as invalid**: no brackets, an empty `<>` in `RCPT`, a `<` or `>` inside the brackets
  (row 10's `<Bob <b@y>`), an unbalanced quote, an empty local part, a control character, or bytes
  that are not UTF-8. `MAIL`: `501 5.1.7 Invalid sender address`; `RCPT`: `501 5.1.3 Invalid
  recipient address`. This is the only recipient refusal a peer can cause with an address, so it
  is how `--mail-rcpt-allowfails` is exercised (rows 9, 10). Row 34's Latin-1 bytes are not UTF-8
  and so are refused: the Windows tool's ANSI arguments are its own limitation, and a UTF-8
  address (`--mail-rcpt` from a UTF-8 console or a script) is accepted.
- **`MAIL` parameters**, matched without regard to case, each at most once:
  - `SIZE=<n>` (RFC 1870): `<n>` 1 to 20 decimal digits; larger than `MaxUploadBytes` (when not 0):
    `552 5.3.4 Message size exceeds the size limit` (curl exits 55, row 11); malformed:
    `501 5.5.4 Invalid SIZE parameter`;
  - `BODY=7BIT` or `BODY=8BITMIME` (RFC 6152): accepted, changes nothing; another value
    `501 5.5.4 Invalid BODY parameter`;
  - `SMTPUTF8` (RFC 6531): accepted, changes nothing;
  - `AUTH=<mailbox>` or `AUTH=<>` (RFC 4954 section 5): accepted and ignored;
  - anything else, or one given twice: `555 5.5.4 Unsupported parameter` (RFC 5321 section 4.1.1.11).
- **`RCPT` parameters**: none is supported, so any gets `555 5.5.4 Unsupported parameter`.
- **At most 100 recipients per transaction** (RFC 5321 section 4.5.3.1.8's minimum, and ADR-0006:
  a peer-fillable list is bounded): the 101st accepted-or-discarded `RCPT` gets `452 4.5.3 Too
  many recipients` (curl exits 55, row 18), and the transaction keeps the first 100.
- **Unknown recipients** get `250 2.1.5 Recipient OK`, exactly as known ones, and their copies are
  discarded at delivery with ADR-0050 decision 5's note. Two `RCPT`s naming the same account
  deliver one copy to it; under `--allow-anonymous` every `RCPT` is one copy in the anonymous
  `INBOX` (ADR-0050 decision 5).

### 5. `STARTTLS` and `smtps://`

- **Whether the server can upgrade** is given to `SmtpProtocolServer`'s constructor as a
  `bool` (`isStartTlsAvailable`), which `Surl.Console` sets when a server certificate is
  configured (`--cert`, or `--self-signed`, ADR-0032 section 10; ADR-0010 makes the throwaway
  certificate for a scheme that can upgrade). Nothing in `IConnection` says whether an upgrade
  would succeed, and no contract changes for it.
- **`STARTTLS`** on a plaintext connection that can upgrade: `220 2.0.0 Ready to start TLS`, then
  ADR-0050 decision 8's discard (every byte buffered after the `STARTTLS` line is thrown away and
  its count noted, ADR-0010 section 1), then `IConnection.UpgradeToTlsAsync`. The session then
  starts over (RFC 3207 section 4.2): no `EHLO`, no login, no transaction; `MAIL` before a new
  `EHLO` gets `503 5.5.1 Send EHLO or HELO first`. curl sends `EHLO` again (row 40).
- **No certificate**: `454 4.7.0 TLS not available` (RFC 3207 section 4), and `STARTTLS` is never
  advertised (decision 2). curl exits 64 with `--ssl-reqd` (row 41, or row 43 since it is not
  advertised) and goes on in plaintext with `--ssl` (row 42) - ADR-0032 section 10's "refused in
  its own words".
- **Already TLS** (after `STARTTLS`, or `smtps://`): `503 5.5.1 Already using TLS`.
- **A failed handshake** ends the connection with the engine's `TLS handshake failed: ` note
  (ADR-0010); nothing is written after it.
- **`smtps://`** is implicit TLS: the engine completes the handshake before `ServeAsync`, as for
  `https://` (ADR-0020). `SmtpProtocolServer` claims the scheme `smtp` only, and `Surl.Console`
  registers it for `smtps` through `ImplicitTlsSchemeServer`, as it does the HTTP server for
  `https` (BL-207); the server tells the two apart by `connection.TlsSession`, never by the scheme,
  and writes `ExchangeContext.Scheme` into its notes and trace fields. Default ports are curl's:
  25 for `smtp`, 465 for `smtps`.

### 6. `DATA`, the stored message and its trace fields

- After `354`, ADR-0050 decision 8's dot-stuffed body reader reads the body, keeping bare LFs and
  bare CRs (row 38: without `--crlf` curl sends a bare-LF file as it is, and does not stuff a `.`
  after a bare LF, so neither may the server unstuff one).
- **Trace fields** (RFC 5321 section 4.4: a server making final delivery adds `Return-Path`, and
  every server receiving a message adds `Received`). The stored message is these two lines, then
  the body's bytes as unstuffed:

  ```
  Return-Path: <reverse-path>
  Received: from <ehlo-domain> (<address-literal>) by surl with <protocol>; <date-time>
  ```

  - `<reverse-path>` is the `MAIL FROM` path's bytes between the brackets as sent (empty for
    `<>`); `<ehlo-domain>` is the `EHLO` or `HELO` argument's bytes as sent; both passed decision
    1's and 4's checks, so neither holds a CR or LF.
  - `<address-literal>` is the connection's `RemoteEndPoint` address: `[127.0.0.1]`, or
    `[IPv6:::1]` for IPv6 (RFC 5321 section 4.1.3). The server's own address and host name are
    never written; it is `surl`.
  - `<protocol>` is RFC 3848's word: `SMTP` after `HELO`, `ESMTP` after `EHLO`, with `S` added
    when `connection.TlsSession` is not `null` and `A` when the session logged in with `AUTH`
    (`ESMTPS`, `ESMTPA`, `ESMTPSA`).
  - `<date-time>` is RFC 5322's, from `ExchangeContext.TimeProvider.GetUtcNow()` when the `354` is
    sent, in the invariant culture: `ddd, dd MMM yyyy HH:mm:ss +0000`.
  - No `for <recipient>` clause and no message id: each recipient's copy is the same file
    (ADR-0050 decision 7), so the fields cannot name one recipient, and naming all of them would
    tell each recipient who else received the message, which a `Bcc` exists to hide.
- **Delivered**: `250 2.0.0 Message accepted`, once the store accepted the message for every
  owner (ADR-0050 decision 3: all or none); the transaction ends and the session goes on.
- **The size limit** is ADR-0006's `--max-filesize` on the stored message, trace fields included,
  since the store checks that again (ADR-0050 decision 6): the body reader's budget is
  `MaxUploadBytes` minus the trace fields' length (no limit when `MaxUploadBytes` is 0). A message
  whose body fits `SIZE` but not the budget (by at most the trace fields' length, some 150 bytes)
  is refused as below; `MAIL`'s `SIZE=` check (decision 4) compares the client's own figure with
  `MaxUploadBytes`.
- **Past the budget** (`BodyTooLarge`): `552 5.3.4 Message exceeds the size limit`, nothing is
  stored (the pending file is deleted), and the connection is closed, since the end of the body can
  no longer be found without reading past the limit (ADR-0050 decision 8, ADR-0006 section 5).
  Measured, curl exits 55 when it is still sending (row 14) and 8 when it had sent the whole body
  (row 15).
- **The store's outcomes**, the session going on after each: `StoreFull`: `452 4.3.1 Insufficient
  system storage`; `StorageFailed`: `451 4.3.0 Local error in processing`, with the exception message
  in a note, never in the reply. curl exits 8 for both (row 13).
- A peer that closes mid-body stores nothing.

### 7. Limits (ADR-0006 section 5, SMTP column)

| Limit | SMTP answer | curl (measured) |
| --- | --- | --- |
| Too many connections (`IConnectionRefusalWriter`, which the server implements) | `421 4.3.2 surl Too many connections, closing` in place of the greeting, then close | 8, row 20 |
| Head timeout (a command line not finished within `--head-timeout`, ADR-0050 decision 8) | `421 4.4.2 surl Timeout waiting for a command, closing`, then close | 55 when it answers `MAIL`, row 17 |
| Line past `--max-line` (8192 bytes, CRLF included) | `500 5.5.6 Command line too long`, then close | 55 when it answers `MAIL`, row 17 |
| Message past `--max-filesize` | decision 6's `552`, nothing stored, then close | 55 or 8, rows 14 and 15 |
| Idle timeout, maximum duration | `421 4.4.2 surl Timeout, closing`, then close | as the head timeout |
| More than 100 recipients | decision 4's `452 4.5.3` | 55, row 18 |
| The store full | decision 6's `452 4.3.1` | 8, row 13 |

A `421` names the server first, `surl`, as RFC 5321 section 4.2.3's `421 <domain>` form asks. A
SASL continuation line is bounded by the same `--max-line` and head timeout (ADR-0050 decision 8)
and answered the same way.

### 8. The verbose notes (ADR-0033, ADR-0038)

Written with `IExchangeLog.Note` at `verbose` and above (ADR-0033 section 3: a protocol server's
notes are never written at `info`), every peer byte escaped as ADR-0006 section 3 says. The bytes
each way are the engine's `BytesReceived`/`BytesSent` lines, as for every server.

| When | Note |
| --- | --- |
| A message stored | `Message stored: <n> bytes for <k> recipients` (`<k>` the owners it reached) |
| A recipient that names no account | `Mail for <address> discarded: no such account` (ADR-0050 decision 5) |
| Past the size limit | `Message refused: past --max-filesize after <n> bytes` |
| The store full | `Message refused: the mail store is full` |
| A message file that could not be written | `Message refused: could not store it: <exception message>` |
| The index that could not be written, or a file that could not be deleted | `Mail store: <exception message>` (ADR-0050 decision 7) |
| Bytes discarded after `STARTTLS` | `Discarded <n> bytes sent after STARTTLS` (only when `<n>` is not 0) |
| `MAIL` refused for want of a login | `MAIL refused: log in with AUTH first, or give --allow-anonymous` |
| A checked `AUTH` | `CheckedLogin.Note` from the step, before the reply (ADR-0038, ADR-0049 section 6) |

### 9. Help

The `smtp` category, `SMTP and SMTPS protocol` (ADR-0034 decision 1's wording), claims the
schemes `smtp` and `smtps`, and holds every option the SMTP server reads: the limits it applies
(`--max-filesize`, `--max-line`, `--head-timeout`), the login options (`--user`, `--user-file`,
`--allow-anonymous`, `--allow-plaintext-auth`, `--auth`) and `--directory` (where the mail store
persists, ADR-0050 decision 7). BL-207 adds the category, its `--aihelp` topic, prose and
example, and grows the pinned topic lists, as root `CLAUDE.md` requires.

### 10. What BL-210 proves with the pinned build

Each against `surl` over loopback through the conformance harness, with the options named; `-k`
wherever TLS is used with `--self-signed`; `M` is `--mail-from a@x --mail-rcpt tester@example.com
-T mail.txt` and every URL ends `/c` so the `EHLO` domain is fixed. "Stored" means the message,
read back from the temporary `--directory` through ADR-0050's persisted format, is decision 6's
trace fields then `mail.txt`'s bytes (`..dot line` unstuffed).

| surl options | curl command line | Exit |
| --- | --- | --- |
| `--allow-anonymous` | `curl -sS M smtp://127.0.0.1:<p>/c` | 0, stored in the anonymous `INBOX` |
| `--user tester:secret` | `curl -sS M smtp://.../c` (no login) | 55 (`530`) |
| `--user tester:secret` | `curl -sS -u tester:secret M smtp://.../c` | 0, `CRAM-MD5`, stored in `tester`'s `INBOX` |
| `--user tester:secret` | `curl -sS -u tester:wrong M smtp://.../c` | 67 |
| `--allow-anonymous` | `curl -sS --mail-from a@x --mail-rcpt b@y --mail-rcpt c@z -T mail.txt smtp://.../c` | 0, two copies |
| `--user tester:secret` | `curl -sS -u tester:secret --mail-from a@x --mail-rcpt tester@x --mail-rcpt nobody@x -T mail.txt smtp://.../c` | 0, one copy in `tester`'s `INBOX`, the other discarded |
| `--allow-anonymous` | `curl -sS --mail-from a@x --mail-rcpt b@y --mail-rcpt "Bob <c@z>" -T mail.txt smtp://.../c` | 55 (`501`) |
| `--allow-anonymous` | the same with `--mail-rcpt-allowfails` | 0, one copy |
| `--user tester:secret --self-signed` | `curl -sS -k --ssl-reqd -u tester:secret M smtp://.../c` | 0 |
| `--user tester:secret` (no certificate) | `curl -sS --ssl-reqd -u tester:secret M smtp://.../c` | 64 |
| `--user tester:secret --self-signed` | `curl -sS -k -u tester:secret M smtps://.../c` | 0 |
| `--user tester:secret` | `curl -sS -u tester:secret --login-options AUTH=<mech> [--sasl-ir] M smtp://.../c` over plaintext for `CRAM-MD5`, and with `--self-signed -k --ssl-reqd` for `PLAIN`, `LOGIN`; `-u tester: --oauth2-bearer tok` with `--user :tok` for `XOAUTH2`, `OAUTHBEARER`; `--auth digest-md5` for `DIGEST-MD5`, `--auth ntlm` for `NTLM` | 0 each |
| `--user tester:secret` | `curl -sS -u tester:secret --login-options AUTH=PLAIN M smtp://.../c` (plaintext, `PLAIN` not offered) | 67 (row 45) |
| `--user tester:secret --allow-plaintext-auth` | the same | 0 |
| `--allow-anonymous --max-filesize 1000` | `curl -sS --mail-from a@x --mail-rcpt b@y -T big.txt smtp://.../c`, `big.txt` over 1 MiB | 55 (`552`), or 8 where the platform's close lets curl read the reply; nothing stored |
| `--allow-anonymous --max-filesize 10` | `curl -sS M smtp://.../c` | 55 (`552` to `SIZE=53`), nothing stored |
| `--allow-anonymous` | `curl -sS --mail-rcpt b@y -X VRFY smtp://.../c` | 0, stdout `252 2.1.5 Cannot verify the user, but will accept the message` |
| `--allow-anonymous` | `curl -sS --mail-rcpt list -X EXPN smtp://.../c` | 0, stdout the `EXPN` `252` line |
| `--allow-anonymous` | `curl -sS smtp://.../c` (no upload: `HELP`) | 0, stdout decision 1's `214` line |
| `--allow-anonymous` | `curl -sS -X NOOP smtp://.../c` | 0, stdout `250 2.0.0 OK` |
| `--allow-anonymous` | `curl -sS --crlf --mail-from a@x --mail-rcpt b@y -T mail-lf.txt smtp://.../c` | 0, stored with CRLF line ends |
| `--allow-anonymous` | `curl -sS --mail-from a@x --mail-rcpt b@y -T mail-lf.txt smtp://.../c` | 0, stored with its bare LFs and a CRLF before the end |
| `--allow-anonymous --max-connections 1`, a second client holding the first | `curl -sS M smtp://.../c` | 8 (`421`) |

The one-row-per-case expectations are pinned from this ADR's measurements; where the Linux or
macOS OpenSSL builds give another exit (the `552` close is the likely one), it is pinned per
platform in its own test (root `CLAUDE.md`) and recorded against this ADR.

## Alternatives considered

- **Take mail from anyone by default**, as a receiving MX does. Rejected in decision 3: surl is
  secure by default (ADR-0032) and a store any peer can fill is only bounded, not protected;
  `--allow-anonymous` gives it back for tests.
- **Answer `VRFY` `250` with the account, or `550` for an unknown one.** Rejected: it enumerates
  accounts (ADR-0032 section 8), and `550` is exit 8 for curl (row 25); `252` is RFC 5321's answer
  for exactly this. **`502` for `EXPN`.** Rejected: exit 8 (row 28), where `252` is 0.
- **Add no trace fields**, so a stored message is byte-for-byte what curl sent. Rejected in
  decision 6: RFC 5321 section 4.4 requires them of a server making final delivery, and the IMAP and
  POP3 readers of the mail expect them. The two lines are fixed by the injected clock and the
  session, so tests still pin the bytes.
- **Refuse a bare LF in the body**, as some servers do since the SMTP-smuggling reports. Rejected:
  curl without `--crlf` sends a bare-LF file as it is (row 38), and would be refused; ADR-0050's
  reader already ends the body only at CRLF `.` CRLF, which is what closes the smuggling defect.
- **Leave out `PIPELINING` and `ENHANCEDSTATUSCODES`**, which curl does not use. Rejected: both cost
  nothing with ADR-0050's reader, and a faithful SMTP server offers them; the replies were measured
  with the enhanced codes in place.
- **Echo the client's `EHLO` domain in the `250` greeting line** (`250-surl Hello <domain>`).
  Rejected: fixed reply text (ADR-0006 section 3), and curl reads nothing from it.
- **Count only the body against `--max-filesize`.** Rejected in decision 6: the store refuses a
  message past `MaxUploadBytes` again, so the trace fields must fit inside the same budget.
- **A `for <recipient>` clause in `Received`.** Rejected in decision 6: it would leak `Bcc`
  recipients through the shared message file.
- **An `IConnection` member saying whether an upgrade can succeed.** Rejected in decision 5: it
  would change eleven implementers; the server is told at construction.

## Consequences

- BL-198 builds decisions 1, 2 (without `STARTTLS` and `AUTH`), 3's `MAIL` rule, 4, 6, 7 and 8 on
  `AnonymousAuthenticationPolicy`; BL-199 decision 5; BL-200 decision 3's `AUTH` and the `AUTH`
  line of decision 2; BL-207 decision 5's `smtps` registration, the constructor flag and decision
  9; BL-210 decision 10's table.
- The fixtures those tasks replay are recorded again from this ADR's rows with
  `Record-CurlExchange.ps1 -Smtp` and the overrides named, and live under
  `Surl.Protocol.Smtp.UnitTests/Fixtures/<case>/` with a `README.md` (build, SHA-256, command line,
  date), as BL-198 says.
- A stored message is some 150 bytes larger than the body curl sent, and those bytes count against
  `--max-filesize`.
- `Record-CurlExchange.ps1` gains `-SmtpMaxMessageBytes`, described in its help.
