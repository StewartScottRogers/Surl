# POP3 fixtures

Exchanges recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1 -Pop3` (BL-205). Never from the Curl port (ADR-0003).

Each case was fed, through `-Pop3Reply`, exactly the replies ADR-0056 decides and
`Pop3ProtocolServer` sends, before any test pinned them, and curl completed each with the exit
code in `exitcode.txt`: 0 for every case but `x-xyzzy` and `retr-missing`, which exit 8 with
`curl: (8) Weird server reply`, as ADR-0056 rows 11 and 15 measured. The recorder's own default
replies serve the rest (`STAT`, `LIST`, `LIST n`, `RETR`, `TOP`, `DELE`, `NOOP`, and `-ERR Command
not recognized`), and are the words ADR-0056 decision 5 decides. `RecordedFixtureTests` replays
each `request.bin` against a store whose account `u` holds two copies of the recorder's message,
and asserts that the bytes Surl writes equal every `< ` line of `transcript.txt` with CRLF after
each.

Each folder holds the recorder's five files: `request.bin` (the bytes curl sent),
`transcript.txt` (both directions, `>` curl and `<` the recorder), `stdout.bin`, `stderr.txt`
and `exitcode.txt`. They are embedded resources of `Surl.Protocol.Pop3.UnitTests`, so the tests
read them without touching the file system. `.gitattributes` here keeps git from rewriting
their CRLF line endings.

Recorded on 2026-09-30 from the repository root, in Windows PowerShell, on port 18117, with the
recorder's default message (133 bytes, its last line `.A line that starts with a dot.`) and
every case given these replies:

```powershell
$capaUser = 'CAPA=+OK Capability list follows\r\nTOP\r\nUIDL\r\nRESP-CODES\r\nAUTH-RESP-CODE\r\nPIPELINING\r\nUSER\r\n.'
$capaAfter = 'CAPA=+OK Capability list follows\r\nTOP\r\nUIDL\r\nRESP-CODES\r\nAUTH-RESP-CODE\r\nPIPELINING\r\n.'
$common = @('GREETING=+OK surl ready', 'USER=+OK User accepted', 'PASS=+OK Logged in',
    'QUIT=+OK surl signing off', 'RSET=+OK Maildrop has 2 messages (266 octets)', $capaUser)
.\Record-CurlExchange.ps1 -Port 18117 -Pop3 -Pop3IdleMilliseconds 3000 -Pop3Reply ($common + <extra replies>) -CurlArgs <curl arguments> -OutDirectory Surl.Protocol.Pop3.UnitTests\Fixtures\<folder>
```

`U` is `-sS -u u:p`, and every URL is `pop3://127.0.0.1:18117` then the path shown. With `USER`
in `CAPA` and neither `SASL` nor a timestamp in the greeting, curl logs in with `USER u` and
`PASS p`, then sends the command shown, then `QUIT`.

| Folder | curl arguments | Extra replies | curl sends |
| --- | --- | --- | --- |
| `list` | `U /` | | `LIST` |
| `retr` | `U /1` | | `RETR 1` |
| `list-l` | `U -l /` | | `LIST` |
| `list-one-l` | `U -l /1` | | `LIST 1` |
| `head-list` | `U -I /` | | `LIST` |
| `head-retr` | `U -I /1` | | `RETR 1` |
| `x-uidl` | `U -X UIDL /` | `UIDL=+OK Unique-ID listing follows\r\n1 1790668800.1\r\n2 1790668800.2\r\n.` | `UIDL` |
| `x-uidl-one` | `U -X 'UIDL 1' /` | `UIDL=+OK 1 1790668800.1` | `UIDL 1` |
| `x-capa` | `U -X CAPA /` | `$capaAfter` for the second `CAPA` | `CAPA` after the login |
| `x-list-one` | `U -X 'LIST 1' /` | | `LIST 1` |
| `x-dele` | `U -X 'DELE 1' /` | | `DELE 1` |
| `x-dele-url` | `U -X DELE /1` | | `DELE 1` |
| `x-top` | `U -X 'TOP 1 0' /` | | `TOP 1 0` |
| `x-top-url` | `U -X TOP /1` | | `TOP 1`: the headers, as a count of 0 |
| `x-retr` | `U -X 'RETR 1' /` | | `RETR 1` |
| `x-stat` | `U -X STAT /` | | `STAT` |
| `x-noop` | `U -X NOOP /` | | `NOOP` |
| `x-rset` | `U -X RSET /` | | `RSET` |
| `x-xyzzy` | `U -X XYZZY /` | | `XYZZY`, answered `-ERR Command not recognized`: exit 8 |
| `retr-missing` | `U /9` | `RETR=-ERR No such message` | `RETR 9`: exit 8 |
| `anonymous-retr` | `-sS /1` (no `-u`) | | `RETR 1` straight after `CAPA` |

`1790668800` is the `UIDVALIDITY` a test store's `INBOX` gets from the tests' clock
(2026-09-29T08:00:00Z). `anonymous-retr` is replayed against an `--allow-anonymous` store.
