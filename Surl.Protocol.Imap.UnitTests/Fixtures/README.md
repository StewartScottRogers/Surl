# IMAP fixtures

Exchanges recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1 -Imap` (BL-201). Never from the Curl port (ADR-0003).

Each case was fed, through `-ImapReply`, exactly the responses ADR-0055 decides and
`ImapProtocolServer` sends, and curl completed each with exit 0. `RecordedFixtureTests` replays
each `request.bin` against an `--allow-anonymous` store whose `INBOX` holds two unseen messages
(UIDs 1 and 2, `UIDVALIDITY` 1790668800, the store made at 2026-09-29 08:00 UTC) beside a mailbox
`Sent`, and asserts that the bytes Surl writes equal every `< ` line of `transcript.txt` with CRLF
after each.

Each folder holds the recorder's five files: `request.bin` (the bytes curl sent),
`transcript.txt` (both directions, `>` curl and `<` the recorder), `stdout.bin`, `stderr.txt`
and `exitcode.txt`. They are embedded resources of `Surl.Protocol.Imap.UnitTests`, so the tests
read them without touching the file system. `.gitattributes` here keeps git from rewriting
their CRLF line endings.

Recorded on 2026-09-30 from the repository root, in Windows PowerShell, on port 18143. The
capabilities are those of a plaintext connection whose policy offers the clear-password login
and no SASL mechanism (`AUTH=` and `STARTTLS` are BL-204's), so curl logs in with `LOGIN`
(ADR-0055 row 41). Every case was given these responses, the `LIST` response for `list-inbox`
being its `INBOX` line alone:

```powershell
$caps = 'IMAP4rev1 SASL-IR UIDPLUS UNSELECT NAMESPACE CHILDREN ID MOVE APPENDLIMIT=104857600'
$flags = '(\\Answered \\Flagged \\Deleted \\Seen \\Draft)'
$replies = @(
  "GREETING=* OK [CAPABILITY $caps] surl ready",
  "CAPABILITY=* CAPABILITY $caps\r\nOK CAPABILITY completed",
  'LOGIN=OK LOGIN completed',
  'LIST=* LIST (\\HasNoChildren) "/" INBOX\r\n* LIST (\\HasNoChildren) "/" Sent\r\nOK LIST completed',
  'LSUB=* LSUB (\\HasNoChildren) "/" INBOX\r\n* LSUB (\\HasNoChildren) "/" Sent\r\nOK LSUB completed',
  "SELECT=* FLAGS $flags\r\n* OK [PERMANENTFLAGS $flags] Flags permitted\r\n* 2 EXISTS\r\n* 0 RECENT\r\n* OK [UNSEEN 1] First unseen\r\n* OK [UIDVALIDITY 1790668800] UIDs valid\r\n* OK [UIDNEXT 3] Predicted next UID\r\nOK [READ-WRITE] SELECT completed",
  "EXAMINE=* FLAGS $flags\r\n* OK [PERMANENTFLAGS ()] No permanent flags permitted\r\n* 2 EXISTS\r\n* 0 RECENT\r\n* OK [UNSEEN 1] First unseen\r\n* OK [UIDVALIDITY 1790668800] UIDs valid\r\n* OK [UIDNEXT 3] Predicted next UID\r\nOK [READ-ONLY] EXAMINE completed",
  'STATUS=* STATUS INBOX (MESSAGES 2 UIDNEXT 3 UIDVALIDITY 1790668800 UNSEEN 2 RECENT 0)\r\nOK STATUS completed',
  'LOGOUT=* BYE surl logging out\r\nOK LOGOUT completed')
.\Record-CurlExchange.ps1 -Port 18143 -Imap -ImapReply $replies -CurlArgs <curl arguments> -OutDirectory Surl.Protocol.Imap.UnitTests\Fixtures\<folder>
```

Every URL starts `imap://127.0.0.1:18143`.

| Folder | curl arguments | curl sends after `CAPABILITY` and `LOGIN u p` |
| --- | --- | --- |
| `list-root` | `-sS -u u:p imap://127.0.0.1:18143/` | `LIST "" *` (ADR-0055 row 1) |
| `list-inbox` | `-sS -u u:p imap://127.0.0.1:18143/INBOX` | `LIST "INBOX" *` (row 2) |
| `lsub` | `-sS -u u:p -X 'LSUB "" *' imap://127.0.0.1:18143/` | `LSUB "" *` |
| `select` | `-sS -u u:p -X 'SELECT INBOX' imap://127.0.0.1:18143/` | `SELECT INBOX` |
| `examine` | `-sS -u u:p -X 'EXAMINE INBOX' imap://127.0.0.1:18143/` | `EXAMINE INBOX` (row 32) |
| `status` | `-sS -u u:p -X 'STATUS INBOX (MESSAGES UIDNEXT UIDVALIDITY UNSEEN RECENT)' imap://127.0.0.1:18143/` | the `STATUS` command as given (row 36) |

Each ends with `LOGOUT`, and stdout holds the untagged lines of the command's response.

## Fetch and search (BL-202)

Recorded on 2026-09-30 the same way, on port 18243 (the port is in no byte curl sends), with the
`GREETING`, `CAPABILITY`, `LOGIN`, `SELECT` and `LOGOUT` replies above and one reply more for the
command each case sends, written in the same escapes. Each message is `Subject: <n>`, an empty
line and `hello`, CRLF line ends (21 bytes), as `RecordedFixtureTests` stores it; a body fetch
that is not a `.PEEK` sets `\Seen`, so its response carries `FLAGS (\Seen)` (ADR-0055, decision 4).
curl exited 0 on every case.

| Folder | curl arguments | curl sends after `SELECT INBOX` | Reply given |
| --- | --- | --- | --- |
| `fetch-uid` | `'imap://.../INBOX;UID=1'` | `UID FETCH 1 BODY[]` (row 4) | `* 1 FETCH (UID 1 FLAGS (\Seen) BODY[] {21}` CRLF, the message, `)` |
| `fetch-mailindex` | `'imap://.../INBOX;MAILINDEX=1'` | `FETCH 1 BODY[]` (row 10) | `* 1 FETCH (FLAGS (\Seen) BODY[] {21}` CRLF, the message, `)` |
| `fetch-section-text` | `'imap://.../INBOX;UID=1;SECTION=TEXT'` | `UID FETCH 1 BODY[TEXT]` (row 5) | `... BODY[TEXT] {7}` CRLF `hello` CRLF `)` |
| `fetch-header-fields` | `'imap://.../INBOX;UID=1;SECTION=HEADER.FIELDS%20(SUBJECT)'` | `UID FETCH 1 BODY[HEADER.FIELDS (SUBJECT)]` (row 6) | `... BODY[HEADER.FIELDS (SUBJECT)] {14}` CRLF `Subject: 1` CRLF CRLF `)` |
| `fetch-section-1` | `'imap://.../INBOX;UID=1;SECTION=1'` | `UID FETCH 1 BODY[1]` (row 7) | `... BODY[1] {7}` CRLF `hello` CRLF `)` |
| `fetch-partial` | `'imap://.../INBOX;UID=1;PARTIAL=0.10'` | `UID FETCH 1 BODY[]<0.10>` (row 8) | `... BODY[]<0> {10}` CRLF `Subject: 1)` |
| `fetch-text-partial` | `'imap://.../INBOX;UID=1/;SECTION=TEXT;PARTIAL=0.3'` | `UID FETCH 1 BODY[TEXT]<0.3>` (row 8) | `... BODY[TEXT]<0> {3}` CRLF `hel)` |
| `search-subject` | `'imap://.../INBOX?SUBJECT%201'` | `SEARCH SUBJECT 1` (row 25) | `* SEARCH 1` |
| `fetch-all` | `-X 'FETCH 1:* ALL' imap://.../INBOX` | `FETCH 1:* ALL` | both messages' `FLAGS`, `INTERNALDATE`, `RFC822.SIZE` and `ENVELOPE` |
| `uid-fetch-bodystructure` | `-X 'UID FETCH 2 (BODYSTRUCTURE BODY.PEEK[HEADER])' imap://.../INBOX` | the command as given | `* 2 FETCH (UID 2 BODYSTRUCTURE (...) BODY[HEADER] {14}` CRLF, the header, `)` |
| `uid-search` | `-X 'UID SEARCH UNSEEN' imap://.../INBOX` | the command as given | `* SEARCH 1 2` |

Every case also gives `-sS -u u:p`; each reply ends with its `OK FETCH completed` or
`OK SEARCH completed`. `transcript.txt` holds each reply as sent.

## Append and the changing commands (BL-203)

Recorded on 2026-09-30 the same way, on port 18343, with the `GREETING`, `CAPABILITY`, `LOGIN`,
`SELECT` and `LOGOUT` replies above and one reply more for the command each case sends.
`mail.txt` is `From: a@x`, `Subject: hi`, an empty line and `hello`, CRLF line ends (33 bytes).
curl exited 0 on every case. `RecordedFixtureTests` replays each against the store above
(`INBOX` UIDs 1 and 2, `Sent` with `UIDVALIDITY` 1790668801), with UID 1 flagged `\Deleted`
first for the two expunge cases, and asserts the store's mailboxes, UIDs and flags afterwards.

The recorder always sends `+` before a literal, so only an `APPEND` Surl accepts can be served
exactly as Surl serves it; the refusals Surl sends in place of `+` (`TRYCREATE`, `TOOBIG`, ADR-0055
row 28) are pinned by the fast tests in `ImapAppendTests`.

| Folder | curl arguments | curl sends after `CAPABILITY` and `LOGIN u p` | Reply given |
| --- | --- | --- | --- |
| `append` | `-T mail.txt imap://.../INBOX` | `APPEND INBOX (\Seen) {33}`, the 33 bytes after `+`, CRLF (row 26) | `OK [APPENDUID 1790668800 3] APPEND completed` |
| `create` | `-X 'CREATE Archive' imap://.../` | `CREATE Archive` | `OK CREATE completed` |
| `delete` | `-X 'DELETE Sent' imap://.../` | `DELETE Sent` | `OK DELETE completed` |
| `rename` | `-X 'RENAME Sent Archive' imap://.../` | `RENAME Sent Archive` | `OK RENAME completed` |
| `subscribe` | `-X 'SUBSCRIBE Sent' imap://.../` | `SUBSCRIBE Sent` | `OK SUBSCRIBE completed` |
| `unsubscribe` | `-X 'UNSUBSCRIBE Sent' imap://.../` | `UNSUBSCRIBE Sent` | `OK UNSUBSCRIBE completed` |
| `store` | `-X 'STORE 1 +FLAGS \Deleted' imap://.../INBOX` | `SELECT INBOX`, the command (row 33) | `* 1 FETCH (FLAGS (\Deleted))` |
| `uid-store-silent` | `-X 'UID STORE 1:2 +FLAGS.SILENT (\Seen)' imap://.../INBOX` | `SELECT INBOX`, the command | `OK STORE completed` |
| `copy` | `-X 'COPY 1:2 Sent' imap://.../INBOX` | `SELECT INBOX`, the command | `OK [COPYUID 1790668801 1:2 1:2] COPY completed` |
| `uid-move` | `-X 'UID MOVE 1 Sent' imap://.../INBOX` | `SELECT INBOX`, the command | `* OK [COPYUID 1790668801 1 1] Moved`, `* 1 EXPUNGE` |
| `expunge` | `-X EXPUNGE imap://.../INBOX` | `SELECT INBOX`, `EXPUNGE` (row 34) | `* 1 EXPUNGE` |
| `uid-expunge` | `-X 'UID EXPUNGE 1:2' imap://.../INBOX` | `SELECT INBOX`, the command | `* 1 EXPUNGE` |

Every case also gives `-sS -u u:p`; each reply ends with its tagged completion.
