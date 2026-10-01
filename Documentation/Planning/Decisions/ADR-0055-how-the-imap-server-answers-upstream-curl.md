# ADR-0055 — How the IMAP server answers upstream curl

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-187 (FR-044).
- **Amends:** nothing. [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) section 5's IMAP
  column, [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)'s
  "Protocol servers not yet built" IMAP row, [ADR-0049](ADR-0049-the-mail-servers-sasl-and-apop-logins.md)
  (the logins, `LOGINDISABLED`, `SASL-IR`, the failure words) and
  [ADR-0050](ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md) (the store,
  its flags, bounds and outcomes, and the line machinery) are applied as written. No contract in
  `Surl.Protocol.Abstractions` changes.

## Context

`surl imap://...` and `surl imaps://...` are to be the server upstream curl's `imap://` and
`imaps://` requests talk to: listing mailboxes, fetching a message or a part of one, searching,
appending with `-T`, and any command a user sends with `-X`. ADR-0050 decided the mail store and
the line machinery, ADR-0049 the SASL logins and their failure words. What was left, and what this
ADR decides from measurement, is every other response `Surl.Protocol.Imap` sends: the protocol
version and capabilities per state, the greeting, each command's untagged data and tagged
completion, the `FETCH` item formats and literals, the `SEARCH` keys, `APPEND` and its bound, the
flags, mailbox names, `STARTTLS`, ADR-0006's limits in IMAP's words, the verbose notes and the
help category, so BL-201 to BL-204 and BL-208 are built without a question and BL-211 knows what to
prove.

### What upstream curl 8.21.0 does (measured)

- **Build:** the Windows reference build, `C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
  `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0
  (x86_64-w64-mingw32) libcurl/8.21.0 Schannel ...` (`UpstreamCurlBuilds.json`).
- **Tool:** `Record-CurlExchange.ps1 -Imap` (with `-Tls` for `imaps://`, `-ImapReply` overrides and
  `-SaslChallenge` where a row names them), 2026-09-30, on `127.0.0.1:18143`. The script needed no
  extension: its `-Imap` session, overrides and `CLOSE` reply covered every case.
- `U` below is `-sS -u u:p`, every URL is `imap://127.0.0.1:18143` then the path shown, and
  `mail.txt` is `From: a@x`, `Subject: hi`, an empty line and `hello`, every line ending CRLF (33
  bytes). Every session opens with the recorder's greeting `* OK [CAPABILITY IMAP4rev1 STARTTLS
  AUTH=PLAIN AUTH=LOGIN] ready`, then curl's `A001 CAPABILITY` and, with `-u`, `A002 AUTHENTICATE
  PLAIN` and its response, unless a row says otherwise; a completed session ends with `LOGOUT`.
  Those lines are left out. `>` is curl, `<` the recorder; the recorder's default `SELECT` reports
  `2 EXISTS` and `UIDVALIDITY 1`, and its `FETCH` sends a 100-byte message as a literal.

| # | curl arguments | Overrides | What curl sent, in order (excerpt) | Exit | stdout / stderr |
| --- | --- | --- | --- | --- | --- |
| 1 | `U /`, and `U` with no path | | `LIST "" *` | 0 | stdout: every untagged `* LIST` line as sent, CRLF |
| 2 | `U /INBOX`, `U /INBOX/` | | `LIST "INBOX" *`: a path with no `;UID=` or `;MAILINDEX=` lists, with the mailbox as the reference | 0 | as 1 |
| 3 | `U /My%20Box` | | `LIST "My Box" *` | 0 | as 1 |
| 4 | `U '/INBOX;UID=1'`, `U '/INBOX/;UID=1'` | | `SELECT INBOX`, `UID FETCH 1 BODY[]` | 0 | stdout: the literal's 100 bytes, nothing else |
| 5 | `U '/INBOX;UID=1;SECTION=TEXT'` | | `UID FETCH 1 BODY[TEXT]` | 0 | the literal |
| 6 | `U '/INBOX;UID=1;SECTION=HEADER.FIELDS%20(SUBJECT)'` | | `UID FETCH 1 BODY[HEADER.FIELDS (SUBJECT)]` | 0 | the literal |
| 7 | `U '/INBOX;UID=1;SECTION=1'` | | `UID FETCH 1 BODY[1]` | 0 | the literal |
| 8 | `U '/INBOX;UID=1;PARTIAL=0.100'`, `U '/INBOX;UID=1/;SECTION=TEXT;PARTIAL=0.10'` | | `UID FETCH 1 BODY[]<0.100>`, `UID FETCH 1 BODY[TEXT]<0.10>` | 0 | the literal |
| 9 | `U '/INBOX;UID=1;PARTIAL=0.10'` | `UID FETCH` answered `* 1 FETCH (UID 1 BODY[]<0> {10}` ... (RFC 3501 section 7.4.2's form: the origin only) | as 8 | 0 | the 10 bytes |
| 10 | `U '/INBOX;MAILINDEX=1'` | | `SELECT INBOX`, `FETCH 1 BODY[]` | 0 | the literal |
| 11 | `U '/INBOX;UIDVALIDITY=1/;UID=1'` | | `SELECT INBOX`, `UID FETCH 1 BODY[]`: curl compares `SELECT`'s `UIDVALIDITY` itself | 0 | the literal |
| 12 | `U '/INBOX;UIDVALIDITY=7/;UID=1'` | | `SELECT INBOX`, then `LOGOUT`: no fetch | 78 | `curl: (78) Mailbox UIDVALIDITY has changed` |
| 13 | as 12 | `SELECT` with no `UIDVALIDITY` code | `SELECT`, `UID FETCH 1 BODY[]`: no code, no check | 0 | the literal |
| 14 | `U '/INBOX;UID=1:2'` | two `FETCH` responses, `hello` and `world` | `UID FETCH 1:2 BODY[]` | 0 | `hello`: only the first literal |
| 15 | `U '/INBOX;UID=1'` | `FLAGS (\Seen)` before `BODY[]`; after the literal; `UID` after the literal | as 4 | 0 | the literal each time |
| 16 | `U '/INBOX;UID=1'` | `UID FETCH` answered `* 1 FETCH (UID 1 BODY[] "hello")` | as 4 | 8 | `curl: (8) Failed to parse FETCH response.`: a body must be a literal |
| 17 | `U '/INBOX;UID=9'` | `UID FETCH=OK FETCH completed` (no data) | as 4 | 78 | `curl: (78) Remote file not found` |
| 18 | `U '/INBOX;MAILINDEX=9'` | `FETCH=BAD ...`, and `FETCH=NO ...` | as 10 | 78 | as 17 |
| 19 | `U '/Nope;UID=1'` | `SELECT=NO [NONEXISTENT] Mailbox does not exist` | `SELECT Nope`, `LOGOUT` | 67 | `curl: (67) Select failed` |
| 20 | `U '/INBOX;UID=1'` | `SELECT=BAD Command line too long` | `SELECT INBOX`, `LOGOUT` | 67 | as 19 |
| 21 | `U '/INBOX;UID=1'` | `UID FETCH` closed with no reply | `UID FETCH 1 BODY[]` | 56 | `curl: (56) response reading failed (errno: 0)` |
| 22 | `U '/INBOX;UID=1'` | `UID FETCH` answered `* BYE surl Timeout, closing` then a tagged `BAD` | `UID FETCH`, `LOGOUT` | 78 | as 17 |
| 23 | `U '/Archive/2026;UID=1'`, `U '/My%20Box;UID=1'`, `U '/a%22b;UID=1'`, `U '/inbox;UID=1'` | | `SELECT Archive/2026`, `SELECT "My Box"`, `SELECT "a\"b"`, `SELECT inbox`: `/` inside the path is sent as it is, a name that needs it is quoted, case is kept | 0 | the literal |
| 24 | `U '/%C3%A4;UID=1'` | | `SELECT` then the two UTF-8 bytes `C3 A4` unquoted: curl sends no modified UTF-7 | 0 | the literal |
| 25 | `U '/INBOX?SUBJECT%20x'` | | `SELECT INBOX`, `SEARCH SUBJECT x` | 0 | stdout: `* SEARCH 1 2` CRLF |
| 26 | `U -T mail.txt /INBOX`, `/My%20Box` | | `APPEND INBOX (\Seen) {33}` (`"My Box"`), the 33 bytes after `+`, then CRLF | 0 | |
| 27 | `U -T mail.txt /` | | `APPEND mail.txt (\Seen) {33}`: the upload's name is appended to a path ending in `/` | 0 | |
| 28 | `U -T mail.txt /INBOX` | `APPEND=NO [TOOBIG] ...` (and `NO [TRYCREATE] ...` for `/Nope`) sent before any `+` | `APPEND ...`, then `LOGOUT`: no literal | 25 | `curl: (25) Upload failed (at start/before it took off)` |
| 29 | as 26 | `APPEND=OK [APPENDUID 1790726400 3] APPEND completed` | as 26 | 0 | |
| 30 | `U -X 'CREATE x' /` | the recorder's `BAD` | `CREATE x`, `LOGOUT` | 21 | `curl: (21) Quote command returned error` |
| 31 | `U -X 'CREATE Archive' /` | `CREATE=OK CREATE completed` | `CREATE Archive` | 0 | |
| 32 | `U -X 'EXAMINE INBOX' /` | | `EXAMINE INBOX` | 0 | stdout: every untagged line (`* FLAGS`, `* 2 EXISTS`, ...) as sent |
| 33 | `U -X 'STORE 1 +FLAGS \Deleted' /INBOX` | `* 1 FETCH (FLAGS (\Deleted))` then `OK` | `SELECT INBOX`, then `STORE 1 +FLAGS \Deleted`: a URL naming a mailbox is selected first | 0 | stdout: `* 1 FETCH (FLAGS (\Deleted))` |
| 34 | `U -X EXPUNGE /INBOX` | `* 1 EXPUNGE` then `OK` | `SELECT INBOX`, `EXPUNGE` | 0 | stdout: `* 1 EXPUNGE` |
| 35 | `U -X 'FETCH 1 BODY[]' /INBOX` | | `SELECT INBOX`, `FETCH 1 BODY[]` | 0 | stdout: the `* 1 FETCH (BODY[] {100}` line, then the literal: a custom command's untagged data is printed as sent |
| 36 | `U -X 'LIST "" *' /`, `U -X 'STATUS INBOX (MESSAGES UIDNEXT)' /` | | the command as given | 0 | the untagged lines |
| 37 | `U -X 'STORE 1 +FLAGS (\Seen)' /` | `STORE=BAD No mailbox selected` | no `SELECT`; `STORE ...`, `LOGOUT` | 21 | as 30 |
| 38 | `-sS '/INBOX;UID=1'` (no `-u`) | | `CAPABILITY`, then `SELECT INBOX` at once: without `-u` curl never logs in | 0 | the literal |
| 39 | as 38 | `SELECT=NO Log in first`, and `SELECT=BAD ...` | `SELECT INBOX`, `LOGOUT` | 67 | `curl: (67) Select failed` |
| 40 | `-sS /` (no `-u`) | `LIST=BAD ...` | `LIST "" *`, `LOGOUT` | 21 | as 30 |
| 41 | `U '/INBOX;UID=1'` | `CAPABILITY` of `IMAP4rev1` alone | `LOGIN u p`: with no `AUTH=` curl logs in with `LOGIN` | 0 | the literal |
| 42 | `U '/INBOX;UID=1'` | `CAPABILITY IMAP4rev1 STARTTLS LOGINDISABLED` (no `AUTH=`) | nothing after `CAPABILITY` | 67 | `curl: (67) Login denied` |
| 43 | `U '/INBOX;UID=1'` | `AUTHENTICATE=NO [AUTHENTICATIONFAILED] Authentication failed` | `AUTHENTICATE PLAIN` | 67 | as 42 |
| 44 | `-sS -k --ssl-reqd -u u:p '/INBOX;UID=1'` | | `CAPABILITY`, `STARTTLS` (`OK`, TLS 1.2 handshake), `CAPABILITY` again, `AUTHENTICATE PLAIN`, `SELECT`, `UID FETCH` | 0 | the literal |
| 45 | as 44 | `STARTTLS=BAD TLS not available`, and `STARTTLS=NO ...` | `STARTTLS`, then hangs up | 64 | `curl: (64) STARTTLS denied` |
| 46 | as 44 with `--ssl` for `--ssl-reqd` | `STARTTLS=BAD ...` | `STARTTLS`, then `AUTHENTICATE PLAIN` ... in plaintext | 0 | the literal |
| 47 | as 44 | `CAPABILITY` without `STARTTLS` | `CAPABILITY`, then hangs up | 64 | `curl: (64) STARTTLS not available.` |
| 48 | `-sS -k -u u:p 'imaps://127.0.0.1:18143/INBOX;UID=1'`, `-Tls` | | TLS from the first byte, then as 4 | 0 | the literal |
| 49 | `U '/INBOX;UID=1'` | `GREETING=* BYE Too many connections` | nothing | 8 | `curl: (8) Got unexpected imap-server response` |
| 50 | `U '/INBOX;UID=1'` | `GREETING=* PREAUTH ready` | `CAPABILITY`, then `SELECT` with no login even with `-u` | 0 | the literal |

**The decided responses, measured.** Every response decision 1 to 4 pins for a session curl can
drive was then served to curl through `-ImapReply` and `-SaslChallenge`, and curl completed each
exchange:

| # | curl arguments | What was served | Exit |
| --- | --- | --- | --- |
| D1 | `U '/INBOX;UIDVALIDITY=1790726400/;UID=1'` | decision 2's greeting and plaintext `CAPABILITY` (`... STARTTLS LOGINDISABLED AUTH=CRAM-MD5`), a `CRAM-MD5` challenge, decision 5's `SELECT` data (`FLAGS`, `PERMANENTFLAGS`, `EXISTS`, `RECENT`, `UNSEEN`, `UIDVALIDITY`, `UIDNEXT`), decision 3's `LOGOUT` | 0: curl picked `CRAM-MD5`, the only mechanism offered, and fetched the literal |
| D2 | `U --login-options AUTH=PLAIN '/INBOX;UID=1'` | as D1 | 67 `Login denied`: `PLAIN` not offered over plaintext, nothing sent after `CAPABILITY` |
| D3 | `-sS -k -u u:p --login-options AUTH=PLAIN 'imaps://.../INBOX;UID=1'`, with and without `--sasl-ir` | decision 2's TLS `CAPABILITY` (`... AUTH=CRAM-MD5 AUTH=OAUTHBEARER AUTH=XOAUTH2 AUTH=PLAIN AUTH=LOGIN`, with `SASL-IR`), `OK AUTHENTICATE completed` | 0: `AUTHENTICATE PLAIN AHUAcA==` both times - **with `SASL-IR` advertised curl sends the initial response unasked** |
| D4 | `U -T mail.txt /INBOX` | a `CAPABILITY` with `ID MOVE APPENDLIMIT=104857600`, `OK [APPENDUID 1790726400 3] APPEND completed` | 0 |
| D5 | `U -X 'CREATE Archive' /`, `U -X NOOP /`, `U -X 'STATUS INBOX (MESSAGES UIDNEXT)' /` | decision 3's `OK` texts, `* STATUS INBOX (MESSAGES 2 UIDNEXT 3)` | 0 each; `STATUS`'s line on stdout |

What curl 8.21.0 sends of its own is `CAPABILITY`, `STARTTLS`, `LOGIN`, `AUTHENTICATE`, `LIST "<ref>"
*`, `SELECT`, `FETCH`/`UID FETCH` of one `BODY[<section>]<partial>` item, `SEARCH <query as
given>`, `APPEND <mailbox> (\Seen) {<n>}` and `LOGOUT`; it never sends `EXAMINE`, `BODY.PEEK`,
`NOOP`, `IDLE`, `ENABLE` or a non-synchronizing literal, never pipelines, and anything else reaches
the server only through `-X`, sent as given after a `SELECT` when the URL names a mailbox.

## Decision

### 1. The protocol version

**IMAP4rev1 (RFC 3501)**, with the extensions of decision 2; not IMAP4rev2 (RFC 9051).

**Why:** curl 8.21.0 speaks IMAP4rev1 - it reads `* SEARCH`, not rev2's `ESEARCH`, and never sends
`ENABLE`. Advertising `IMAP4rev2` beside `IMAP4rev1` obliges the server to switch behaviour for a
client that enables it (no `\Recent`, `ESEARCH`, `LIST-STATUS` and more), a second protocol no
upstream curl ever asks for; RFC 9051 section 1 lets a server offer rev1 alone. The rev2
extensions that cost nothing and help a `-X` user (`UIDPLUS`, `UNSELECT`, `NAMESPACE`,
`CHILDREN`, `SASL-IR`, `MOVE`, `ID`) are advertised individually, as rev1 servers do.

### 2. The greeting and the capabilities, per state

- **The greeting**: `* OK [CAPABILITY <list>] surl ready` - the capability list of the connection's
  current state (below), so a client need not ask; `surl`, no version, no host name (ADR-0006
  section 3). curl sends `CAPABILITY` anyway (every row). No `PREAUTH` greeting, ever: row 50 shows
  curl then skips its login, so `-u` would be ignored.
- **`CAPABILITY`** answers `* CAPABILITY <list>` then `<tag> OK CAPABILITY completed`, the list
  asked afresh each time, since it changes with TLS (RFC 3501 section 6.2.1) and with the login.
- **The list**, in this order, each item present only as said:

  | Item | Present |
  | --- | --- |
  | `IMAP4rev1` | always |
  | `SASL-IR` (RFC 4959) | always before login (ADR-0049 section 2) |
  | `UIDPLUS` (RFC 4315), `UNSELECT` (RFC 3691), `NAMESPACE` (RFC 2342), `CHILDREN` (RFC 3348), `ID` (RFC 2971), `MOVE` (RFC 6851) | always |
  | `APPENDLIMIT=<n>` (RFC 7889) | `<n>` is `ExchangeLimits.MaxUploadBytes` in decimal (`--max-filesize`); left out when it is 0 (no limit) |
  | `STARTTLS` (RFC 3501 section 6.2.1) | only on a plaintext connection whose server can upgrade (decision 11), before login |
  | `LOGINDISABLED` (RFC 3501 section 6.2.3) | before login, when `IMailAuthenticationPolicy.GetMailLoginOffer(connection.TlsSession).IsClearPasswordLoginOffered` is false (ADR-0049 section 2) |
  | `AUTH=<m>` for each mechanism | before login, `GetMailLoginOffer(...).SaslMechanisms` in the order given, asked afresh each time |

  After a login the list is `IMAP4rev1 UIDPLUS UNSELECT NAMESPACE CHILDREN ID MOVE
  APPENDLIMIT=<n>`: `SASL-IR`, `STARTTLS`, `LOGINDISABLED` and `AUTH=` describe commands no longer
  allowed (RFC 3501 section 6.2).

  With the default `--auth` set and `--max-filesize` that is, over plaintext with a certificate
  configured, `IMAP4rev1 SASL-IR UIDPLUS UNSELECT NAMESPACE CHILDREN ID MOVE APPENDLIMIT=104857600
  STARTTLS LOGINDISABLED AUTH=CRAM-MD5` (D1: `curl -u` logs in with `CRAM-MD5`), and over TLS
  `IMAP4rev1 SASL-IR UIDPLUS UNSELECT NAMESPACE CHILDREN ID MOVE APPENDLIMIT=104857600 AUTH=CRAM-MD5
  AUTH=OAUTHBEARER AUTH=XOAUTH2 AUTH=PLAIN AUTH=LOGIN` (D3).
- `IDLE`, `ENABLE`, `CONDSTORE`, `QRESYNC`, `ESEARCH`, `SORT`, `THREAD`, `QUOTA`, `ACL`,
  `METADATA`, `NOTIFY`, `COMPRESS`, `SPECIAL-USE`, `LIST-EXTENDED` and `LITERAL+` are not advertised
  and their commands are answered as decision 3's unknown command. `IDLE` is left out because its
  end is the client's `DONE`, and curl's `-X` sends one line and waits for the tagged response, so
  no upstream curl could complete one; the rest are extensions curl never uses, and each would widen
  what a peer may make the server keep or compute.

### 3. Commands, states and the response table

Every response is a CRLF-terminated line written through ADR-0050 decision 8's reply-line writer,
so no byte a peer sent reaches a response unescaped except as decision 6 and 7 say (mailbox names
and message data, which are the peer's own data sent back in IMAP's quoting). A tagged response
starts with the tag the command carried. Status texts are fixed text from this ADR (ADR-0006
section 3). A command name is matched without regard to case; arguments are parsed by RFC 3501
section 9's grammar (atoms, quoted strings, literals, lists, sequence sets).

**States** (RFC 3501 section 3): not authenticated, authenticated, selected, logout. A **tag** is
one or more `ASTRING-CHAR`s other than `+`; a line whose tag cannot be read (empty, or starting with
a space or a character outside `ASTRING-CHAR`) is answered `* BAD Invalid tag` and the session goes
on.

| Command | State | Response (tagged unless it starts `*`) |
| --- | --- | --- |
| `CAPABILITY` | any | decision 2 |
| `NOOP` | any | decision 5's pending updates when selected, then `OK NOOP completed` |
| `LOGOUT` | any | `* BYE surl logging out`, then `OK LOGOUT completed`, then close (`CompleteWritesAsync`) |
| `ID <list or NIL>` | any | `* ID NIL`, then `OK ID completed`: the server names nothing (ADR-0006 section 3) and ignores the client's list |
| `STARTTLS` | not authenticated | decision 11 |
| `AUTHENTICATE <mechanism> [<initial response>]` | not authenticated | ADR-0049 section 7's words; success `OK AUTHENTICATE completed` |
| `LOGIN <user> <password>` | not authenticated | ADR-0049 section 7: `OK LOGIN completed`, `NO [AUTHENTICATIONFAILED] Authentication failed`, `NO [PRIVACYREQUIRED] Encryption required` |
| `STARTTLS`, `AUTHENTICATE`, `LOGIN` after a login | | `BAD Already authenticated` |
| `SELECT`, `EXAMINE` | authenticated, selected | decision 5 |
| `CREATE`, `DELETE`, `RENAME`, `SUBSCRIBE`, `UNSUBSCRIBE`, `LIST`, `LSUB`, `STATUS`, `NAMESPACE`, `APPEND` | authenticated, selected | decision 6, decision 8 |
| `CHECK` | selected | pending updates, then `OK CHECK completed` |
| `CLOSE` | selected | read-write: the `\Deleted` messages expunged with no untagged `EXPUNGE` (RFC 3501 section 6.4.2); then `OK CLOSE completed`, state authenticated |
| `UNSELECT` | selected | `OK UNSELECT completed`, nothing expunged, state authenticated |
| `EXPUNGE`, `UID EXPUNGE <set>` | selected | decision 7 |
| `SEARCH`, `FETCH`, `STORE`, `COPY`, `MOVE` and their `UID` forms | selected | decisions 4 and 7 |
| a selected-state command in the authenticated state | | `BAD No mailbox selected` (curl exits 21, row 37) |
| an authenticated-state command before a login | | decision 10 |
| anything else, and every command decision 2 does not advertise | any | `BAD Command not recognized` |
| a known command whose arguments do not parse | any | `BAD Invalid arguments` |

- **`EXAMINE`, `SELECT` on a read-only view**: every command that would change the mailbox
  (`STORE`, `EXPUNGE`, `UID EXPUNGE`, `MOVE`, and `CLOSE`'s expunge, which is skipped) answers
  `NO [READ-ONLY] Mailbox is read-only`, except `CLOSE`, which completes without expunging.
- **A failed `SELECT` or `EXAMINE`** leaves the authenticated state (RFC 3501 section 6.3.1): the
  previously selected mailbox is closed without expunging.
- **The store's typed outcomes** (ADR-0050 decision 6) in IMAP's words, the session going on:

  | Outcome | Response |
  | --- | --- |
  | No such mailbox (`SELECT`, `EXAMINE`, `STATUS`, `DELETE`, `RENAME`'s source, `SUBSCRIBE`) | `NO [NONEXISTENT] Mailbox does not exist` (RFC 5530) |
  | No such mailbox (`APPEND`, `COPY`, `MOVE` target) | `NO [TRYCREATE] Mailbox does not exist` (RFC 3501 section 6.3.11) |
  | Already exists (`CREATE`, `RENAME`'s target) | `NO [ALREADYEXISTS] Mailbox already exists` |
  | `DELETE INBOX` | `NO [CANNOT] INBOX cannot be deleted` |
  | An invalid name (decision 6) | `NO [CANNOT] Invalid mailbox name` |
  | `StoreFull` (`APPEND`, `COPY`, `MOVE`) | `NO [OVERQUOTA] The mail store is full` |
  | `TooManyMailboxes` (`CREATE`, `RENAME`) | `NO [LIMIT] Too many mailboxes` |
  | `StorageFailed` | `NO [SERVERBUG] Could not store the change` (the exception in a note, decision 13) |

- Success texts are `OK <COMMAND> completed` with the command name in capitals (`OK CREATE
  completed`, `OK UID FETCH completed` becomes `OK FETCH completed`: the `UID` forms name the
  command after `UID`), except where this ADR gives a response code.

### 4. `FETCH` and `UID FETCH`

- **Data items**: every RFC 3501 section 6.4.5 item: `ALL`, `FAST`, `FULL` (macros), `BODY`,
  `BODYSTRUCTURE`, `ENVELOPE`, `FLAGS`, `INTERNALDATE`, `RFC822`, `RFC822.HEADER`, `RFC822.SIZE`,
  `RFC822.TEXT`, `UID`, and `BODY[<section>]<<partial>>` and `BODY.PEEK[<section>]<<partial>>` with
  every section form: empty, `HEADER`, `HEADER.FIELDS (<names>)`, `HEADER.FIELDS.NOT (<names>)`,
  `TEXT`, `MIME`, and part numbers (`1`, `1.2`, `1.2.TEXT`, ...), computed from the stored bytes by
  RFC 5322 and RFC 2045/2046 structure. The MIME parser lives in `Surl.Protocol.Imap` (BL-202); the
  store keeps bytes only. A part that does not exist is the empty string (`{0}`), as RFC 3501
  section 6.4.5 allows; a malformed MIME structure is read as a single `text/plain` part.
- **The untagged response** is `* <sequence number> FETCH (<items>)`, one per message in the set in
  ascending order, with the items in this order: `UID <n>` (always for `UID FETCH`, RFC 3501
  section 6.4.8, and when asked), then `FLAGS (...)` (when asked, or when a non-peek body fetch
  set `\Seen`, RFC 3501 section 6.4.5), then the other items in the order asked. Measured, curl
  reads the body whatever comes before or after it (row 15).
- **Every message datum is a literal** - `BODY[...]`, `RFC822`, `RFC822.HEADER`, `RFC822.TEXT` -
  `<name> {<n>}` CRLF then the n bytes, even when empty or short: a quoted string is exit 8 for curl
  (row 16). The item name echoes the section as the client wrote it, with the header field names
  in capitals as RFC 3501 section 7.4.2 shows, and a partial as `<origin>` only (`BODY[]<0> {10}`,
  row 9). A `.PEEK` is never echoed. `ENVELOPE` and `BODYSTRUCTURE` strings are quoted strings
  (with `\` before `"` and `\`) when they hold only 7-bit bytes and no CR or LF, else literals;
  absent values are `NIL`.
- **`\Seen`**: a `BODY[...]`, `RFC822` or `RFC822.TEXT` fetch (not `.PEEK`, not `RFC822.HEADER`)
  sets `\Seen` in a read-write mailbox (curl's `UID FETCH ... BODY[]` marks a message read, as it
  does against every IMAP server); not under `EXAMINE`.
- **`INTERNALDATE`** is `"dd-MMM-yyyy HH:mm:ss +hhmm"` in the invariant culture, from the store's
  internal date with its offset (ADR-0050 decision 3). **`RFC822.SIZE`** is the stored byte count.
- **Sequence sets**: numbers, `n:m` in either order, `*` (the highest number or UID), and comma
  lists. For `FETCH`, a number past `EXISTS` makes the whole command `BAD Invalid message sequence
  number` (RFC 3501 section 9, `seq-number`); curl exits 78 (row 18). For `UID FETCH`, UIDs that
  name no message are ignored (RFC 3501 section 6.4.8), and a set naming none answers `OK` with no
  data; curl exits 78, `Remote file not found` (row 17).
- **A message expunged by another session** since this one last saw the mailbox is still
  numbered in this session's view until decision 5's updates are sent; fetching it answers its
  `FETCH` with the items it still has in the view (flags, UID) and the empty string for its body,
  and a note (decision 13).

### 5. `SELECT`, `EXAMINE`, `STATUS` and the session view

- **`SELECT <mailbox>`** answers, in this order (measured D1):

  ```
  * FLAGS (\Answered \Flagged \Deleted \Seen \Draft)
  * OK [PERMANENTFLAGS (\Answered \Flagged \Deleted \Seen \Draft)] Flags permitted
  * <n> EXISTS
  * 0 RECENT
  * OK [UNSEEN <first unseen number>] First unseen      (only when a message lacks \Seen)
  * OK [UIDVALIDITY <v>] UIDs valid
  * OK [UIDNEXT <u>] Predicted next UID
  <tag> OK [READ-WRITE] SELECT completed
  ```

  `EXAMINE` is the same with `* OK [PERMANENTFLAGS ()] No permanent flags permitted` and `<tag> OK
  [READ-ONLY] EXAMINE completed` (RFC 3501 section 6.3.2).
- **Flags**: the store's five system flags (ADR-0050 decision 3), no `\*` - keywords are not kept.
  **`\Recent`** is never set: the store does not keep it, so `RECENT` is always 0, the `RECENT` and
  `NEW` search keys match nothing and `OLD` matches everything (RFC 3501 section 2.3.2 lets a
  server report none). A `STORE` or `APPEND` naming a keyword applies its system flags and ignores
  the keyword, answering as if it were stored nowhere, since `PERMANENTFLAGS` never listed it (RFC
  3501 section 6.4.6); `\Recent` in a flag list is `BAD Invalid arguments` (the grammar's `flag`
  excludes it).
- **The session view**: selecting fixes a list of UIDs numbered 1 upward. Before the tagged
  response of `NOOP`, `CHECK` and every other command except `FETCH`, `STORE`, `SEARCH`, `COPY`,
  `MOVE` and their `UID` forms (RFC 3501 section 7.4.1: no `EXPUNGE` then), the server sends `* <n>
  EXPUNGE` for each message another session expunged (highest number first, renumbering the view)
  and `* <n> EXISTS` when messages were added. Flag changes made by other sessions are not pushed;
  a client sees them on its next `FETCH FLAGS`.
- **`STATUS <mailbox> (<items>)`**: `MESSAGES`, `RECENT` (0), `UIDNEXT`, `UIDVALIDITY` and
  `UNSEEN`, answered `* STATUS <mailbox> (<item> <n> ...)` in the order asked, then `OK STATUS
  completed`. An unknown item is `BAD Invalid arguments`.

### 6. Mailbox names, `LIST` and `LSUB`

- **The hierarchy delimiter is `/`** (measured: curl passes `/` inside a URL path through as it is,
  row 23), and the one personal namespace is `""`: `NAMESPACE` answers `* NAMESPACE (("" "/")) NIL
  NIL`. The store keeps names flat (ADR-0050 decision 3); the hierarchy is only the reading of `/`
  in them.
- **On the wire**, a name is an atom, a quoted string or a literal (RFC 3501 section 9). It is
  decoded from modified UTF-7 (RFC 3501 section 5.1.3), and bytes of 0x80 and above are read as
  UTF-8, since curl sends a URL's UTF-8 bytes as they are (row 24); a name that is neither (a bad
  `&...-` run, or bytes that are not UTF-8) is `NO [CANNOT] Invalid mailbox name`, as is one that
  ADR-0050 decision 3 refuses, one with an empty level (`a//b`, a leading or trailing `/`) or one
  holding `*` or `%`. Names are written back in modified UTF-7, as a quoted string when they are
  not atoms. `INBOX` in any case is the `INBOX` (row 23's `inbox`), written `INBOX`.
- **`LIST <reference> <pattern>`**: the reference and the pattern are joined as strings (RFC 3501
  section 6.3.8's usual reading), and the result matched against every name, `*` matching any
  run of characters and `%` any run without `/`. Each match is `* LIST (<attributes>) "/"
  <name>`, `INBOX` first and then the rest in ordinal order, then `OK LIST completed`.
  Attributes: `\HasChildren` or `\HasNoChildren` (RFC 3348); a parent level that is not itself a
  mailbox but has one below it and matches (`a` when only `a/b` exists, found by `%`) is listed
  `\Noselect \HasChildren`. `LIST "" ""` answers `* LIST (\Noselect) "/" ""` (RFC 3501 section
  6.3.8). curl's own `LIST "INBOX" *` (row 2) so lists `INBOX` and every name that starts `INBOX`.
- **Subscriptions are not kept.** Every mailbox counts as subscribed: `LSUB` answers as `LIST`
  with `* LSUB` lines (no `\Noselect` placeholders), `SUBSCRIBE` of an existing mailbox and
  `UNSUBSCRIBE` of any name answer `OK` and change nothing. **Why:** the store has no subscription
  list (ADR-0050 decision 3), RFC 3501 section 6.3.9 allows a server to treat mailboxes as
  subscribed, and curl never subscribes.
- **`CREATE <name>`**: creates the name (a trailing `/` is dropped, RFC 3501 section 6.3.3);
  creating `INBOX` is `NO [ALREADYEXISTS]`. Parent levels are not created: they are listed as
  `\Noselect` placeholders instead. **`DELETE`** removes the mailbox and its messages; a mailbox
  with children below it is deleted like any other (the children stay). **`RENAME`** as ADR-0050
  decision 3, `INBOX` included; children are not renamed with it (the store's names are flat).

### 7. `STORE`, `COPY`, `MOVE`, `EXPUNGE` and `SEARCH`

- **`STORE <set> [+|-]FLAGS[.SILENT] <flags>`**: sets, adds or removes system flags; unless
  `.SILENT`, each changed message's new flags are sent as `* <n> FETCH (FLAGS (...))` (with `UID
  <u>` first for `UID STORE`), then `OK STORE completed`. Measured, curl prints the untagged
  line (row 33).
- **`COPY <set> <mailbox>`**: copies in ascending UID order, all or none (ADR-0050 decision 3);
  answers `OK [COPYUID <uidvalidity> <source UIDs> <new UIDs>] COPY completed` (RFC 4315) when at
  least one message was copied, else `OK COPY completed`.
- **`MOVE <set> <mailbox>`** (RFC 6851): a `COPY`, then the sources expunged: `* OK [COPYUID ...]
  Moved`, one `* <n> EXPUNGE` per source, then `OK MOVE completed`.
- **`EXPUNGE`**: one `* <n> EXPUNGE` per removed message, in descending order of number so each
  number is right when read, then `OK EXPUNGE completed`. **`UID EXPUNGE <set>`** (RFC 4315)
  removes only the `\Deleted` messages in the set.
- **`SEARCH [CHARSET <charset>] <keys>`**: every RFC 3501 section 6.4.4 key - `ALL`, `ANSWERED`,
  `BCC`, `BEFORE`, `BODY`, `CC`, `DELETED`, `DRAFT`, `FLAGGED`, `FROM`, `HEADER`, `KEYWORD`,
  `LARGER`, `NEW`, `NOT`, `OLD`, `ON`, `OR`, `RECENT`, `SEEN`, `SENTBEFORE`, `SENTON`, `SENTSINCE`,
  `SINCE`, `SMALLER`, `SUBJECT`, `TEXT`, `TO`, `UID`, `UNANSWERED`, `UNDELETED`, `UNDRAFT`,
  `UNFLAGGED`, `UNKEYWORD`, `UNSEEN`, a sequence set, and parenthesised groups.
  - String keys match as a substring, comparing without regard to case (`OrdinalIgnoreCase`) after
    reading both as UTF-8 (invalid bytes replaced): header keys against the unfolded field values
    as stored (no RFC 2047 decoding), `BODY` against the text part's bytes, `TEXT` against the
    whole message. No transfer decoding is done; RFC 3501 does not require it.
  - `KEYWORD` matches nothing and `UNKEYWORD` everything (no keywords are kept).
  - Date keys compare calendar dates: `BEFORE`/`ON`/`SINCE` the internal date in its own offset,
    `SENT*` the `Date:` field's date (RFC 5322); a message whose `Date:` does not parse matches no
    `SENT*` key.
  - `CHARSET` `US-ASCII` or `UTF-8` (any case) is accepted; another is `NO [BADCHARSET (US-ASCII
    UTF-8)] Unsupported charset`. An unknown key is `BAD Invalid arguments`.
  - The answer is `* SEARCH` followed by the matching numbers (UIDs for `UID SEARCH`) in ascending
    order, `* SEARCH` alone when none match, then `OK SEARCH completed`. curl prints the line
    (row 25).
  - Searching does not set `\Seen`.

### 8. `APPEND`

`APPEND <mailbox> [(<flags>)] [<date-time>] <literal>`, curl's `-T` (row 26).

1. Before the `+` continuation, in order: the session must be logged in (decision 10); the name
   valid and the mailbox existing, else `NO [TRYCREATE] Mailbox does not exist`; the literal's
   length at most `MaxUploadBytes` (when not 0), else `NO [TOOBIG] Message exceeds the size limit`
   (RFC 7889 section 4). A refusal here is sent instead of `+`, so the client sends no message bytes
   and the session goes on (curl exits 25, row 28). `--allow-uploads` does not gate it (ADR-0050
   decision 5). The flags and date-time are parsed first; a bad one is `BAD Invalid arguments`.
2. `+ Ready for literal data`, then the n bytes are streamed into the store's pending file and the
   rest of the command line (normally just CRLF) is read.
3. The message is appended with the given system flags (curl's `\Seen`; keywords ignored as decision
   5 says) and the given date-time as its internal date, else the clock's: `OK [APPENDUID
   <uidvalidity> <uid>] APPEND completed` (RFC 4315; measured D4). `StoreFull`: `NO [OVERQUOTA] The
   mail store is full`; `StorageFailed`: `NO [SERVERBUG] Could not store the change`. When the
   mailbox is the selected one, the new message is reported by decision 5's `EXISTS`.
4. A peer that closes inside the literal stores nothing.

The message is stored as the bytes sent: no trace fields are added (a client putting its own
message in its own mailbox is not a delivery, RFC 5321 section 4.4).

### 9. Literals, lines and the command bounds

- **Synchronizing literals** (`{n}` at the end of a line) are the only kind accepted. Before the
  `+ Ready for literal data` continuation the server checks the literal: in `APPEND`'s message
  position as decision 8 says; anywhere else (a name, a string, a password) its length plus the
  bytes of the command read so far must not pass `--max-line` (ADR-0006, 8192 bytes by default),
  else `<tag> BAD Literal too long` instead of `+`, and the session goes on (RFC 3501 section 7.5:
  the client abandons the command).
- **A non-synchronizing literal** (`{n+}`, RFC 7888), which was never advertised, is answered
  `<tag> BAD Non-synchronizing literals are not supported` and the connection is closed: its bytes
  are already on the way and the command's end cannot be found without reading them.
- **The whole command** - every line and every literal but `APPEND`'s message - is bounded by
  `--max-line` together; each line ends only at CRLF (ADR-0050 decision 8), and the head timeout
  (`--head-timeout`) bounds the time to read one command, literals included except `APPEND`'s
  message, which the idle timeout bounds.

### 10. Logins, and what a login unlocks

- `AUTHENTICATE` and `LOGIN` run ADR-0049's exchanges through `IMailAuthenticationPolicy` and
  `CheckPasswordLoginAsync` and answer in ADR-0049 section 7's IMAP words. `LOGIN` while
  `LOGINDISABLED` is advertised is `NO [PRIVACYREQUIRED] Encryption required`; curl never sends it
  then (row 42), and exits 67. `AUTHENTICATE` with `SASL-IR` takes the initial response on the
  command line (D3: curl sends one unasked when `SASL-IR` is advertised).
- **An authenticated-state command before any login** (curl without `-u`, rows 38 to 40) asks the
  policy once with `CheckPasswordLoginAsync(new PasswordLogin(scheme, null, null,
  connection.TlsSession))`, as SMTP's first `MAIL` does (ADR-0053 decision 3), and keeps the
  verdict for the session:
  - `AcceptedUnchecked` (`--allow-anonymous`): the session is logged in as ADR-0050 decision 2's
    anonymous owner, with no login note, and the command proceeds; `curl imap://.../INBOX;UID=1`
    with no `-u` then works;
  - anything else: `NO [AUTHENTICATIONFAILED] Authentication required`, and the session stays not
    authenticated (curl exits 67 for a `SELECT`, 21 for a `LIST`, rows 39 and 40).
  **Why:** secure by default (ADR-0032) - nobody reads mail without an account unless the operator
  loosens it - while curl's natural no-login use still works under `--allow-anonymous`. A
  `PREAUTH` greeting under `--allow-anonymous` was rejected: curl would then ignore `-u` (row 50).
- A login lasts until the connection ends; `LOGOUT` ends it.

### 11. `STARTTLS` and `imaps://`

- **Whether the server can upgrade** is given to `ImapProtocolServer`'s constructor as a `bool`,
  which `Surl.Console` sets when a server certificate is configured (`--cert`, or `--self-signed`,
  ADR-0032 section 10), as ADR-0053 decision 5 does for SMTP.
- **`STARTTLS`** on a plaintext connection that can upgrade, before login: `<tag> OK Begin TLS
  negotiation now`, then ADR-0050 decision 8's discard (every byte buffered after the `STARTTLS`
  line thrown away and its count noted, ADR-0010 section 1), then `IConnection.UpgradeToTlsAsync`.
  The session stays not authenticated and curl asks `CAPABILITY` again (row 44), which decision 2
  answers for TLS.
- **No certificate**: `<tag> BAD STARTTLS not available` (it was never advertised, so it is an
  unknown command to this server, RFC 3501 section 6.2.1). curl exits 64 with `--ssl-reqd` (rows 45
  and 47) and goes on in plaintext with `--ssl` (row 46) - ADR-0032 section 10's "refused in its own
  words".
- **Already TLS** (after `STARTTLS`, or `imaps://`): `<tag> BAD Already using TLS`. After a login:
  decision 3's `BAD Already authenticated`.
- **A failed handshake** ends the connection with the engine's `TLS handshake failed: ` note; nothing
  is written after it.
- **`imaps://`** is implicit TLS: `ImapProtocolServer` claims the scheme `imap` only, and
  `Surl.Console` registers it for `imaps` through `ImplicitTlsSchemeServer`, as SMTP's `smtps` (BL-208).
  The server tells the two apart by `connection.TlsSession`, never by the scheme. Default ports are
  curl's: 143 for `imap`, 993 for `imaps`.

### 12. Limits (ADR-0006 section 5, IMAP column)

| Limit | IMAP answer | curl (measured) |
| --- | --- | --- |
| Too many connections (`IConnectionRefusalWriter`, which the server implements) | `* BYE surl Too many connections, closing` in place of the greeting, then close | 8, row 49 |
| Head timeout (a command not finished within `--head-timeout`) | `* BYE surl Timeout waiting for a command, closing`, then close | 78 when it answers a fetch, row 22 |
| A line past `--max-line`, or a whole command past it (decision 9) | `<tag> BAD Command line too long` when the tag was read, else `* BYE surl Command line too long, closing`; then close | 67 when it answers `SELECT`, row 20 |
| A literal past the bound | decision 9's `BAD Literal too long`, the session going on | as a `BAD` |
| An `APPEND` past `--max-filesize` | decision 8's `NO [TOOBIG]`, nothing stored, the session going on | 25, row 28 |
| Idle timeout, maximum duration | `* BYE surl Timeout, closing`, then close | as the head timeout |
| The store full or a bound reached | decision 3's `NO [OVERQUOTA]`, `NO [LIMIT]` | 25 for `APPEND`, 21 for `-X` |

The session also ends with `* BYE` and a close when curl's `LOGOUT` is answered. **Why the `APPEND`
past the limit does not close**: the refusal comes before the `+` continuation, so no message byte
is sent and the command's end is known, unlike SMTP's `DATA`.

### 13. The verbose notes (ADR-0033, ADR-0038)

Written with `IExchangeLog.Note` at `verbose` and above, every peer byte escaped as ADR-0006
section 3 says.

| When | Note |
| --- | --- |
| A message appended | `Message appended to <mailbox>: <n> bytes` |
| `APPEND` refused past the limit | `APPEND refused: <n> bytes is past --max-filesize` |
| The store full | `Message refused: the mail store is full` |
| A change that could not be written | `Mail store: <exception message>` (ADR-0050 decision 7) |
| A fetch of a message another session expunged | `Message <uid> in <mailbox> was expunged by another session` |
| Bytes discarded after `STARTTLS` | `Discarded <n> bytes sent after STARTTLS` (only when `<n>` is not 0) |
| A command refused for want of a login | `<COMMAND> refused: log in first, or give --allow-anonymous` |
| A checked `LOGIN` or `AUTHENTICATE` | `CheckedLogin.Note` from the step, before the response (ADR-0038, ADR-0049 section 6) |

### 14. Help

The `imap` category, `IMAP and IMAPS protocol` (ADR-0034 decision 1's wording), claims the schemes
`imap` and `imaps`, and holds every option the IMAP server reads: the limits (`--max-filesize`,
`--max-line`, `--head-timeout`), the login options (`--user`, `--user-file`, `--allow-anonymous`,
`--allow-plaintext-auth`, `--auth`) and `--directory` (where the mail store persists, ADR-0050
decision 7). BL-208 adds the category, its `--aihelp` topic, prose and example, and grows the pinned
topic lists, as root `CLAUDE.md` requires.

### 15. What BL-211 proves with the pinned build

Each against `surl` over loopback through the conformance harness, with the options named; `-k`
wherever TLS is used with `--self-signed`. The mail store is seeded by an SMTP delivery of
`mail.txt` (`From: a@x`, `Subject: hi`, an empty line, `hello`, CRLF line ends) to `tester` (BL-210's
case) before each row, every row against a fresh `surl` and store, the rows with two commands running them in turn against the same one, so `INBOX` holds one message, UID 1, stored as ADR-0053
decision 6's two trace fields followed by `mail.txt`'s bytes (call those bytes `S`). `A` is `-u
tester:secret`. Exits and stdout:

| surl options | curl command line | Exit | stdout |
| --- | --- | --- | --- |
| `--user tester:secret` | `curl -sS A imap://127.0.0.1:<p>/` | 0 | `* LIST (\HasNoChildren) "/" INBOX` CRLF |
| `--user tester:secret` | `curl -sS A 'imap://.../INBOX;UID=1'` | 0 | `S` exactly (`CRAM-MD5` login) |
| `--user tester:secret` | `curl -sS A 'imap://.../INBOX;MAILINDEX=1'` | 0 | `S` |
| `--user tester:secret` | `curl -sS A 'imap://.../INBOX;UID=1;SECTION=TEXT'` | 0 | `hello` CRLF |
| `--user tester:secret` | `curl -sS A 'imap://.../INBOX;UID=1;SECTION=HEADER.FIELDS%20(SUBJECT)'` | 0 | `Subject: hi` CRLF CRLF |
| `--user tester:secret` | `curl -sS A 'imap://.../INBOX;UID=1;PARTIAL=0.10'` | 0 | the first 10 bytes of `S` |
| `--user tester:secret` | `curl -sS A 'imap://.../INBOX;UIDVALIDITY=1/;UID=1'` | 78 | (`Mailbox UIDVALIDITY has changed`: the real `UIDVALIDITY` is the clock's) |
| `--user tester:secret` | `curl -sS A 'imap://.../INBOX;UID=9'` | 78 | |
| `--user tester:secret` | `curl -sS A 'imap://.../Nope;UID=1'` | 67 | |
| `--user tester:secret` | `curl -sS A 'imap://.../INBOX?SUBJECT%20hi'` | 0 | `* SEARCH 1` CRLF |
| `--user tester:secret` | `curl -sS A 'imap://.../INBOX?SUBJECT%20nothing'` | 0 | `* SEARCH` CRLF |
| `--user tester:secret` | `curl -sS A -T mail.txt imap://.../INBOX`, then `curl -sS A 'imap://.../INBOX;UID=2'` | 0, 0 | nothing; then `mail.txt`'s bytes exactly (no trace fields) |
| `--user tester:secret --max-filesize 10` | `curl -sS A -T mail.txt imap://.../INBOX` | 25 | |
| `--user tester:secret` | `curl -sS A -T mail.txt imap://.../Nope` | 25 | |
| `--user tester:secret` | `curl -sS A -X 'CREATE Archive' imap://.../`, then `curl -sS A imap://.../` | 0, 0 | nothing; then `INBOX` and `Archive` `LIST` lines |
| `--user tester:secret` | `curl -sS A -X 'EXAMINE INBOX' imap://.../` | 0 | decision 5's `EXAMINE` untagged lines |
| `--user tester:secret` | `curl -sS A -X 'STORE 1 +FLAGS \Deleted' imap://.../INBOX` | 0 | `* 1 FETCH (FLAGS (\Deleted))` CRLF |
| `--user tester:secret` | `curl -sS A -X 'STORE 1 +FLAGS \Deleted' imap://.../INBOX`, then `curl -sS A -X EXPUNGE imap://.../INBOX` | 0, 0 | the `STORE` line; then `* 1 EXPUNGE` CRLF |
| `--user tester:secret` | `curl -sS A -X 'STATUS INBOX (MESSAGES)' imap://.../` | 0 | `* STATUS INBOX (MESSAGES 1)` CRLF |
| `--user tester:secret` | `curl -sS A -X 'STORE 1 +FLAGS (\Seen)' imap://.../` | 21 | |
| `--user tester:secret` | `curl -sS A -X 'XYZZY' imap://.../` | 21 | |
| `--user tester:secret` | `curl -sS -u tester:wrong 'imap://.../INBOX;UID=1'` | 67 | |
| `--user tester:secret` | `curl -sS 'imap://.../INBOX;UID=1'` (no `-u`) | 67 | |
| `--allow-anonymous` (message delivered anonymously over SMTP) | `curl -sS 'imap://.../INBOX;UID=1'` | 0 | `S` |
| `--user tester:secret` | `curl -sS A --login-options AUTH=PLAIN 'imap://.../INBOX;UID=1'` (plaintext, `PLAIN` not offered) | 67 | |
| `--user tester:secret --allow-plaintext-auth` | the same, and `curl -sS A --login-options AUTH=LOGIN ...` | 0 | `S` |
| `--user tester:secret --self-signed` | `curl -sS -k --ssl-reqd A 'imap://.../INBOX;UID=1'` | 0 | `S` |
| `--user tester:secret` (no certificate) | `curl -sS --ssl-reqd A 'imap://.../INBOX;UID=1'` | 64 | |
| `--user tester:secret --self-signed` | `curl -sS -k A 'imaps://.../INBOX;UID=1'` | 0 | `S` |
| `--user tester:secret --self-signed` | `curl -sS -k A --login-options AUTH=<mech> 'imaps://.../INBOX;UID=1'` for `PLAIN` (with and without `--sasl-ir`), `LOGIN`, `CRAM-MD5`; `-u tester: --oauth2-bearer tok` with `--user :tok` for `XOAUTH2` and `OAUTHBEARER`; `--auth digest-md5` for `DIGEST-MD5`, `--auth ntlm` for `NTLM` | 0 each | `S` |
| `--user tester:secret --max-connections 1`, a second client holding the first | `curl -sS A 'imap://.../INBOX;UID=1'` | 8 | |

The rows are pinned from this ADR's measurements; where the Linux or macOS OpenSSL builds give
another exit, it is pinned per platform in its own test (root `CLAUDE.md`) and recorded against
this ADR.

### 16. Details of decisions 4 and 7 settled in BL-202

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30, in
BL-202, where decisions 4 and 7 left a detail open. None changes a byte curl 8.21.0 sends or reads
in the rows above; the fetch and search fixtures of BL-202 (`Surl.Protocol.Imap.UnitTests/Fixtures`)
were served to the pinned build and it exited 0 on each.

- **Quoted or literal.** An `ENVELOPE` or `BODYSTRUCTURE` string is quoted only when every byte is
  printable ASCII (0x20 to 0x7E); anything else, a tab included, is a literal. **Why:** it is within
  decision 4's rule (a literal is always allowed) and keeps every byte outside a literal a byte the
  reply-line rule of ADR-0050 decision 8 allows.
- **Header and MIME reading.** A line ends at LF, with or without CR. The header ends at the first
  empty line and keeps it; a message with none is all header. A line starting with space or tab
  folds into the field before it; a line with no colon is a field with no name, which `HEADER.FIELDS`
  never names and `HEADER.FIELDS.NOT` keeps. `HEADER.FIELDS` output is each field's lines as stored,
  CRLF added after a last field that has no line end, then CRLF. Multipart and `message/rfc822`
  nesting is read 32 levels deep (`ImapBodyPart.MaxDepth`); below that a part is a leaf, and a
  multipart there is read as `text/plain`. **Why:** a message is the peer's data, so reading it must
  end whatever it holds.
- **Body structure values.** Type, subtype and parameter names are sent in capitals, parameter and
  disposition values as stored; a part with no `Content-Type` is `("TEXT" "PLAIN" ("CHARSET"
  "US-ASCII"))` (RFC 2045 section 5.2) and one with a `Content-Type` naming no charset has no
  `CHARSET`; the encoding is `Content-Transfer-Encoding` in capitals, else `7BIT`; lines are LFs
  plus one for bytes after the last LF. An address with no `@` has the empty host `""`, since
  `NIL` would make it a group's start (RFC 3501 section 7.4.2); comments are dropped, not used as
  names.
- **A message another session expunged** (decision 4) is answered with no flags, `RFC822.SIZE 0`,
  `INTERNALDATE "01-Jan-1970 00:00:00 +0000"` and empty message data, and never matches a search key.
- **A message whose bytes cannot be read** (its file removed or unreadable) ends the `FETCH` after
  the responses already sent, or answers the `SEARCH`, with `NO [SERVERBUG] Could not read the
  message` and the note `Mail store: <exception message>`; `\Seen` flags already set are saved.
- **`SEARCH` sequence sets** name message numbers; a number past the messages the session sees
  matches nothing rather than making the command `BAD` (unlike `FETCH`), since a search key is a
  filter. `SENT*` read the `Date:` field's first three words after an optional `<day>,` as day, month
  and year (two-digit years 00 to 49 are 20xx, others and three-digit years 19xx, RFC 5322 section
  4.3); anything else makes the message match no `SENT*` key.
- **`UID` with any command but `FETCH` and `SEARCH`** answered `BAD Command not recognized` until
  BL-203 built `UID STORE`, `UID COPY`, `UID MOVE` and `UID EXPUNGE` (decision 17); `UID` with any
  other command still does.

### 17. Details of decisions 6 to 8 settled in BL-203

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30, in
BL-203, where decisions 6 to 8 left a detail open. None changes a byte curl 8.21.0 sends or reads
in the rows above; the twelve `-T` and `-X` fixtures of BL-203 (`Surl.Protocol.Imap.UnitTests/Fixtures`)
were served to the pinned build and it exited 0 on each.

- **`APPEND`'s checks, in order**, before the `+`: the login (decision 10), the arguments (`BAD
  Invalid arguments`), the name (`NO [CANNOT] Invalid mailbox name`, decision 3's row, not
  `TRYCREATE`: creating it would not help), the mailbox (`NO [TRYCREATE]`), the size
  (`NO [TOOBIG]`). A literal is `APPEND`'s message when the command before it reads as a tag,
  `APPEND` and a mailbox; a literal before that is the mailbox, read as any literal is. The
  `date-time` is read strictly by RFC 3501's grammar: 26 characters, the day's first digit a space
  or a digit, an English month name, a zone of at most 14 hours either way, and a moment that in
  UTC still falls between the years 1 and 9999. **Why:** each refusal
  that can come before the `+` does, so the client sends no message byte for it.
- **After the message** the rest of the line must be empty; anything else (a second message, as
  the unadvertised `MULTIAPPEND` sends) is `BAD Invalid arguments` and nothing is stored. A message
  past the store's own `MaxMessageBytes`, found only once it is read, is `NO [TOOBIG]` with no note.
- **`STORE`** answers an untagged `FETCH` for each message whose flags the change altered, as
  decision 7 says, and none for one whose flags stayed the same or one another session expunged.
  A keyword, or a `\` flag that is not a system flag, is read and ignored; `\Recent` and `\*` are
  `BAD Invalid arguments`. `STORE`, `COPY` and `MOVE` take message numbers as `FETCH` does: one
  past `EXISTS` is `BAD Invalid message sequence number`.
- **`COPYUID`** writes each UID set with runs of consecutive UIDs as `n:m`, the sources in
  ascending order and the copies paired with them (RFC 4315). A `COPY` or `MOVE` that copies
  nothing (a UID set naming no message) answers plain `OK`, with no `COPYUID` and, for `MOVE`, no
  `* OK ... Moved` line.
- **`MOVE`** is one step in the store (`MailboxStore.Move`): all or none, the originals removed
  as the copies are made, and a store at its message bound can still move. Its `* <n> EXPUNGE`
  lines are for its own sources only, highest number first; expunges by other sessions wait for
  the next command that may send them (RFC 3501 section 7.4.1). `COPY` is allowed under `EXAMINE`,
  `MOVE` is not.
- **`UID EXPUNGE <set>`** removes, in one step in the store (`MailboxStore.Expunge` with UIDs),
  the `\Deleted` messages among the UIDs the set names that the session sees.
- **`CREATE`** also answers `NO [CANNOT] Invalid mailbox name` for a name the store refuses
  (ADR-0050 decision 3, e.g. past 1024 bytes). **`DELETE` or `RENAME` of the selected mailbox**
  leaves the session selected on a name that no longer exists: its next response that may carry
  updates reports every message expunged, and the session stays selected until `CLOSE`,
  `UNSELECT` or another `SELECT`.
- **`UNSUBSCRIBE`** of any `astring` is `OK`, even one that is not a mailbox name, since nothing is
  kept to refuse.

### 18. Details of decisions 2, 10 and 11 settled in BL-204

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30, in
BL-204, where decisions 2, 10 and 11 left a detail open. The fifteen `STARTTLS`, `imaps` and
`AUTHENTICATE` fixtures of BL-204 (`Surl.Protocol.Imap.UnitTests/Fixtures`, recorded with the
pinned win-x64 build, SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`)
were served to that build before any test pinned them.

- **Measured: curl 8.21.0 remembers `LOGINDISABLED` across `STARTTLS`.** Given `STARTTLS
  LOGINDISABLED` before TLS and, after it, a list with neither `LOGINDISABLED` nor any `AUTH=`,
  `curl -sS -k --ssl-reqd -u u:p imap://127.0.0.1:18343/` sends `CAPABILITY`, `STARTTLS`,
  `CAPABILITY`, and then nothing: `curl: (67) Login denied` (fixture
  `starttls-logindisabled-remembered`). The same exchange without `LOGINDISABLED` before TLS logs
  in with `LOGIN u p` and exits 0 (`starttls`), and with decision 2's default offer (`AUTH=CRAM-MD5`
  before TLS, the five mechanisms after it) curl logs in with `AUTHENTICATE CRAM-MD5` and exits 0
  (`starttls-authenticate`). **Decision 2 stands**: `LOGINDISABLED` is still advertised whenever
  the clear-password login is not offered on the connection. **Why:** leaving it out would make
  a curl without `--ssl-reqd` send the password in clear before the `NO [PRIVACYREQUIRED]`, which
  is what RFC 3501 section 6.2.3 and ADR-0032 section 4 exist to prevent. The cost is one
  configuration only: an `--auth` set with no mail word, without `--allow-plaintext-auth`, where
  `curl --ssl-reqd` over `imap://` exits 67; `imaps://`, or any SASL word in `--auth`, logs in.
- **The capability order** of decision 2's table holds for the new items: `STARTTLS`, then
  `LOGINDISABLED`, then the `AUTH=` items in the policy's order, all after `APPENDLIMIT`.
- **`STARTTLS`'s checks, in order**: an argument (`BAD Invalid arguments`); a connection already
  TLS (`BAD Already using TLS`); a server that cannot upgrade (`BAD STARTTLS not available`). A
  login comes first of all (`BAD Already authenticated`, decision 3). After the upgrade the
  session asks the policy again about the login with no credentials, since its verdict may
  depend on TLS (decision 10). A failed handshake throws the engine's `TlsHandshakeException`
  after the `OK`, and nothing more is written.
- **`AUTHENTICATE`'s arguments**: the mechanism is an atom, handed to the policy in capitals; the
  initial response is one word of printable ASCII after one space, `=` for an empty one (RFC
  4959). Anything else after the mechanism is `BAD Invalid arguments`; a word that is not base64,
  `*` included, is `BAD Cannot decode response` and starts no exchange.
- **Continuation lines** are read with the line reader's bounds: one past `--max-line` is decision
  12's `<tag> BAD Command line too long` and a close, one not complete within `--head-timeout` its
  `* BYE`, and the peer's close ends the session. A `CheckedLogin` carried on a challenge step (the
  bearer mechanisms' error challenge, ADR-0049 section 6) is written before the `+` line.
- **An accepted `AUTHENTICATE`** opens the view of the step's `AccountName` (the anonymous owner's
  with `--allow-anonymous`, as `LOGIN`'s does); `RefusedMechanism` is `NO Unsupported
  authentication mechanism` (ADR-0049 section 7), and the session stays not authenticated after
  every refusal.

## Alternatives considered

- **Advertise `IMAP4rev2` too.** Rejected in decision 1: curl never enables it, and it doubles the
  behaviour to build and test.
- **`PREAUTH` under `--allow-anonymous`.** Rejected in decision 10: curl then skips its login even
  with `-u` (row 50), so accounts could not be reached.
- **Refuse every command before a login with `BAD`, as RFC 3501's states suggest.** Rejected:
  curl's natural `imap://h/INBOX;UID=1` without `-u` would never work, even with
  `--allow-anonymous`; the implicit anonymous check is SMTP's rule (ADR-0053) in IMAP's words.
- **Send short bodies as quoted strings.** Rejected: curl exits 8 (row 16).
- **Close the connection on an `APPEND` past the limit**, as SMTP does after `DATA`. Rejected in
  decision 12: IMAP refuses before the literal is sent.
- **Keep subscriptions.** Rejected in decision 6: the store has none, curl never subscribes, and
  RFC 3501 allows every mailbox to count as subscribed.
- **Advertise `LITERAL+` or `LITERAL-`.** Rejected in decision 9: a non-synchronizing literal
  cannot be refused before its bytes arrive, curl never sends one, and synchronizing literals let
  every bound be checked first.
- **`IDLE`.** Rejected in decision 2: curl's `-X` cannot send its `DONE`.
- **Keep `\Recent`.** Rejected in decision 5: the store does not keep it (ADR-0050), and RFC 3501
  lets a server report none.
- **Hierarchy delimiter `.`.** Rejected in decision 6: curl passes `/` in a URL path through as
  it is (row 23), so `/` makes `imap://h/Archive/2026;UID=1` name the `2026` child of `Archive`.

## Consequences

- BL-201 builds decisions 1, 2 (without `STARTTLS` and `AUTH=`), 3, 5, 6, 9, 10's `LOGIN` and the
  implicit anonymous check, 12 and 13; BL-202 decision 4 and decision 7's `SEARCH`; BL-203
  decisions 7 and 8 and `CREATE`, `DELETE`, `RENAME`, `SUBSCRIBE`, `UNSUBSCRIBE` of decision 6;
  BL-204 decision 11 and decision 10's `AUTHENTICATE`, with decision 2's `STARTTLS` and `AUTH=`
  items; BL-208 decision 11's `imaps` registration, the constructor flag and decision 14; BL-211
  decision 15's table.
- The fixtures those tasks replay are recorded again from this ADR's rows with
  `Record-CurlExchange.ps1 -Imap` and the overrides named, and live under
  `Surl.Protocol.Imap.UnitTests/Fixtures/<case>/` with a `README.md` (build, SHA-256, command line,
  date).
- `Surl.Protocol.Imap` carries its own RFC 5322 and MIME structure parser for `ENVELOPE`,
  `BODYSTRUCTURE`, sections and `SEARCH`; the store stays bytes only.
- `Record-CurlExchange.ps1` is unchanged: its `-Imap` mode measured every case.
