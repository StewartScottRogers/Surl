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
