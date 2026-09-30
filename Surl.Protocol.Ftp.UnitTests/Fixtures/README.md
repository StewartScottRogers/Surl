# FTP fixtures

FTP exchanges recorded from upstream curl 8.21.0, the win-x64 build pinned in
`UpstreamCurlBuilds.json` (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1 -Ftp` (BL-177, BL-178). Never from the Curl port (ADR-0003).

Each case was fed, through `-FtpReply`, the exact replies ADR-0052 decides and
`FtpProtocolServer` sends, before any test pinned them. The four control-connection cases
below use no data connection: each ends before curl would send `EPSV`, at a refused login or a
`CWD` to a missing directory. The downloads under "Downloads (BL-178)" do.

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

## Downloads (BL-178)

Recorded on 2026-09-29 the same way, with `-FtpData 'hello world\n'` (the 12 bytes of `/a.txt`)
and every reply surl sends given through `-FtpReply`, so each `< ` line is surl's own text.
BL-178 extended the recorder so an overridden `RETR` reply starting with `1` replaces only the
`150` and still serves the data connection. `SIZE` (`213 12`), `MDTM` (`213 20260927123456`),
`REST` (`350 Restarting at <n>`), `EPRT`/`PORT` (`200 EPRT command successful`) and the
`226 Transfer complete` after the data are the recorder's defaults, which are surl's texts too.
`RecordedDownloadTests` replays each `request.bin` with an in-memory data connection: a passive
case scripts the fake listener with the port the recorder announced in `229` or `227`, and an
active case checks surl dialled the port curl named. It asserts the replies equal the `< `
lines, that surl sent `/a.txt` from the `REST` offset on the data connection, and that
`stdout.bin` is those bytes or, for a range, their start. Every case exited 0 with an empty
`stderr.txt`. With `$g` as above and

```powershell
$r = "GREETING=$g",'USER=331 Password required','PASS=230 Logged in','PWD=257 \"/\" is the current directory','EPSV=229 Entering Extended Passive Mode (|||{DATAPORT}|)','PASV=227 Entering Passive Mode (127,0,0,1,{DATAPORT_HI},{DATAPORT_LO})','TYPE=200 Type set to I','RETR=150 Opening data connection for a.txt (12 bytes)','ABOR=226 Abort successful','QUIT=221 Goodbye'
```

each was `.\Record-CurlExchange.ps1 -Port 18745 -Ftp -FtpData 'hello world\n' -FtpReply $r -CurlArgs '-sS',<arguments>,'ftp://127.0.0.1:18745/a.txt' -OutDirectory Surl.Protocol.Ftp.UnitTests\Fixtures\<folder>`:

| Folder | curl arguments | What curl did |
| --- | --- | --- |
| `download` | none | `PWD`, `EPSV`, `TYPE I`, `SIZE a.txt`, `RETR a.txt`, `QUIT` |
| `head` | `-I` | `PWD`, `MDTM a.txt`, `TYPE I`, `SIZE a.txt`, `REST 0`, `QUIT`; no data connection; printed `Last-Modified`, `Content-Length` and `Accept-ranges` |
| `range-0-9` | `-r 0-9` | as `download`, then `ABOR` after the `226`; printed 10 bytes |
| `continue-at-auto` | `-C -` | as `download`: writing to stdout, curl resumes from 0 and sends no `REST` |
| `continue-at-5` | `-C 5`, with `RETR=150 Opening data connection for a.txt (7 bytes)` in `$r` | as `download` with `REST 5` before `RETR`; printed ` world\n` |
| `disable-epsv` | `--disable-epsv` | `PASV` in place of `EPSV` |
| `active-eprt` | `-P -` | `EPRT \|1\|127.0.0.1\|<port>\|` in place of `EPSV`; the recorder dialled curl |
| `active-port` | `-P -`, `--disable-eprt` | `PORT 127,0,0,1,<hi>,<lo>` in place of `EPSV` |

## Listings (BL-179)

Recorded on 2026-09-30 the same way, with `-FtpData` set to the listing surl sends for `/dir/`
of the content store `RecordedListingTests` serves (the file `b.txt`, 4 bytes, and the
directory `sub`, both last written 2026-09-27 12:34:56 UTC, with `--list-directories`), so curl
printed surl's own bytes. `RecordedListingTests` replays each `request.bin` with an in-memory
passive data connection on the port the recorder announced, and asserts the replies equal the
`< ` lines and that the bytes surl sent on the data connection equal `stdout.bin`. Both cases
exited 0 with an empty `stderr.txt`. With `$g` as above and

```powershell
$base = "GREETING=$g",'USER=331 Password required','PASS=230 Logged in','PWD=257 \"/\" is the current directory','CWD=250 Directory changed','EPSV=229 Entering Extended Passive Mode (|||{DATAPORT}|)','TYPE=200 Type set to A','QUIT=221 Goodbye'
```

| Folder | What curl did | Command line |
| --- | --- | --- |
| `list-directory` | `PWD`, `CWD dir`, `EPSV`, `TYPE A`, `LIST`, `QUIT`; printed the listing as sent | `.\Record-CurlExchange.ps1 -Port 18779 -Ftp -FtpData '-rw-r--r-- 1 surl surl            4 Sep 27 12:34 b.txt\r\ndrwxr-xr-x 1 surl surl            0 Sep 27 12:34 sub\r\n' -FtpReply ($base + 'LIST=150 Opening data connection for directory listing') -CurlArgs '-sS','ftp://127.0.0.1:18779/dir/' -OutDirectory Surl.Protocol.Ftp.UnitTests\Fixtures\list-directory` |
| `name-list-directory` | as `list-directory` with `NLST`; printed the names as sent | `.\Record-CurlExchange.ps1 -Port 18779 -Ftp -FtpData 'b.txt\r\nsub\r\n' -FtpReply ($base + 'NLST=150 Opening data connection for directory listing') -CurlArgs '-sS','-l','ftp://127.0.0.1:18779/dir/' -OutDirectory Surl.Protocol.Ftp.UnitTests\Fixtures\name-list-directory` |

## Uploads and file management (BL-180)

Recorded on 2026-09-30 the same way, with `-FtpData 'hello world\n'` and every reply surl sends
given through `-FtpReply`, so each `< ` line is surl's own text; the `226 Transfer complete`
after an upload is the recorder's default, which is surl's text too. `up.txt` held
`uploaded body\n` (14 bytes) and `up-longer.txt` `hello world\nmore\n` (17 bytes), both in a
temporary folder. Each folder also holds the recorder's `upload.bin`: the bytes curl sent on
the data connection. `RecordedUploadTests` replays each `request.bin` against a content store
with `--allow-uploads`, with an in-memory passive data connection on the port the recorder
announced carrying what curl sent, and asserts the replies equal the `< ` lines and the
uploaded bytes land in the store. With `$g` as above and

```powershell
$base = "GREETING=$g",'USER=331 Password required','PASS=230 Logged in','PWD=257 \"/\" is the current directory','EPSV=229 Entering Extended Passive Mode (|||{DATAPORT}|)','TYPE=200 Type set to I','QUIT=221 Goodbye'
```

each was `.\Record-CurlExchange.ps1 -Port <port> -Ftp -FtpData 'hello world\n' -FtpReply ($base + <replies>) -CurlArgs '-sS',<arguments> -OutDirectory Surl.Protocol.Ftp.UnitTests\Fixtures\<folder>`:

| Folder | curl arguments | Added replies | What curl did | Exit |
| --- | --- | --- | --- | --- |
| `upload` | `-T up.txt ftp://127.0.0.1:18801/dir/b.txt` | `'CWD=250 Directory changed','STOR=150 Opening data connection for b.txt'` | `PWD`, `CWD dir`, `EPSV`, `TYPE I`, `STOR b.txt`; 14 bytes sent | 0 |
| `upload-resume` | `-C - -T up-longer.txt ftp://127.0.0.1:18802/a.txt` | `'APPE=150 Opening data connection for a.txt'` | `PWD`, `EPSV`, `TYPE I`, `SIZE a.txt` (`213 12`), `APPE a.txt`; the 5 bytes past 12 sent | 0 |
| `upload-create-dirs` | `--ftp-create-dirs -T up.txt ftp://127.0.0.1:18803/new/deep/b.txt` | `'CWD=550 No such directory','CWD=250 Directory changed','CWD=550 No such directory','CWD=250 Directory changed','MKD=257 \"/new\" created','MKD=257 \"/new/deep\" created','STOR=150 Opening data connection for b.txt'` | `CWD new` (550), `MKD new`, `CWD new`, the same for `deep`, then `EPSV`, `TYPE I`, `STOR b.txt` | 0 |
| `quote-file-management` | `-Q 'MKD x' -Q '*SITE CHMOD 644 a.txt' -Q '-RNFR a.txt' -Q '-RNTO x/c.txt' -Q '-DELE x/c.txt' -Q '-RMD x' ftp://127.0.0.1:18804/a.txt` | `'MKD=257 \"/x\" created','SITE=504 SITE CHMOD is not supported','RNFR=350 Ready for RNTO','RNTO=250 Renamed','DELE=250 File deleted','RMD=250 Directory removed','RETR=150 Opening data connection for a.txt (12 bytes)'` | `MKD x`, `SITE CHMOD 644 a.txt` (504, ignored for its `*`), the download, then `RNFR`, `RNTO`, `DELE`, `RMD` | 0 |
| `upload-too-large` | `-T up.txt ftp://127.0.0.1:18805/b.txt`, with `-FtpMaxUploadBytes 4` | `'STOR=150 Opening data connection for b.txt','STORDONE=552 Upload exceeds the size limit'` | `STOR b.txt`; the recorder read 4 bytes and closed; `curl: (70) Exceeded storage allocation` | 70 |
| `upload-not-permitted` | `-T up.txt ftp://127.0.0.1:18806/b.txt` | `'STOR=550 Not permitted'` | `STOR b.txt`, no data connection; `curl: (25) Failed FTP upload: 550` | 25 |
