# SMTP fixtures

Exchanges recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1 -Smtp` (BL-198). Never from the Curl port (ADR-0003).

Each case was fed, through `-SmtpReply`, exactly the replies ADR-0053 decides and
`SmtpProtocolServer` sends, before any test pinned them, and curl completed each with the exit
code in `exitcode.txt`: 0 for every case but `refused-recipient`, which exits 55 with
`curl: (55) RCPT failed: 501`, as ADR-0053 row 10 measured. `RecordedFixtureTests` replays
each `request.bin` against an `--allow-anonymous` store and asserts that the bytes Surl writes
equal every `< ` line of `transcript.txt` with CRLF after each, and that the store holds the
message curl sent after decision 6's trace fields.

Each folder holds the recorder's five files: `request.bin` (the bytes curl sent),
`transcript.txt` (both directions, `>` curl and `<` the recorder), `stdout.bin`, `stderr.txt`
and `exitcode.txt`. They are embedded resources of `Surl.Protocol.Smtp.UnitTests`, so the tests
read them without touching the file system. `.gitattributes` here keeps git from rewriting
their CRLF line endings.

Recorded on 2026-09-29 from the repository root, in Windows PowerShell, on port 18025, with
`mail.txt` holding `From: a@x`, `To: b@y`, `Subject: hi`, an empty line, `hello`, `.dot line`
(53 bytes), `dots.txt` holding `Subject: dots`, an empty line, `.`, `..`, `.x`, `end` (33
bytes), every line ending CRLF, and every case given these replies:

```powershell
$replies = @(
    'GREETING=220 surl ESMTP ready',
    'EHLO=250-surl Hello\r\n250-SIZE 104857600\r\n250-8BITMIME\r\n250-SMTPUTF8\r\n250-PIPELINING\r\n250 ENHANCEDSTATUSCODES',
    'MAIL=250 2.1.0 Sender OK',
    'DATA=354 End data with <CR><LF>.<CR><LF>',
    'DATADONE=250 2.0.0 Message accepted',
    'QUIT=221 2.0.0 Bye',
    'VRFY=252 2.1.5 Cannot verify the user, but will accept the message',
    'EXPN=252 2.1.5 Cannot expand the list, but will accept the message',
    'HELP=214 2.0.0 Commands: EHLO HELO STARTTLS AUTH MAIL RCPT DATA RSET NOOP VRFY EXPN HELP QUIT',
    'NOOP=250 2.0.0 OK')
$ok = 'RCPT=250 2.1.5 Recipient OK'
$bad = 'RCPT=501 5.1.3 Invalid recipient address'
.\Record-CurlExchange.ps1 -Port 18025 -Smtp -SmtpReply ($replies + <RCPT replies>) -CurlArgs <curl arguments> -OutDirectory Surl.Protocol.Smtp.UnitTests\Fixtures\<folder>
```

Every URL is `smtp://127.0.0.1:18025/c`, so curl sends `EHLO c`.

| Folder | RCPT replies | curl arguments (then the URL) | curl sends |
| --- | --- | --- | --- |
| `one-recipient` | `$ok` | `-sS --mail-from a@x --mail-rcpt b@y -T mail.txt` | `MAIL FROM:<a@x> SIZE=53`, `RCPT TO:<b@y>`, `DATA`, the body with `..dot line` |
| `two-recipients` | `$ok` | the same with a second `--mail-rcpt c@z` | two `RCPT`s, then `DATA` |
| `refused-recipient` | `$ok`, `$bad` | `-sS --mail-from a@x --mail-rcpt b@y --mail-rcpt 'Bob <c@z>' -T mail.txt` | `RCPT TO:<Bob <c@z>`, then `QUIT`: exit 55 |
| `refused-recipient-allowfails` | `$ok`, `$bad` | the same with `--mail-rcpt-allowfails` first | both `RCPT`s, then `DATA`: exit 0 |
| `dot-stuffed-body` | `$ok` | `-sS --mail-from a@x --mail-rcpt b@y -T dots.txt` | `SIZE=33`, and the body lines `..`, `...`, `..x` |
| `vrfy` | `$ok` | `-sS --mail-rcpt b@y -X VRFY` | `VRFY b@y` |
| `expn` | `$ok` | `-sS --mail-rcpt list -X EXPN` | `EXPN list SMTPUTF8` |
| `help` | `$ok` | `-sS` | `HELP` |
| `noop` | `$ok` | `-sS -X NOOP` | `NOOP` |

## TLS cases (BL-199)

Recorded on 2026-09-30 the same way, with `mail.txt` as above, the common replies `GREETING`,
`MAIL`, `RCPT=250 2.1.5 Recipient OK`, `DATA`, `DATADONE` and `QUIT` from the list above, and
these `EHLO` replies (ADR-0053 decision 2, before and after TLS):

```powershell
$plain = 'EHLO=250-surl Hello\r\n250-SIZE 104857600\r\n250-8BITMIME\r\n250-SMTPUTF8\r\n250-PIPELINING\r\n250-ENHANCEDSTATUSCODES\r\n250 STARTTLS'
$tls = 'EHLO=250-surl Hello\r\n250-SIZE 104857600\r\n250-8BITMIME\r\n250-SMTPUTF8\r\n250-PIPELINING\r\n250 ENHANCEDSTATUSCODES'
```

| Folder | Recorder options and extra replies | curl arguments (then the URL) | curl sends |
| --- | --- | --- | --- |
| `starttls` | `-Smtp`, `$plain`, `STARTTLS=220 2.0.0 Ready to start TLS`, `$tls` | `-sS -k --ssl-reqd --mail-from a@x --mail-rcpt b@y -T mail.txt`, `smtp://127.0.0.1:18025/c` | `EHLO c`, `STARTTLS`, then over TLS `EHLO c` again and the one-recipient exchange |
| `smtps` | `-Smtp -Tls`, `$tls` | `-sS -k --mail-from a@x --mail-rcpt b@y -T mail.txt`, `smtps://127.0.0.1:18025/c` | TLS from the first byte, then the one-recipient exchange |

Both exit 0. `request.bin` holds the decrypted bytes. `RecordedFixtureTests` hands the
`starttls` request out in two reads split after the `STARTTLS` line, through
`UpgradePointRecordingConnection`, which fails the test if the server reads the second before
upgrading and pins the upgrade point right after the `220`; it serves `smtps` on a connection
that is TLS from the start, as the engine's implicit TLS gives it (ADR-0053 decision 5).

## AUTH cases (BL-200)

Recorded on 2026-09-30 the same way, with `mail.txt` as above, the common replies `GREETING`,
`MAIL`, `RCPT=250 2.1.5 Recipient OK`, `DATA`, `DATADONE` and `QUIT` from the first list, an
`EHLO` reply advertising the one mechanism last, and `-SaslChallenge` scripting that
mechanism's continuations and then `235 2.7.0 Authentication successful` (ADR-0049 section 7):

```powershell
$ehlo = 'EHLO=250-surl Hello\r\n250-SIZE 104857600\r\n250-8BITMIME\r\n250-SMTPUTF8\r\n250-PIPELINING\r\n250-ENHANCEDSTATUSCODES\r\n250 AUTH <MECHANISM>'
.\Record-CurlExchange.ps1 -Port 18025 -Smtp -SmtpReply ($common + $ehlo) -SaslChallenge '<MECHANISM>=334 <challenge>', ..., '<MECHANISM>=235 2.7.0 Authentication successful' -CurlArgs '-sS', <credentials>, '--login-options', 'AUTH=<MECHANISM>', [--sasl-ir,] '--mail-from', 'a@x', '--mail-rcpt', 'b@y', '-T', 'mail.txt', 'smtp://127.0.0.1:18025/c' -OutDirectory Surl.Protocol.Smtp.UnitTests\Fixtures\<folder>
```

Each mechanism has a folder `auth-<mechanism>` and, recorded with `--sasl-ir`, one
`auth-<mechanism>-sasl-ir`. The challenges are the `334` lines each `transcript.txt` shows; the
`-sasl-ir` cases drop what curl's initial response already answers, as the table says.

| Mechanism | Credentials | Challenges scripted |
| --- | --- | --- |
| `PLAIN` | `-u user:secret` | an empty one (not with `--sasl-ir`) |
| `LOGIN` | `-u user:secret` | `Username:` (not with `--sasl-ir`) and `Password:`, in base64 |
| `CRAM-MD5` | `-u user:secret` | `<0123456789abcdef.1790640000@surl>` (curl sends no initial response even with `--sasl-ir`) |
| `DIGEST-MD5` | `-u user:secret` | a `realm="surl"` challenge, then `rspauth=`, answered with an empty line |
| `NTLM` | `-u user:secret` | an empty one (not with `--sasl-ir`), then a type 2 message |
| `XOAUTH2` | `-u user: --oauth2-bearer tok` | an empty one (not with `--sasl-ir`) |
| `OAUTHBEARER` | `-u user: --oauth2-bearer tok` | an empty one, kept with `--sasl-ir`, where curl answers it `AQ==` |
| `EXTERNAL` | `-u user:` | an empty one (not with `--sasl-ir`) |

Every case exits 0. `RecordedFixtureTests` replays each against a store without
`--allow-anonymous`, with a `ScriptedMailAuthenticationPolicy` offering the one mechanism and
scripted with the recorded challenges then acceptance, and asserts the bytes Surl writes, what
the policy was handed, the login note, and the message stored with protocol `ESMTPA`.
