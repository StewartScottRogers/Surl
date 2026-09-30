# FTP fixtures

Control-connection exchanges recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1 -Ftp` (BL-177). Never from the Curl port (ADR-0003).

Each case was fed, through `-FtpReply`, the exact replies ADR-0052 decides and
`FtpProtocolServer` sends, before any test pinned them. None of them uses a data connection:
each ends before curl would send `EPSV`, at a refused login or a `CWD` to a missing directory.

Each folder holds the recorder's files: `request.bin` (every command line curl sent),
`transcript.txt` (both directions, `> ` for curl and `< ` for the server), `stdout.bin`,
`stderr.txt` and `exitcode.txt`. They are embedded resources of `Surl.Protocol.Ftp.UnitTests`,
so the tests read them without touching the file system. `.gitattributes` here keeps git from
rewriting their CRLF line endings. `RecordedExchangeTests` replays each `request.bin`, whole and
one byte per read, against a content store holding `/a.txt`, `/dir/` and `/dir/sub/`, and
asserts the bytes surl writes equal the `< ` lines of `transcript.txt`, each ended by CRLF.

Recorded on 2026-09-29 from the repository root, in Windows PowerShell, with
`$g = '220 surl FTP server ready'`:

| Folder | What curl did | Exit | Command line |
| --- | --- | --- | --- |
| `login-cwd-missing` | `USER anonymous`, `PASS ftp@example.com`, `PWD`, `CWD nodir` (550), `QUIT` | 9 | `.\Record-CurlExchange.ps1 -Port 18721 -Ftp -FtpReply "GREETING=$g",'USER=331 Password required','PASS=230 Logged in','PWD=257 \"/\" is the current directory','CWD=550 No such directory','QUIT=221 Goodbye' -CurlArgs '-sS','ftp://127.0.0.1:18721/nodir/a.txt' -OutDirectory Surl.Protocol.Ftp.UnitTests\Fixtures\login-cwd-missing` |
| `quote-type-cwd` | the anonymous login, `PWD`, `TYPE A`, `TYPE I`, `NOOP`, `SYST` (from `-Q`), `CWD dir` (250), `CWD nodir` (550), `QUIT` | 9 | `.\Record-CurlExchange.ps1 -Port 18721 -Ftp -FtpReply "GREETING=$g",'USER=331 Password required','PASS=230 Logged in','PWD=257 \"/\" is the current directory','TYPE=200 Type set to A','TYPE=200 Type set to I','NOOP=200 NOOP ok','SYST=215 UNIX Type: L8','CWD=250 Directory changed','CWD=550 No such directory','QUIT=221 Goodbye' -CurlArgs '-sS','-Q','TYPE A','-Q','TYPE I','-Q','NOOP','-Q','SYST','ftp://127.0.0.1:18721/dir/nodir/a.txt' -OutDirectory Surl.Protocol.Ftp.UnitTests\Fixtures\quote-type-cwd` |
| `login-refused` | `USER tester`, `PASS wrong` (530 `Login incorrect`), then closed without `QUIT` | 67 | `.\Record-CurlExchange.ps1 -Port 18721 -Ftp -FtpReply "GREETING=$g",'USER=331 Password required','PASS=530 Login incorrect','QUIT=221 Goodbye' -CurlArgs '-sS','-u','tester:wrong','ftp://127.0.0.1:18721/a.txt' -OutDirectory Surl.Protocol.Ftp.UnitTests\Fixtures\login-refused` |
| `login-refused-plaintext` | `USER tester`, `PASS secret` (530 `Login needs TLS first: send AUTH TLS`), then closed without `QUIT` | 67 | `.\Record-CurlExchange.ps1 -Port 18721 -Ftp -FtpReply "GREETING=$g",'USER=331 Password required','PASS=530 Login needs TLS first: send AUTH TLS' -CurlArgs '-sS','-u','tester:secret','ftp://127.0.0.1:18721/a.txt' -OutDirectory Surl.Protocol.Ftp.UnitTests\Fixtures\login-refused-plaintext` |

curl waits for each reply before it sends the next line, and every case ended with the exit
code and the one-line `stderr.txt` ADR-0052 records for it (9: `Server denied you to change to
the given directory`; 67: `Access denied: 530`). The recorder's empty `upload.bin` was left out.
