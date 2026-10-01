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

## TLS, APOP and AUTH cases (BL-206)

Recorded on 2026-09-30 the same way, on port 18126, with the recorder's default message and these
replies (ADR-0056 decisions 2 to 4 and 8, ADR-0049 section 7):

```powershell
$fixed = 'CAPA=+OK Capability list follows\r\nTOP\r\nUIDL\r\nRESP-CODES\r\nAUTH-RESP-CODE\r\nPIPELINING'
$common = @('USER=+OK User accepted', 'PASS=+OK Logged in', 'QUIT=+OK surl signing off')
$greeting = 'GREETING=+OK surl ready'
$stamped = 'GREETING=+OK surl ready <0123456789abcdef.1790640000@surl>'
.\Record-CurlExchange.ps1 -Port 18126 -Pop3 -Pop3IdleMilliseconds 3000 -Pop3Reply ($common + <greeting> + <CAPA replies> + <extra replies>) [-SaslChallenge <lines>] [-Tls] -CurlArgs <curl arguments> -OutDirectory Surl.Protocol.Pop3.UnitTests\Fixtures\<folder>
```

`CAPA` below lists what follows `$fixed`, then `\r\n.`; two `CAPA` replies are sent in turn.

| Folder | Greeting, `CAPA` and extra replies | curl arguments (then the URL) | curl sends | Exit |
| --- | --- | --- | --- | --- |
| `stls` | `$greeting`; `STLS`, then after TLS `USER`; `STLS=+OK Begin TLS negotiation` | `-sS -k --ssl-reqd -u u:p`, `/1` | `CAPA`, `STLS`, over TLS `CAPA`, `USER u`, `PASS p`, `RETR 1`, `QUIT` | 0 |
| `stls-not-offered` | `$greeting`; nothing | `-sS --ssl-reqd -u u:p`, `/1` | `CAPA`, then hangs up: `curl: (64) STLS not supported.` | 64 |
| `pop3s` | `$greeting`; `USER`; `-Tls` | `-sS -k -u u:p`, `pop3s://127.0.0.1:18126/1` | TLS from the first byte, `CAPA`, `USER u`, `PASS p`, `RETR 1`, `QUIT` | 0 |
| `apop` | `$stamped`; nothing; `APOP=+OK Authentication successful` | `-sS -u user:secret`, `/1` | `CAPA`, `APOP user 32d4437494fda0ae78d0559952474e34`, `RETR 1`, `QUIT` | 0 |
| `apop-forced` | `$stamped`; `USER`, `SASL CRAM-MD5 PLAIN`; as `apop` | `-sS -u user:secret --login-options AUTH=+APOP`, `/1` | as `apop` | 0 |
| `apop-refused` | `$stamped`; nothing; `APOP=-ERR [AUTH] Authentication failed` | `-sS -u user:secret`, `/1` | `CAPA`, `APOP ...`, then hangs up: `curl: (67) Authentication failed: 45` | 67 |
| `auth-refused` | `$greeting`; `SASL CRAM-MD5`; `-SaslChallenge 'CRAM-MD5=+ PDAxMjM0NTY3ODlhYmNkZWYuMTc5MDY0MDAwMEBzdXJsPg==', 'CRAM-MD5=-ERR [AUTH] Authentication failed'` | `-sS -u user:wrong`, `/1` | `CAPA`, `AUTH CRAM-MD5`, the response, then hangs up: `curl: (67) Login denied` | 67 |
| `auth-<mechanism>` | `$greeting`; `SASL <MECHANISM>`; `-SaslChallenge` scripting the challenges below, then `+OK Authentication successful` | `-sS -u <credentials> --login-options AUTH=<MECHANISM>`, `/1` | `CAPA`, `AUTH <MECHANISM>`, the responses, `RETR 1`, `QUIT` | 0 |

The `auth-<mechanism>` folders, and with `-sasl-ir` those recorded with `--sasl-ir`, whose initial
response answers the first challenge. The challenges are SMTP's (its `Fixtures/README.md`) with
`+ ` in place of `334 `:

| Mechanism | Credentials | Challenges scripted |
| --- | --- | --- |
| `PLAIN` (and `-sasl-ir`) | `user:secret` | an empty one (not with `--sasl-ir`) |
| `LOGIN` (and `-sasl-ir`) | `user:secret` | `Username:` (not with `--sasl-ir`) and `Password:`, in base64 |
| `CRAM-MD5` | `user:secret` | `<0123456789abcdef.1790640000@surl>` |
| `DIGEST-MD5` | `user:secret` | a `realm="surl"` challenge, then `rspauth=`, answered with an empty line |
| `NTLM` (and `-sasl-ir`) | `user:secret` | an empty one (not with `--sasl-ir`), then ADR-0039's type 2 message |
| `XOAUTH2` (and `-sasl-ir`) | `user: --oauth2-bearer tok` | an empty one (not with `--sasl-ir`) |
| `OAUTHBEARER` | `user: --oauth2-bearer tok` | an empty one |
| `EXTERNAL` (and `-sasl-ir`) | `user:` | an empty one (not with `--sasl-ir`) |

`request.bin` holds the decrypted bytes. `RecordedFixtureTests` hands the `stls` request out in two
reads split after the `STLS` line, through `UpgradePointRecordingConnection`, which fails the test if
the server reads the second before upgrading and pins the upgrade point right after the `+OK`; it
serves `pop3s` on a connection that is TLS from the start. The `apop` cases run on a clock at
1790640000 and a random source giving `0123456789abcdef`, so Surl's greeting is the recorded one.
The `auth-` cases run with a `Pop3TestPolicy` offering the one mechanism, scripted with every `+ `
challenge of the transcript then acceptance (refusal for `auth-refused`), and assert the bytes Surl
writes and the mechanism and responses the policy was handed.
