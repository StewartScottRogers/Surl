# ADR-0056 — How the POP3 server answers upstream curl

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-188 (FR-045).
- **Amends:** nothing. [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) section 5's POP3
  column, [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) section
  10, [ADR-0049](ADR-0049-the-mail-servers-sasl-and-apop-logins.md) (the logins, the `APOP`
  timestamp's form, the failure words) and
  [ADR-0050](ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md) (the store,
  the maildrop and its lock, the line machinery) are applied as written. No contract in
  `Surl.Protocol.Abstractions` changes.

## Context

`surl pop3://...` and `surl pop3s://...` are to be the server upstream curl's `pop3://` and
`pop3s://` requests talk to: listing a maildrop, retrieving a message, and any command a user sends
with `-X`. ADR-0050 decided the store, the maildrop (the owner's `INBOX`), its exclusive-access lock
and the fixed view; ADR-0049 the SASL and `APOP` logins and their failure words. What was left, and
what this ADR decides from measurement, is every other reply `Surl.Protocol.Pop3` sends: the greeting,
the `CAPA` list per state, each command's reply and text, the maildrop commands' semantics, `UIDL`
values, `STLS`, ADR-0006's limits in POP3's words, the verbose notes and the help category, so
BL-205, BL-206 and BL-209 are built without a question and BL-212 knows what to prove.

### What upstream curl 8.21.0 does (measured)

- **Build:** the Windows reference build, `C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
  `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0
  (x86_64-w64-mingw32) libcurl/8.21.0 Schannel ...` (`UpstreamCurlBuilds.json`).
- **Tool:** `Record-CurlExchange.ps1 -Pop3` (with `-Tls` for `pop3s://`, `-Pop3Reply` overrides,
  `-Pop3Message` and `-SaslChallenge` where a row names them), 2026-09-30, on `127.0.0.1:18110`,
  `-Pop3IdleMilliseconds 3000`. The script needed no extension: its `-Pop3` session, overrides and
  `CLOSE` reply covered every case.
- `U` below is `-sS -u u:p`, every URL is `pop3://127.0.0.1:18110` then the path shown. Every
  session opens with the recorder's greeting `+OK POP3 ready <1896.697170952@localhost>`, then
  curl's `CAPA` (answered `USER`, `SASL PLAIN LOGIN`, `STLS`, `TOP`, `UIDL`) and, with `-u`, `AUTH
  PLAIN`, `+ ` and `AHUAcA==`, unless a row says otherwise; a completed session ends with `QUIT`.
  Those lines are left out. The recorder's maildrop is two copies of a 133-byte message whose last
  line is `.A line that starts with a dot.`, sent stuffed as `..A line ...`.

| # | curl arguments | Overrides | What curl sent, in order (excerpt) | Exit | stdout / stderr |
| --- | --- | --- | --- | --- | --- |
| 1 | `U /`, and `U -l /`, `U -I /` | | `LIST` | 0 | stdout: the listing's lines `1 133`, `2 133`, CRLF each; not the status line |
| 2 | `U /1`, and `U -I /1` | | `RETR 1` | 0 | stdout: the message's 133 bytes, unstuffed (`.A line ...`); `-I` changes nothing |
| 3 | `U -l /1` | | `LIST 1` | 0 | nothing: a single-line reply's status is never printed |
| 4 | `U -X UIDL /`, `U -X CAPA /` | | `UIDL`, `CAPA` | 0 | stdout: the multi-line reply's lines as sent, without the status line and the `.` |
| 5 | `U -X 'UIDL 1' /`, `U -X 'LIST 1' /` | | `UIDL 1`, `LIST 1`, `QUIT` | 0 | nothing |
| 6 | `U -X UIDL /1`, `U -X LIST /1` | | `UIDL 1`, `LIST 1`, then no `QUIT`: curl waits for a multi-line end after the single-line `+OK 1 uid-1` until the server hangs up (3 s) | 0 | nothing |
| 7 | `U -X 'DELE 1' /`, `U -X DELE /1` | | `DELE 1`: the URL's message number is appended to the custom command | 0 | nothing |
| 8 | `U -X 'TOP 1 0' /`, `U -X TOP /1`, `U -X 'TOP 1 0' /1` | | `TOP 1 0`, `TOP 1` (no line count), `TOP 1 0 1` | 0 | stdout: the headers and the blank line |
| 9 | `U -X 'RETR 1' /` | | `RETR 1` | 0 | as 2 |
| 10 | `U -X STAT /`, `U -X NOOP /`, `U -X RSET /`, `U -X STAT /1` | | `STAT`, `NOOP`, `RSET`, `STAT 1` | 0 | nothing |
| 11 | `U -X XYZZY /` | the recorder's `-ERR Command not recognized` | `XYZZY`, then `QUIT` | 8 | `curl: (8) Weird server reply` |
| 12 | `-sS /1`, `-sS /` (no `-u`) | | `CAPA`, then `RETR 1` or `LIST` at once: without `-u` curl never logs in | 0 | as 2, as 1 |
| 13 | `-sS /1` (no `-u`) | `RETR=-ERR [AUTH] Authentication required` | `RETR 1`, `QUIT` | 8 | as 11 |
| 14 | `U /1` | `CAPA=-ERR ...` | `APOP u d727ab40...` (the greeting has a timestamp); with `GREETING=+OK POP3 ready` (none), `USER u`, `PASS p` | 0 | as 2 |
| 15 | `U /9`, `U -l /9`, `U -X 'DELE 9' /` | `RETR`, `LIST`, `DELE` `=-ERR No such message` | the command, then `QUIT` | 8 | as 11 |
| 16 | `U /1` | greeting without timestamp, `CAPA` of `USER` alone, `PASS=-ERR [IN-USE] Maildrop is locked by another session` | `USER u`, `PASS p`, then hangs up | 67 | `curl: (67) Access denied. -` |
| 17 | as 16 | `USER=-ERR [AUTH] Encryption required` | `USER u`, then hangs up: the password is never sent | 67 | as 16 |
| 18 | `U /1` | `AUTH=-ERR [IN-USE] Maildrop is locked by another session` | `AUTH PLAIN`, then hangs up | 67 | `curl: (67) Login denied` |
| 19 | `U /1` | no timestamp, `CAPA` of `TOP` and `UIDL` only | nothing after `CAPA` | 67 | as 18 |
| 20 | `U /1` | `GREETING=-ERR Too many connections` | nothing | 8 | `curl: (8) Got unexpected pop3-server response` |
| 21 | `U /1` | `RETR` closed with no reply | `RETR 1` | 56 | `curl: (56) response reading failed (errno: 0)` |
| 22 | `U /1` | `RETR=-ERR Timeout waiting for a command, closing` (and `-ERR Command line too long, closing`), `QUIT` closed with no reply | `RETR 1`, `QUIT` | 8 | as 11 |
| 23 | `U /1` | `QUIT=-ERR Some deleted messages not removed` | as 2 | 0 | as 2: curl ignores `QUIT`'s reply |
| 24 | `U /` | `LIST=+OK 2 messages\r\n1 133`, `QUIT` closed: the reply cut off | `LIST`, `QUIT` | 0 | `1 133` with no line end: what arrived |
| 25 | `U /` | `LIST=+OK 0 messages (0 octets)\r\n.` (an empty maildrop) | `LIST` | 0 | stdout: one CRLF - curl prints the terminator's leading CRLF when the listing is empty |
| 26 | `U /1` | `-Pop3Message 'Subject: d\r\n\r\n.\r\n..\r\n. x\r\nend'` | `RETR 1`; the lines went out `..`, `...`, `.. x`, `end` | 0 | `.`, `..`, `. x` unstuffed, and `end` CRLF: a last line without a line end gains one |
| 27 | `-sS -k --ssl-reqd -u u:p /1` | | `CAPA`, `STLS` (`+OK`, TLS 1.2 handshake), `CAPA` again (now without `STLS`), `AUTH PLAIN`, `RETR 1` | 0 | as 2 |
| 28 | as 27 | `STLS=-ERR STLS not available` | `STLS`, then hangs up | 64 | `curl: (64) STARTTLS denied` |
| 29 | as 27 with `--ssl` for `--ssl-reqd` | as 28 | `STLS`, then `AUTH PLAIN` ... in plaintext | 0 | as 2 |
| 30 | as 27 | `CAPA` without `STLS` | nothing after `CAPA` | 64 | `curl: (64) STLS not supported.` |
| 31 | `-sS -k -u u:p 'pop3s://127.0.0.1:18110/1'`, `-Tls` | | TLS from the first byte, `CAPA` (no `STLS`), then as 2 | 0 | as 2 |
| 32 | `-sS -u user:secret --login-options AUTH=+APOP /1` | greeting `+OK surl ready <0123456789abcdef.1790640000@surl>`, `CAPA` with `USER` and `SASL CRAM-MD5 PLAIN` | `APOP user 32d4437494fda0ae78d0559952474e34`: forced, `APOP` wins over the SASL offer | 0 | as 2 |
| 33 | `U --sasl-ir /` | | `AUTH PLAIN AHUAcA==`: the initial response only with `--sasl-ir` (POP3 has no `SASL-IR` capability) | 0 | as 1 |

**The decided replies, measured.** The greeting, `CAPA` lists and texts decisions 2 to 5 pin were
then served to curl through `-Pop3Reply` and `-SaslChallenge`, and curl completed each exchange:

| # | curl arguments | What was served | Exit |
| --- | --- | --- | --- |
| D1 | `-sS -u user:secret /1` | decision 2's greeting `+OK surl ready`, decision 3's plaintext `CAPA` (`TOP`, `UIDL`, `RESP-CODES`, `AUTH-RESP-CODE`, `PIPELINING`, `SASL CRAM-MD5`, `STLS`), a `CRAM-MD5` challenge, decision 4's `QUIT` `+OK surl signing off` | 0: curl picked `CRAM-MD5`, the only mechanism offered, and did not pipeline though `PIPELINING` was advertised |
| D2 | `-sS -u user:secret --login-options AUTH=PLAIN /1` | as D1 | 67 `Login denied`: `PLAIN` not offered over plaintext, nothing sent after `CAPA` |
| D3 | `-sS -k -u user:secret --login-options AUTH=PLAIN 'pop3s://.../1'` | decision 3's TLS `CAPA` (`... USER`, `SASL CRAM-MD5 OAUTHBEARER XOAUTH2 PLAIN LOGIN`), the `+ ` challenge | 0 |
| D4 | `U -X UIDL /` | decision 5's `UIDL` values `1790726400.1`, `1790726400.2` | 0; stdout `1 1790726400.1`, `2 1790726400.2` |
| D5 | `U -X RSET /` | `+OK Maildrop has 2 messages (266 octets)` | 0 |
| D6 | `-sS -u user:secret /1` | decision 2's greeting with the timestamp `<0123456789abcdef.1790640000@surl>` and decision 3's plaintext `CAPA` under `--auth apop` (the five fixed items alone) | 0: curl sent `APOP user 32d4437494fda0ae78d0559952474e34` unforced |

What curl 8.21.0 sends of its own is `CAPA`, `STLS`, `USER`/`PASS`, `APOP`, `AUTH`, `LIST`,
`LIST <n>` (with `-l`), `RETR <n>` and `QUIT`; it never sends `STAT`, `UIDL`, `TOP`, `DELE`, `RSET`
or `NOOP` unasked, never pipelines, never sends a command before reading the previous reply, and
anything else reaches the server only through `-X`, sent as given with the URL's message number
appended after a space. **Whether curl reads a custom command's reply as multi-line is decided by
curl from the `-X` text alone** (rows 5 to 8): a bare `LIST`, `UIDL`, `TOP`, `RETR` or `CAPA` is
multi-line to curl even when the URL adds a number; a command with its own argument, and every
other command, is single-line. curl prints a multi-line reply's content lines, unstuffed, and
nothing of a single-line reply; any `-ERR` is exit 8, except during the login (67) and `STLS` (64).

## Decision

### 1. The reply form

Every reply is `+OK <text>` or `-ERR <text>` and CRLF, written through ADR-0050 decision 8's
reply-line writer, so no byte a peer sent can reach a reply unescaped. A multi-line reply (RFC 1939
section 3) is the `+OK` status line, the content lines - dot-stuffed through ADR-0050 decision 8's
writer for message bytes, and built only from numbers and fixed words otherwise - and `.` CRLF. The
text is fixed text from this ADR (ADR-0006 section 3): no argument the peer sent is ever echoed,
except as decision 5 says for message numbers the server itself assigned. A command is its first
word, matched without regard to case (RFC 1939 section 3); its arguments are the rest of the line
split at single spaces, except `PASS`, whose argument is the whole rest of the line after one space
(RFC 1939 section 7 lets a password hold spaces). An argument that is a message number is 1 to 10
ASCII digits with a value from 1 to the view's count; anything else in its place is `-ERR Invalid
arguments`.

**Response codes** (RFC 2449 section 8, advertised as `RESP-CODES` and `AUTH-RESP-CODE`) are used
where this ADR shows one: `[AUTH]` and `[SYS/TEMP]` (RFC 3206), `[IN-USE]` (RFC 2449 section 8.1.1).
`[LOGIN-DELAY]` and `[SYS/PERM]` are not used.

### 2. The greeting and its `APOP` timestamp

- **The greeting** is `+OK surl ready` - `surl` in place of a host name, no version (ADR-0006
  section 3) - followed, only when
  `IMailAuthenticationPolicy.GetMailLoginOffer(connection.TlsSession).IsApopOffered` is true when
  the connection opens (the `apop` word in `--auth`, ADR-0049 section 2), by a space and the
  timestamp: `+OK surl ready <0123456789abcdef.1790640000@surl>`.
- **The timestamp** has ADR-0049 section 5's `CRAM-MD5` challenge form, RFC 1939 section 7's
  `msg-id`: `<`, 16 lower-case hex digits of 8 bytes from an injected `RandomNumberGenerator`, `.`,
  the injected `TimeProvider`'s Unix time in seconds, `@surl>`. It tells nothing about the host,
  and a fresh one is made for every connection, so an `APOP` digest is never valid twice. The
  server keeps it for the session and passes it to `CheckApopLoginAsync`.
- A timestamp is sent only when `APOP` is offered: measured, curl uses `APOP` over `USER`/`PASS`
  whenever the greeting carries one and `CAPA` has no `SASL` (row 14), and forced with
  `--login-options AUTH=+APOP` even beside `SASL` (row 32).

### 3. `CAPA`, per state

`CAPA` (RFC 2449) answers `+OK Capability list follows`, then one capability per line in this
order, each present only as said, then `.`. It is computed afresh each time, since it changes with
TLS and with the login.

| Capability | Present |
| --- | --- |
| `TOP`, `UIDL`, `RESP-CODES`, `AUTH-RESP-CODE`, `PIPELINING` | always |
| `USER` | before login, when `GetMailLoginOffer(connection.TlsSession).IsClearPasswordLoginOffered` (ADR-0049 section 2) |
| `SASL <m1> <m2> ...` | before login, when `GetMailLoginOffer(...).SaslMechanisms` is not empty, in its order |
| `STLS` | before login, on a plaintext connection whose server can upgrade (decision 8) |

With the default `--auth` set that is, over plaintext with a certificate configured, `TOP UIDL
RESP-CODES AUTH-RESP-CODE PIPELINING SASL CRAM-MD5 STLS` (D1: `curl -u` logs in with `CRAM-MD5`),
and over TLS `TOP UIDL RESP-CODES AUTH-RESP-CODE PIPELINING USER SASL CRAM-MD5 OAUTHBEARER XOAUTH2
PLAIN LOGIN` (D3). After a login it is the first five alone.

- **`PIPELINING`** (RFC 2449 section 6.6) costs nothing: ADR-0050 decision 8's reader keeps
  pipelined bytes buffered and every command is answered in order. curl does not pipeline (D1).
  `STLS` discards what is buffered after it (decision 8).
- **Not advertised:** `IMPLEMENTATION` (it would name the software, ADR-0006 section 3),
  `EXPIRE` and `LOGIN-DELAY` (Surl removes no mail by itself and delays no login; RFC 2449 lets a
  server leave both out), `UTF8` and `LANG` (RFC 6856: curl sends neither `UTF8` nor `LANG`, and
  replies are fixed ASCII).

### 4. Commands, states and the reply table

**States** (RFC 1939 section 3): AUTHORIZATION, then TRANSACTION after a login, then UPDATE on
`QUIT`. A command not valid in the session's state is answered as the table says and the session
goes on.

| Command | State | Reply |
| --- | --- | --- |
| `CAPA` | any | decision 3 |
| `QUIT` | AUTHORIZATION | `+OK surl signing off`, then close (`CompleteWritesAsync`) |
| `QUIT` | TRANSACTION | decision 5's UPDATE, then `+OK surl signing off` (or `-ERR [SYS/TEMP] Some deleted messages not removed`), then close |
| `NOOP` | TRANSACTION | `+OK` |
| `STLS` | AUTHORIZATION | decision 8 |
| `USER <name>` | AUTHORIZATION | `+OK User accepted` for any name, so no reply tells a peer whether an account exists (ADR-0032 section 8); `-ERR [AUTH] Encryption required` when `USER` is not in `CAPA` (ADR-0049 section 7: the password is then never sent, row 17) |
| `PASS <password>` | AUTHORIZATION, straight after an accepted `USER` | ADR-0049 section 7 through `CheckPasswordLoginAsync`: `+OK Logged in`, `-ERR [AUTH] Authentication failed`, `-ERR [AUTH] Encryption required`; then decision 6's lock |
| `PASS` not straight after an accepted `USER` | AUTHORIZATION | `-ERR Send USER first`; any failed `PASS` forgets the `USER` (RFC 1939 section 7) |
| `APOP <name> <digest>` | AUTHORIZATION | ADR-0049 section 7 through `CheckApopLoginAsync` with decision 2's timestamp: `+OK Authentication successful`, the `AUTH` failure words; `-ERR Unsupported authentication mechanism` when the greeting carried no timestamp or `APOP` is no longer offered; then decision 6's lock |
| `AUTH <mechanism> [<initial response>]` | AUTHORIZATION | ADR-0049 section 7's words, `+ <base64>` continuations; success `+OK Authentication successful`, then decision 6's lock. The initial response, `=` for an empty one (RFC 5034 section 4), is accepted whenever sent (row 33: curl sends one only with `--sasl-ir`) |
| `AUTH` with no mechanism | AUTHORIZATION | `+OK SASL mechanisms follow`, the offered mechanisms one per line, `.` - the RFC 1734 listing older clients ask for; empty when none is offered |
| `STAT`, `LIST`, `RETR`, `DELE`, `RSET`, `TOP`, `UIDL`, `NOOP` | AUTHORIZATION | decision 7's check for a login |
| `STLS`, `USER`, `PASS`, `APOP`, `AUTH` | TRANSACTION | `-ERR Already logged in` |
| `STAT`, `LIST`, `RETR`, `DELE`, `RSET`, `TOP`, `UIDL` | TRANSACTION | decision 5 |
| a known command with arguments it does not take, too few, or a bad message number | any | `-ERR Invalid arguments` |
| anything else | any | `-ERR Command not recognized` (curl exits 8, row 11) |

### 5. The maildrop commands

The view is ADR-0050 decision 4's: at the login, the messages then in the owner's `INBOX`,
numbered 1 upward in UID order, each with its size and UID; nothing another session does changes
it. A **deleted** message is one marked by `DELE` in this session. A message number past the view,
or naming a deleted message, is `-ERR No such message` (curl exits 8, row 15).

| Command | Reply |
| --- | --- |
| `STAT` | `+OK <count> <octets>`: the messages not deleted and the sum of their sizes (RFC 1939 section 5) |
| `LIST` | `+OK <count> messages (<octets> octets)`, then `<number> <size>` for each message not deleted, ascending, then `.` |
| `LIST <n>` | `+OK <n> <size>` |
| `RETR <n>` | `+OK <size> octets`, then the stored bytes, dot-stuffed, then `.` |
| `TOP <n> [<lines>]` | `+OK Top of message follows`, then the header section, the empty line that ends it and the first `<lines>` lines of the body, dot-stuffed, then `.` |
| `UIDL` | `+OK Unique-ID listing follows`, then `<number> <unique-id>` for each message not deleted, then `.` |
| `UIDL <n>` | `+OK <n> <unique-id>` |
| `DELE <n>` | `+OK Message deleted`, the message marked |
| `RSET` | `+OK Maildrop has <count> messages (<octets> octets)`, every mark removed (RFC 1939 section 5's example) |
| `NOOP` | `+OK` |

- **A size** is the stored byte count (ADR-0050 decision 3), which is what `RETR` sends before
  stuffing. A message whose bytes do not end with CRLF gains one on the wire (ADR-0050 decision 8's
  writer; row 26), so for such a message `RETR` sends two bytes more than its size, as RFC 1939
  section 11 allows ("the octet count ... may be off"). Every message SMTP delivers ends with CRLF
  (ADR-0053 decision 6).
- **`TOP`**: the header section ends at the first empty line (CRLF CRLF, the first line itself
  included when it is empty); a message without one is all header and `TOP` sends all of it. Body
  lines are counted by CRLF; a count past the body sends the whole message. `<lines>` is 0 to
  2147483647; **when it is left out it is 0**, so `curl -X TOP pop3://h/1`, which sends `TOP 1`
  (row 8), gets the headers, as it did from the recorder, rather than an error; RFC 1939 section 7
  makes the argument required, and accepting its absence costs nothing and breaks no client.
- **The unique-id** (RFC 1939 section 7) is `<uidvalidity>.<uid>` in decimal, from the store's
  `UIDVALIDITY` of the `INBOX` and the message's UID (ADR-0050 decision 3): 3 to 21 characters of
  0x30 to 0x39 and `.`, inside RFC 1939's 1 to 70 of 0x21 to 0x7E. It never changes while the
  message exists, and is never given to another message, since a UID is never reused within a
  `UIDVALIDITY` and a new `UIDVALIDITY` is never an old one. Measured, curl prints it as sent (D4).
- **`QUIT` in TRANSACTION** enters UPDATE: the deleted messages are expunged from the store by UID,
  all in one store operation, including any IMAP has since removed (a no-op for those, ADR-0050
  decision 4); then `+OK surl signing off`. When the store refuses (`StorageFailed`) nothing is
  removed and the reply is `-ERR [SYS/TEMP] Some deleted messages not removed` (RFC 1939 section 6;
  curl ignores it and exits 0, row 23). The lock is released and the connection closed either way.
- **A session that ends without `QUIT`** - the peer hangs up, a limit closes it, `STLS` fails -
  removes nothing (RFC 1939 section 6) and releases the lock.

### 6. Logins and the maildrop lock

- `USER`/`PASS`, `APOP` and `AUTH` run ADR-0049's exchanges through `IMailAuthenticationPolicy`
  and `CheckPasswordLoginAsync`, and answer in ADR-0049 section 7's POP3 words; each `CheckedLogin`
  note is written before the reply (ADR-0038).
- **On an accepted login** the server asks the store for the owner's maildrop lock (ADR-0050
  decision 4) before it replies. Granted, the view is built and the reply is the login's success;
  refused (`MaildropLocked`), the reply is `-ERR [IN-USE] Maildrop is locked by another session`
  (RFC 2449 section 8.1.1), the session stays in AUTHORIZATION with no login, and the `USER` is
  forgotten. curl exits 67 (rows 16 and 18). The lock is held until the session ends, however it
  ends.
- The owner is ADR-0050 decision 2's: the account named, or the anonymous owner for
  `AcceptedUnchecked`.

### 7. A maildrop command before any login

curl without `-u` sends `LIST` or `RETR` straight after `CAPA` (row 12). A maildrop command in
AUTHORIZATION asks the policy once with `CheckPasswordLoginAsync(new PasswordLogin(scheme, null,
null, connection.TlsSession))`, as SMTP's first `MAIL` (ADR-0053 decision 3) and IMAP's first
authenticated-state command (ADR-0055 decision 10) do, and keeps the verdict for the session:

- `AcceptedUnchecked` (`--allow-anonymous`): the session takes the anonymous owner's lock as
  decision 6 says, with no login note, enters TRANSACTION and runs the command; `curl
  pop3://.../1` with no `-u` then works. A refused lock answers the command `-ERR [IN-USE]
  Maildrop is locked by another session` and the session stays in AUTHORIZATION.
- anything else: `-ERR [AUTH] Authentication required`, and the session stays in AUTHORIZATION
  (curl exits 8, row 13).

**Why:** secure by default (ADR-0032) - nobody reads mail without an account unless the operator
loosens it - while curl's natural no-login use works under `--allow-anonymous`.

### 8. `STLS` and `pop3s://`

- **Whether the server can upgrade** is given to `Pop3ProtocolServer`'s constructor as a `bool`,
  which `Surl.Console` sets when a server certificate is configured (`--cert`, or `--self-signed`,
  ADR-0032 section 10), as ADR-0053 decision 5 does for SMTP.
- **`STLS`** on a plaintext connection that can upgrade, before login: `+OK Begin TLS negotiation`,
  then ADR-0050 decision 8's discard (every byte buffered after the `STLS` line thrown away and its
  count noted, ADR-0010 section 1), then `IConnection.UpgradeToTlsAsync`. The session starts over
  in AUTHORIZATION with any `USER` forgotten (RFC 2595 section 4); the greeting's timestamp stays
  the session's. curl asks `CAPA` again (row 27), which decision 3 answers for TLS.
- **No certificate**: `-ERR STLS not available`, and `STLS` is never advertised (decision 3). curl
  exits 64 with `--ssl-reqd` (rows 28 and 30) and goes on in plaintext with `--ssl` (row 29) -
  ADR-0032 section 10's "refused in its own words".
- **Already TLS** (after `STLS`, or `pop3s://`): `-ERR Already using TLS`. After a login: decision
  4's `-ERR Already logged in`.
- **A failed handshake** ends the connection with the engine's `TLS handshake failed: ` note;
  nothing is written after it.
- **`pop3s://`** is implicit TLS: `Pop3ProtocolServer` claims the scheme `pop3` only, and
  `Surl.Console` registers it for `pop3s` through `ImplicitTlsSchemeServer`, as SMTP's `smtps` and
  IMAP's `imaps` (BL-209). The server tells the two apart by `connection.TlsSession`, never by the
  scheme. Default ports are curl's: 110 for `pop3`, 995 for `pop3s`.

### 9. Lines and limits (ADR-0006 section 5, POP3 column)

A command line, and a SASL continuation line, is read by ADR-0050 decision 8's reader: it ends
only at CRLF, bounded by `--max-line` (8192 bytes by default, CRLF included) and the head timeout
(`--head-timeout`). RFC 2449 section 4's 255-octet command limit is not applied: a SASL initial
response on the `AUTH` line is longer (RFC 5034 section 4 lifts it for that line), and ADR-0006's
single bound serves every line protocol.

| Limit | POP3 answer | curl (measured) |
| --- | --- | --- |
| Too many connections (`IConnectionRefusalWriter`, which the server implements) | `-ERR surl Too many connections, closing` in place of the greeting, then close | 8, row 20 |
| Head timeout (a command line not finished within `--head-timeout`) | `-ERR Timeout waiting for a command, closing`, then close | 8 when it answers a command, row 22 |
| A line past `--max-line` | `-ERR Command line too long, closing`, then close | 8, row 22 |
| Idle timeout, maximum duration | close with no bytes (ADR-0006) | 56, row 21 |

The session also ends with a close when `QUIT` is answered. No limit ever sends a partial
multi-line reply: every reply is written whole, the maximum duration cancelling only between
replies or during a write, where the close is what curl sees (row 24: it keeps what arrived).
`STAT`, `LIST` and `UIDL` hold one line per message of the view, bounded by the store's 100000
messages (ADR-0050 decision 6).

### 10. The verbose notes (ADR-0033, ADR-0038)

Written with `IExchangeLog.Note` at `verbose` and above, every peer byte escaped as ADR-0006
section 3 says.

| When | Note |
| --- | --- |
| A login granted the maildrop | `Maildrop opened: <count> messages, <octets> octets` |
| A login refused for the lock | `Maildrop locked by another session` |
| `QUIT` removed messages | `<n> messages removed from the maildrop` (only when `<n>` is not 0) |
| `QUIT` could not remove them | `Mail store: <exception message>` (ADR-0050 decision 7) |
| Bytes discarded after `STLS` | `Discarded <n> bytes sent after STLS` (only when `<n>` is not 0) |
| A maildrop command refused for want of a login | `<COMMAND> refused: log in first, or give --allow-anonymous` |
| A checked `PASS`, `APOP` or `AUTH` | `CheckedLogin.Note` from the step, before the reply (ADR-0038, ADR-0049 section 7) |

### 11. Help

The `pop3` category, `POP3 and POP3S protocol` (ADR-0034 decision 1's wording), claims the schemes
`pop3` and `pop3s`, and holds every option the POP3 server reads: the limits (`--max-line`,
`--head-timeout`), the login options (`--user`, `--user-file`, `--allow-anonymous`,
`--allow-plaintext-auth`, `--auth`) and `--directory` (where the mail store persists, ADR-0050
decision 7). `--max-filesize` is not in it: POP3 receives no upload. BL-209 adds the category, its
`--aihelp` topic, prose and example, and grows the pinned topic lists, as root `CLAUDE.md` requires.

### 12. What BL-212 proves with the pinned build

Each against `surl` over loopback through the conformance harness, with the options named; `-k`
wherever TLS is used with `--self-signed`. The mail store is seeded by an SMTP delivery of
`mail.txt` (`From: a@x`, `Subject: hi`, an empty line, `hello`, CRLF line ends) to `tester`
(BL-210's case) before each row, every row against a fresh `surl` and store, the rows with two
commands running them in turn against the same one, so the maildrop holds one message, stored as
ADR-0053 decision 6's two trace fields followed by `mail.txt`'s bytes (call those bytes `S`, and
their count `|S|`). `A` is `-u tester:secret`. Exits and stdout:

| surl options | curl command line | Exit | stdout |
| --- | --- | --- | --- |
| `--user tester:secret` | `curl -sS A pop3://127.0.0.1:<p>/` | 0 | `1 <|S|>` CRLF (`CRAM-MD5` login) |
| `--user tester:secret` | `curl -sS A pop3://.../1` | 0 | `S` exactly |
| `--user tester:secret` | `curl -sS A -l pop3://.../`, and `curl -sS A -I pop3://.../` | 0 | `1 <|S|>` CRLF |
| `--user tester:secret` | `curl -sS A -l pop3://.../1` | 0 | nothing |
| `--user tester:secret` | `curl -sS A -I pop3://.../1` | 0 | `S` |
| `--user tester:secret` | `curl -sS A pop3://.../9` | 8 | |
| `--user tester:secret` | `curl -sS A -X UIDL pop3://.../` | 0 | `1 <uidvalidity>.1` CRLF, the `UIDVALIDITY` being the store's |
| `--user tester:secret` | `curl -sS A -X 'TOP 1 0' pop3://.../`, and `curl -sS A -X TOP pop3://.../1` | 0 | `S`'s header lines and the empty line |
| `--user tester:secret` | `curl -sS A -X CAPA pop3://.../` | 0 | `TOP`, `UIDL`, `RESP-CODES`, `AUTH-RESP-CODE`, `PIPELINING`, CRLF each |
| `--user tester:secret` | `curl -sS A -X STAT pop3://.../`, `-X NOOP`, `-X RSET` | 0 each | nothing |
| `--user tester:secret` | `curl -sS A -X 'DELE 1' pop3://.../`, then `curl -sS A pop3://.../` | 0, 0 | nothing; then one CRLF (the maildrop is empty) |
| `--user tester:secret` | `curl -sS A -X XYZZY pop3://.../` | 8 | |
| `--user tester:secret` | `curl -sS -u tester:wrong pop3://.../1` | 67 | |
| `--user tester:secret` | `curl -sS pop3://.../1` (no `-u`) | 8 | |
| `--allow-anonymous` (message delivered anonymously over SMTP) | `curl -sS pop3://.../1` | 0 | `S` |
| `--user tester:secret` | `curl -sS A --login-options AUTH=PLAIN pop3://.../1` (plaintext, `PLAIN` not offered) | 67 | |
| `--user tester:secret --allow-plaintext-auth` | the same, and `curl -sS A --login-options AUTH=LOGIN pop3://.../1` | 0 each | `S` |
| `--user tester:secret --auth basic --allow-plaintext-auth` (no mail mechanism, so `CAPA` offers `USER` alone and the greeting has no timestamp) | `curl -sS A pop3://.../1`: `USER`/`PASS` | 0 | `S` |
| `--user tester:secret --auth basic` (plaintext: `CAPA` offers neither `USER` nor `SASL`) | `curl -sS A pop3://.../1` | 67 | |
| `--user tester:secret --auth basic --allow-plaintext-auth` | `curl -sS -u tester:wrong pop3://.../1` | 67 | |
| `--user tester:secret --auth apop` | `curl -sS A --login-options AUTH=+APOP pop3://.../1`, and the same without `--login-options` | 0 each | `S` |
| `--user tester:secret --self-signed` | `curl -sS -k --ssl-reqd A pop3://.../1` | 0 | `S` |
| `--user tester:secret` (no certificate) | `curl -sS --ssl-reqd A pop3://.../1` | 64 | |
| `--user tester:secret --self-signed` | `curl -sS -k A pop3s://.../1` | 0 | `S` |
| `--user tester:secret --self-signed` | `curl -sS -k A --login-options AUTH=<mech> pop3s://.../1` for `PLAIN` (with and without `--sasl-ir`), `LOGIN`, `CRAM-MD5`; `-u tester: --oauth2-bearer tok` with `--user :tok` for `XOAUTH2` and `OAUTHBEARER`; `--auth digest-md5` for `DIGEST-MD5`, `--auth ntlm` for `NTLM` | 0 each | `S` |
| `--user tester:secret --self-signed --auth basic` | `curl -sS -k A pop3s://.../1`: `USER`/`PASS` over TLS | 0 | `S` |
| `--user tester:secret`, the maildrop held by a second session (a raw socket that logged in and stays) | `curl -sS A pop3://.../1` | 67 (`[IN-USE]`) | |
| `--user tester:secret --max-connections 1`, a second client holding the first | `curl -sS A pop3://.../1` | 8 | |

**`USER`/`PASS` against pinned curl.** curl sends `USER`/`PASS` only when `CAPA` offers no `SASL`
and the greeting no timestamp (ADR-0049's measurements; rows 14 and 16). With the default `--auth`
set Surl always offers `SASL`, so the rows reach `USER`/`PASS` with `--auth basic`: an HTTP-only
set leaves the mail servers no mechanism and no `APOP` (ADR-0049 decision 3), while the clear
password stays outside `--auth` (ADR-0049 section 7).

The rows are pinned from this ADR's measurements; where the Linux or macOS OpenSSL builds give
another exit, it is pinned per platform in its own test (root `CLAUDE.md`) and recorded against
this ADR.

## Alternatives considered

- **A timestamp in every greeting**, as many POP3 servers send. Rejected in decision 2: curl then
  uses `APOP` whenever `CAPA` offers no `SASL` (row 14), and `APOP` is off by default for its MD5
  weakness (ADR-0049 decision 3), so a timestamp that cannot be used would invite a login that
  must be refused.
- **Name the host in the greeting and `IMPLEMENTATION` in `CAPA`**. Rejected: ADR-0006 section 3.
- **Refuse `TOP` without a line count**, as RFC 1939 reads. Rejected in decision 5: `curl -X TOP
  pop3://h/1` sends exactly that, and reading the missing count as 0 breaks no client.
- **Answer `USER` for an unknown name with `-ERR`**, as RFC 1939 permits. Rejected: it tells any
  peer which accounts exist (ADR-0032 section 8, ADR-0050 decision 5's rule for recipients).
- **Refuse every maildrop command before a login.** Rejected in decision 7: curl's natural
  `pop3://h/1` without `-u` would never work, even with `--allow-anonymous`; the implicit
  anonymous check is SMTP's and IMAP's rule in POP3's words.
- **`UIDL` values from the UID alone.** Rejected in decision 5: a new `UIDVALIDITY` restarts UIDs,
  so the bare UID could name two messages over a store's life; RFC 1939 forbids reusing an id.
- **Leave out `PIPELINING` and the response-code capabilities**, which curl does not use.
  Rejected: both cost nothing, and the codes tell a mail client why a login failed (`[IN-USE]`
  against `[AUTH]`), which RFC 3206 exists for.
- **Honour RFC 2449's 255-octet command limit.** Rejected in decision 9: SASL lines need more,
  and ADR-0006's `--max-line` is the one bound every line protocol shares.

## Consequences

- BL-205 builds decisions 1, 2 (without the timestamp), 3 (without `SASL` and `STLS`), 4 to 7
  (`USER`/`PASS` as the login), 9 and 10; BL-206 decision 8, decision 2's timestamp, decision 3's
  `SASL` and `STLS` items and decision 4's `APOP` and `AUTH` rows; BL-209 decision 8's `pop3s`
  registration, the constructor flag and decision 11; BL-212 decision 12's table.
- The fixtures those tasks replay are recorded again from this ADR's rows with
  `Record-CurlExchange.ps1 -Pop3` and the overrides named, and live under
  `Surl.Protocol.Pop3.UnitTests/Fixtures/<case>/` with a `README.md` (build, SHA-256, command line,
  date).
- A custom command curl reads as multi-line but that has a single-line answer (row 6: `curl -X LIST
  pop3://h/1`) leaves curl waiting until surl's idle timeout closes the connection. That is curl's
  reading of its own `-X` text; the server answers RFC 1939, and BL-212 does not test it.
- `Record-CurlExchange.ps1` is unchanged: its `-Pop3` mode measured every case.
