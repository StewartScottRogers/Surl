# ADR-0052 — How the FTP server answers upstream curl, and the FTP data-connection seam

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-173 (FR-036).
- **Amends:** nothing. [ADR-0004](ADR-0004-the-listener-seam-and-the-exchange-context.md)'s
  `ExchangeContext` gains one `init` property with a default (decision 9); no existing interface
  gains a member. [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) section 5's FTP column
  and [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)'s "Protocol
  servers not yet built" FTP row are applied as written.

## Context

`surl ftp://...` and `surl ftps://...` are to be the server upstream curl's `ftp://` and `ftps://`
transfers talk to. FTP is two transports: a control connection of CRLF command lines and numbered
replies (RFC 959), and a data connection per transfer that either side opens - the server listens
(`EPSV`, `PASV`) or the server dials curl (`EPRT`, `PORT`). ADR-0004 rule 2 forbids a protocol
server to construct a socket, so the second transport needs a seam of its own. This ADR decides,
from measurement, every reply `Surl.Protocol.Ftp` sends, how logins map onto ADR-0032, the data
connection contract and who implements it, the limits and the help category, so BL-174 to BL-183
are built without a question.

### What upstream curl 8.21.0 does (measured)

- **Build:** the Windows reference build, `C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
  `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0
  (x86_64-w64-mingw32) libcurl/8.21.0 Schannel ...` (`UpstreamCurlBuilds.json`).
- **Tool:** `Record-CurlExchange.ps1 -Ftp`, 2026-09-29, on loopback, with `-FtpData 'hello world\n'`
  (12 bytes) unless a row says otherwise and `-FtpReply` overrides where a row names one. BL-173
  extended the script with `-FtpMaxUploadBytes` (stop reading an upload part-way and close the
  data connection under curl), `MLSD` served like `LIST`, and `CCC` answered with a TLS
  `close_notify` exchange and a return to plaintext; its comment-based help describes each.
- Every session below opens `< 220 ...`, and with no `-u` curl logs in as
  `> USER anonymous` / `> PASS ftp@example.com`; after the transfer it sends `> QUIT`. Those lines
  are left out of the excerpts.

| # | curl arguments (after `-sS`) | What curl sent, in order (excerpt) | Exit | Output |
| --- | --- | --- | --- | --- |
| 1 | `ftp://h/a.txt` | `PWD`, `EPSV`, `TYPE I`, `SIZE a.txt`, `RETR a.txt` | 0 | `hello world` |
| 2 | `-I ftp://h/a.txt` | `PWD`, `MDTM a.txt`, `TYPE I`, `SIZE a.txt`, `REST 0`; no data connection used | 0 | `Last-Modified: Sun, 27 Sep 2026 12:34:56 GMT`, `Content-Length: 12`, `Accept-ranges: bytes` |
| 3 | `-I ftp://h/a.txt`, `MDTM=550 ...` | as 2 | 0 | no `Last-Modified` line |
| 4 | `-r 0-4 ftp://h/a.txt` | as 1; curl closes the data connection after 5 bytes, reads the `RETR` reply, then sends `ABOR` | 0 | `hello` |
| 5 | as 4, `RETRDONE=426 Connection closed; transfer aborted`, `ABOR=226 Abort successful` | as 4 | 0 | `hello` |
| 6 | `-r 3-6 ftp://h/a.txt` | as 1 with `REST 3` before `RETR`, then `ABOR` | 0 | `lo w` |
| 7 | `-C 5 ftp://h/a.txt` | as 1 with `REST 5` before `RETR` | 0 | ` world` |
| 8 | `-C - -T up.txt ftp://h/a.txt` (11 bytes), `SIZE=213 4` | `EPSV`, `TYPE I`, `SIZE a.txt`, `APPE a.txt`; 7 bytes (`ad body`) sent | 0 | |
| 9 | as 8, `SIZE=550 No such file` | `SIZE a.txt`, `STOR a.txt`; 11 bytes sent | 0 | |
| 10 | as 8, the default `SIZE` 213 12 | `SIZE a.txt`, then `QUIT`: nothing to send | 0 | |
| 11 | `ftp://h/dir/` | `PWD`, `CWD dir`, `EPSV`, `TYPE A`, `LIST` | 0 | the listing as sent |
| 12 | `-l ftp://h/dir/` | as 11 with `NLST` | 0 | the names as sent |
| 13 | `-X MLSD ftp://h/dir/` | as 11 with `MLSD` | 0 | the facts lines as sent |
| 14 | `ftp://h/dir/`, `LIST=550 ...`; `-l`, `NLST=550 ...` | as 11 and 12 | 19 | `curl: (19) RETR response: 550` |
| 15 | `-T up.txt ftp://h/dir/b.txt` | `CWD dir`, `EPSV`, `TYPE I`, `STOR b.txt`; 11 bytes | 0 | |
| 16 | `-a -T up.txt ftp://h/b.txt` | `EPSV`, `TYPE I`, `APPE b.txt` | 0 | |
| 17 | `-T up.txt ftp://h/b.txt`, `-FtpMaxUploadBytes 4`, `STORDONE=552 Exceeded storage allocation` | `STOR b.txt`; the server read 4 bytes and closed | 70 | `curl: (70) Exceeded storage allocation` |
| 18 | `-T up.txt ftp://h/b.txt`, `STOR=550 ...` (or `553`) | `STOR b.txt` | 25 | `curl: (25) Failed FTP upload: 550` |
| 19 | `--ftp-create-dirs -T up.txt ftp://h/new/deep/b.txt`, each `CWD` first 550 then 250 | `CWD new`, `MKD new`, `CWD new`, `CWD deep`, `MKD deep`, `CWD deep`, `EPSV`, `TYPE I`, `STOR b.txt` | 0 | |
| 20 | as 19 with `MKD=550 ...` and every `CWD` 550 | `CWD new`, `MKD new`, `CWD new` | 9 | `curl: (9) Server denied you to change to the given directory` |
| 21 | `ftp://h/nodir/a.txt`, `CWD=550 ...` | `CWD nodir` | 9 | as 20 |
| 22 | `ftp://h/missing.txt`, `SIZE=550 ...` | `EPSV`, `TYPE I`, `SIZE missing.txt`; no `RETR` | 78 | `curl: (78) The file does not exist` |
| 23 | `ftp://h/a.txt`, `RETR=425 ...` | as 1 | 19 | `curl: (19) RETR response: 425` |
| 24 | `-Q "DELE a.txt" ftp://h/`, `DELE=250 ...` | `PWD`, `DELE a.txt`, `EPSV`, `TYPE A`, `LIST` | 0 | the listing |
| 25 | `-Q "RNFR a.txt" -Q "RNTO b.txt" ftp://h/` | `RNFR a.txt` (350), `RNTO b.txt` (250), then as 24 | 0 | the listing |
| 26 | `-Q "MKD x" -Q "-RMD x" -Q "+SITE CHMOD 644 a.txt" ftp://h/a.txt` | `MKD x` before `EPSV`; `SITE CHMOD 644 a.txt` after `TYPE I`, before `SIZE`; `RMD x` after the `RETR` reply | 0 | `hello world` |
| 27 | `-Q "DELE a.txt" ftp://h/`, `DELE=550 Not permitted` | `DELE a.txt`, then nothing | 21 | `curl: (21) QUOT command failed with 550` |
| 28 | `-I -Q "MLST a.txt" ftp://h/`, a multi-line `250-` ... `250 End` | `MLST a.txt` | 0 | |
| 29 | `ftp://h/d1/d2/a.txt` (default `multicwd`) | `CWD d1`, `CWD d2`, `EPSV`, `TYPE I`, `SIZE a.txt`, `RETR a.txt` | 0 | `hello world` |
| 30 | `--ftp-method singlecwd` | `CWD d1/d2`, then as 29 | 0 | `hello world` |
| 31 | `--ftp-method nocwd` | no `CWD`; `SIZE d1/d2/a.txt`, `RETR d1/d2/a.txt` | 0 | `hello world` |
| 32 | `--disable-epsv` | `PASV` in place of `EPSV` | 0 | `hello world` |
| 33 | `--disable-epsv`, `PASV=227 ... (10,9,8,7,<hi>,<lo>)` | curl connected to the control connection's address, not 10.9.8.7 | 0 | `hello world` |
| 34 | `EPSV=502 ...` | `EPSV` (502), then `PASV` | 0 | `hello world` |
| 35 | `EPSV=425 ...`, `PASV=425 ...` | `EPSV`, `PASV` | 13 | `curl: (13) Bad PASV/EPSV response: 425` |
| 36 | `-P - ftp://h/a.txt` | `EPRT \|1\|127.0.0.1\|<port>\|`, `TYPE I`, `SIZE`, `RETR`; the server dialled curl | 0 | `hello world` |
| 37 | `-P - --disable-eprt` | `PORT 127,0,0,1,<hi>,<lo>` in place of `EPRT` | 0 | `hello world` |
| 38 | `-P -`, `EPRT=501 ...`, `PORT=501 ...` | `EPRT`, `PORT` | 30 | `curl: (30) Failed to do PORT` |
| 39 | `-B ftp://h/a.txt` | `TYPE A`, `RETR` (no `SIZE`); the server sent the bytes unconverted | 0 | `hello world` with CRLF: the Windows tool writes `-B` output in text mode |
| 40 | `-u tester:secret ftp://h/a.txt` | `USER tester`, `PASS secret`, then as 1 | 0 | `hello world` |
| 41 | `-u tester:wrong`, `PASS=530 Login incorrect` | `USER tester`, `PASS wrong` | 67 | `curl: (67) Access denied: 530` |
| 42 | `-u tester:wrong`, `USER=530 ...`; with no `-u`, `USER=530 ...` | `USER` only | 67 | as 41 |
| 43 | `-u tester:secret`, `USER=230 ...` | `USER tester`, no `PASS`, then as 1 | 0 | `hello world` |
| 44 | `USER=500 Line too long` | `USER anonymous` | 67 | `curl: (67) Access denied: 500` |
| 45 | `PWD=502 ...` | `PWD`, then as 1 | 0 | `hello world` |
| 46 | `GREETING=421 Too many connections` | nothing | 28 | `curl: (28) Timeout was reached` |
| 47 | `PWD=421 Idle timeout, closing` | `PWD` | 28 | as 46 |
| 48 | `-k --ssl-reqd ftp://h/a.txt` | `AUTH SSL` (234, TLS 1.2 handshake), `USER`, `PASS`, `PBSZ 0`, `PROT P`, `PWD`, `EPSV`, `TYPE I`, `SIZE`, `RETR`; the data connection shook hands afresh with the recorder, which keeps no TLS session between connections | 0 | `hello world` |
| 49 | `-k --ssl-reqd`, `AUTH=502 ...` (or `534`) | `AUTH SSL`, `AUTH TLS` | 64 | `curl: (64) Requested SSL level failed` |
| 50 | `-k --ssl`, `AUTH=502 ...` (or `534`) | `AUTH SSL`, `AUTH TLS`, then the plaintext session as 1 | 0 | `hello world` |
| 51 | `-k --ssl-reqd`, `PROT=536 ...` | up to `PROT P` | 64 | as 49 |
| 52 | `-k --ftp-ssl-control ftp://h/a.txt` | as 48 with `PROT C`; the data connection in plaintext | 0 | `hello world` |
| 53 | `-k --ssl-reqd --ftp-ssl-ccc` (passive and `--ftp-ssl-ccc-mode active`), CCC answered 200 and TLS closed with `close_notify` | as 48 up to `PROT P`, `CCC`; `-v` shows `schannel: shutting down SSL/TLS connection` then the failure, before curl sent any TLS record back | 81 | `curl: (81) Failed to clear the command channel (CCC)` |
| 54 | as 53, `CCC=534 Request denied for policy reasons` | `CCC`, then as 48 from `PWD` over TLS | 0 | `hello world` |
| 55 | `-k ftps://h/a.txt` (`-Tls`, implicit) | TLS from the first byte; `USER`, `PASS`, `PBSZ 0`, `PROT P`, `PWD`, `EPSV`, `TYPE I`, `SIZE`, `RETR` | 0 | `hello world` |
| 56 | `-k ftps://h/dir/` | as 55 with `CWD dir`, `TYPE A`, `LIST` | 0 | the listing |
| 57 | `ftp://h/dir/*.txt` | `CWD dir`, `SIZE *.txt`, `RETR *.txt`: the curl tool sends a glob literally and never parses a listing | 0 | the file the server sent |

Nothing curl 8.21.0 sends in these sessions is `SYST`, `FEAT`, `OPTS`, `NOOP`, `STAT`, `HELP`,
`MODE`, `STRU`, `ALLO`, `ACCT`, `REIN`, `SMNT`, `CDUP` or `MLST` (unless `-Q` or `-X` names
it); `SYST` is sent only after a `PWD` reply whose path does not start with `/`, which surl never
sends.

## Decision

### 1. The greeting and the reply table

Every reply is one line, `<code> <text>\r\n`, except `FEAT`'s, `HELP`'s and `MLST`'s multi-line
`<code>-` ... `<code> ` replies (RFC 959 section 4.2). The text is fixed text from the server's own
table (ADR-0006 section 3); a path the peer sent is echoed only rendered as ADR-0006 section 3
says, with `"` doubled inside `257`'s quoted path (RFC 959 appendix II).

| Command | Reply |
| --- | --- |
| (greeting) | `220 surl FTP server ready` - no version (ADR-0006 section 3) |
| `USER <name>` | `331 Password required` whatever the name (decision 3) |
| `PASS <password>` | decision 3 |
| `AUTH TLS`, `AUTH SSL` | decision 5 |
| `PBSZ`, `PROT`, `CCC` | decision 5 |
| `PWD`, `XPWD` | `257 "<current directory>" is the current directory` |
| `CWD <path>`, `XCWD` | `250 Directory changed` to an existing directory; `550 No such directory` otherwise (a file, a missing or hidden path) |
| `CDUP`, `XCUP` | `250 Directory changed` (to the parent; at `/`, stays at `/`) |
| `TYPE I`, `TYPE L 8` | `200 Type set to I` |
| `TYPE A`, `TYPE A N` | `200 Type set to A`; the bytes are not converted (decision 4) |
| other `TYPE` | `504 Type not supported` |
| `MODE S`, `STRU F` | `200 Mode set to S` / `200 Structure set to F`; any other `504` |
| `SIZE <path>` | `213 <length>` for a file; `550 No such file` otherwise (a directory too) |
| `MDTM <path>` | `213 <yyyyMMddHHmmss>` of the file's last write in UTC (RFC 3659 section 3); `550 No such file` otherwise |
| `REST <n>` | `350 Restarting at <n>`; `501 Invalid restart offset` for anything but decimal digits |
| `EPSV`, `EPSV 1`, `EPSV 2`, `PASV`, `EPRT`, `PORT` | decision 6 |
| `EPSV ALL` | `200 EPSV ALL accepted`; from then on `PASV`, `EPRT` and `PORT` get `503 Only EPSV after EPSV ALL` (RFC 2428 section 4) |
| `RETR <path>` | decision 4 |
| `LIST [<path>]`, `NLST [<path>]`, `MLSD [<path>]` | decision 7 |
| `MLST [<path>]` | `250-` facts `250 End` (decision 7); `550 No such file` for an absent path |
| `STOR`, `APPE`, `MKD`, `XMKD`, `RMD`, `XRMD`, `DELE`, `RNFR`, `RNTO` | decision 8 |
| `ABOR` | `226 Abort successful` (decision 4) |
| `SYST` | `215 UNIX Type: L8` |
| `FEAT` | `211-Features:` then ` EPRT`, ` EPSV`, ` MDTM`, ` MLST type*;size*;modify*;`, ` PASV`, ` REST STREAM`, ` SIZE`, ` TVFS`, ` UTF8`, and on a listener with a certificate ` AUTH TLS`, ` PBSZ`, ` PROT`; then `211 End` |
| `OPTS UTF8 ON` | `200 UTF8 set to on`; any other `OPTS` `501 Option not understood` |
| `NOOP` | `200 NOOP ok` |
| `HELP` | `214-` the commands of this table, `214 End` |
| `ALLO`, `ACCT` | `202 Not needed` |
| `SITE <anything>` | `504 SITE <word> is not supported` (decision 8) |
| `QUIT` | `221 Goodbye`, then close |
| `REIN`, `SMNT`, `STOU`, `STAT`, anything else | `502 Command not implemented` |

- A command is its first word, matched without regard to case; its argument is the rest of the
  line after one space. A command that needs an argument and has none gets `501 Syntax error in
  arguments`.
- **Before login** only `USER`, `PASS`, `AUTH`, `PBSZ`, `PROT`, `FEAT`, `SYST`, `HELP`, `NOOP`,
  `OPTS` and `QUIT` are answered as above; every other command gets `530 Please log in with USER
  and PASS`. curl never sends one (it logs in first), so this answers only other clients.
- A `-X`/`--request` word curl sends in place of `LIST` or `NLST` (row 13) is answered from this
  table like any other command.

### 2. Paths and the current directory

- The session keeps a current directory, `/` at login; `PWD` names it. curl's three
  `--ftp-method`s (rows 29 to 31) all work: `multicwd` and `singlecwd` change directory and name
  the file alone, `nocwd` names the whole relative path.
- A path is read as UTF-8 (surl advertises `UTF8`, RFC 2640); one that is not valid UTF-8 is
  answered as absent. A path starting with `/` is absolute, any other is relative to the current
  directory; `.` and `..` segments are resolved lexically and a path that climbs above `/` is
  answered as absent.
- The resolved path is mapped through `ContentStore.MapRequestPath`, so ADR-0006 section 2's
  exposure rules and ADR-0031 decision 5 (nothing under `/.surl`) apply exactly as over HTTP, and
  "answered as absent" is the command's `550` in decision 1.
- A glob is not expanded: `RETR *.txt` names a file called `*.txt` (row 57).

### 3. Logins (ADR-0032, ADR-0038)

- `USER` is answered `331 Password required` whatever the name, without asking the policy, so
  the reply never says whether an account exists. A second `USER` before `PASS` replaces the name.
  `USER` after a login gets `503 Already logged in`.
- `PASS` without a preceding `USER` gets `503 Send USER first`. Otherwise the server calls
  `IAuthenticationPolicy.CheckPasswordLoginAsync(new PasswordLogin(scheme, user, passwordBytes,
  connection.TlsSession))`, with the scheme of the listen URL (`ftp` or `ftps`), the user name as
  sent (decoded as UTF-8, as the path is) and the password's bytes as sent, and the `TlsSession`
  the control connection has at that moment - `null` on `ftp://` before `AUTH TLS`:

  | Verdict | Reply | Login note (ADR-0038) |
  | --- | --- | --- |
  | `Accepted` | `230 Logged in` | `Login accepted: ftp <user>` (`ftps` on an `ftps://` listen URL) |
  | `AcceptedUnchecked` (`--allow-anonymous`) | `230 Logged in` | none |
  | `RefusedCredentials` | `530 Login incorrect`, after ADR-0032's fixed 1-second delay | `Login refused: ftp <user>` |
  | `RefusedPlaintext` | `530 Login needs TLS first: send AUTH TLS`, undelayed, the password unchecked | none |
  | `RefusedAnonymous` | `530 Login incorrect` | none |

  curl exits 67 on either `530` (rows 41, 42).
- **With no account configured** the policy refuses every checked login (`RefusedCredentials`),
  so `530` (ADR-0032 row 2).
- **curl's default anonymous login** (`USER anonymous`, `PASS ftp@example.com`, measured) is an
  ordinary login: FTP is not one of ADR-0032's anonymous protocols (HTTP, Gopher, TFTP), so it is
  refused unless `--allow-anonymous` is given (then `AcceptedUnchecked`, served as anonymous) or
  an account named `anonymous` exists with that password. Over `ftp://` without TLS it is
  `RefusedPlaintext` unless `--allow-plaintext-auth` or `--allow-anonymous` is given, as the
  policy already decides for MQTT. So `curl ftp://127.0.0.1:2121/a.txt` needs
  `surl --allow-anonymous ftp://127.0.0.1:2121/`; `curl -u tester:secret` over `ftp://` needs
  `--allow-plaintext-auth` or `--ssl-reqd`.
- No per-connection attempt counter: the 1-second delay and the per-address connection limit bound
  guessing (ADR-0032).
- The logged-in user is the account the verdict named; the FTP server serves every account the
  same content store (as HTTP does) and records no per-user state beyond the session.

### 4. Downloads and data transfers

- **`RETR <path>`**: for a file, `150 Opening data connection for <path> (<n> bytes)`, then the
  file from the last `REST` offset over the data connection (decision 6), then the data
  connection is completed (`CompleteWritesAsync`, with `close_notify` under TLS) and
  `226 Transfer complete`. For anything else `550 No such file` before any data connection is
  used. A `REST` offset past the file's length gets `554 Restart offset past end of file` (RFC
  3659 section 5.4). A `REST` applies to the next `RETR`, `STOR` or `APPE` only and is then
  cleared.
- **No data connection**: when the passive listener accepts none within the timeout, or the
  active connect fails, `425 Cannot open data connection` (curl exits 19, row 23).
- **curl closes the data connection early** (a range, rows 4 to 6): the write fails, the server
  stops and replies `426 Connection closed; transfer aborted`; the `ABOR` curl then sends is
  answered `226 Abort successful` (row 5: exit 0). `ABOR` with no transfer in progress is answered
  `226` too (RFC 959 section 4.1.3). The server does not read the control connection during a
  transfer; curl sends `ABOR` only after the reply.
- **`TYPE A`** is accepted and changes nothing: files are sent and stored as their bytes, so
  `SIZE`, `REST` and byte ranges stay exact (row 39: curl completes, and the Windows tool itself
  writes `-B` output in text mode). Converting line ends is not what any curl option asks the
  server for; a client that needs CRLF has `--crlf` on upload.
- **`-I`** (row 2) is answered from `MDTM`, `SIZE` and `REST 0` alone, all above.

### 5. TLS: `AUTH`, `PBSZ`, `PROT`, `CCC` and implicit FTPS

- **`AUTH TLS` and `AUTH SSL`** (curl sends `AUTH SSL` first, row 48) are both answered `234 AUTH
  accepted, start TLS` and the control connection is upgraded with `IConnection.UpgradeToTlsAsync`
  (ADR-0010: the same certificate, TLS versions and no renegotiation as every listener). Bytes
  buffered after the `AUTH` line are discarded before the handshake (as ADR-0050 decides for
  `STARTTLS`). With no certificate (neither `--cert` nor `--self-signed`, ADR-0032 section 10)
  `AUTH` gets `534 TLS is not available` (rows 49 and 50: curl exits 64 with `--ssl-reqd`, carries
  on in plaintext with `--ssl`). On a connection already under TLS: `503 Already using TLS`.
  Another mechanism word: `504 Security mechanism not understood`.
- **`PBSZ 0`** after TLS: `200 PBSZ=0`; before TLS `503 Send AUTH first`; another size: `200
  PBSZ=0` (RFC 4217 section 9).
- **`PROT P`** and **`PROT C`** after `PBSZ`: `200 Protection level set to P` (or `C`); every
  later data connection is then TLS or plaintext. `PROT S` and `PROT E`: `536 Protection level
  not supported`. Before `PBSZ`: `503 Send PBSZ first`. `PROT C` is honoured: `--ftp-ssl-control`
  asks for it (row 52), and the login it protects is still encrypted. The default before any
  `PROT` is `C` on `ftp://` and `P` on `ftps://`.
- **TLS on a data connection** is `IConnection.UpgradeToTlsAsync` on the data connection the seam
  returns (decision 9), started by the server as soon as the connection is open. Surl does **not**
  require the data connection to resume the control connection's TLS session: the pinned build
  completes a fresh handshake (rows 48, 55), and requiring resumption would refuse clients that
  do not offer it. A data-connection handshake that fails is `425 Cannot open data connection`.
- **`CCC`** is refused: `534 Request denied for policy reasons` (RFC 4217 section 11). Measured,
  curl then carries on over TLS and completes (row 54, exit 0), while an accepted `CCC` fails with
  the Windows reference build whatever follows (row 53, exit 81). Refusing keeps the control
  connection, and so later passwords and commands, encrypted, which is the secure default, and
  needs no `IConnection` member to leave TLS.
- **`ftps://`** is implicit TLS: the engine's handshake happens before the greeting, as for
  `https://` (ADR-0020); curl then sends `PBSZ 0` and `PROT P` without `AUTH` (row 55).

### 6. Passive and active data connections, and the defences

- **`EPSV`** (and `EPSV 1`, `EPSV 2`): the server asks the seam for a passive listener (decision
  9) and answers `229 Entering Extended Passive Mode (|||<port>|)`. **`PASV`**: the same listener,
  answered `227 Entering Passive Mode (<a>,<b>,<c>,<d>,<p1>,<p2>)`. The address named is the
  control connection's local address - the one curl reached - mapped to IPv4 when it is an
  IPv4-mapped IPv6 address. So a listen URL of `0.0.0.0` never announces `0.0.0.0`. On an IPv6
  control connection `PASV` cannot name the address and is answered `425 Use EPSV on IPv6`; curl
  uses `EPSV` first (rows 1, 34). A second `EPSV`/`PASV` replaces the listener. A seam that
  refuses (the default, decision 9) gets `425 Cannot open data connection` (curl exits 13, row 35).
  curl connects to the control connection's address whatever `227` names (row 33), so the address
  matters only to other clients.
- **The listener** binds the control connection's local address with an ephemeral port chosen by
  the operating system, accepts connections only from the control connection's peer address
  (compared after IPv4-mapping); a connection from any other address is closed at once and the
  listener goes on waiting (RFC 2577 section 4, port stealing). It accepts one connection, within
  the exchange's head timeout (`--head-timeout`, 30 s by default) from the `EPSV`/`PASV` reply,
  and is disposed when that connection is taken, when another `EPSV`/`PASV`/`EPRT`/`PORT`
  replaces it, or when the exchange ends. curl connects as soon as it reads the `229` (before
  `TYPE`), so the connection normally waits for the transfer command, not the other way round.
  There is no port-range option: an operator who needs a range firewalls the host; it can be added
  without changing the contract.
- **`EPRT |<af>|<address>|<port>|`** and **`PORT <h1>,...,<p2>`** are answered `200 EPRT command
  successful` (or `PORT`) only when the address equals the control connection's peer address
  (after IPv4-mapping) and the port is 1024 or above (RFC 2577 section 3, bounce attacks);
  otherwise `501 Address must be your own, port 1024 or above` (curl exits 30 when both are
  refused, row 38 - it never sends another address). A malformed argument: `501 Syntax error in
  arguments`; `EPRT` with an address family other than 1 or 2: `522 Network protocol not
  supported, use (1,2)` (RFC 2428 section 2). The connection is made when the transfer command
  arrives, within the head timeout; the seam checks the peer address again (decision 9).
- A transfer command with no preceding `EPSV`, `PASV`, `EPRT` or `PORT` gets `425 Use PASV or
  PORT first`. Each data connection carries one transfer and is closed after it.

### 7. Listings

- **`--list-directories` off (the default)**: `LIST`, `NLST` and `MLSD` of a directory are
  answered as absent, `550 No such directory` before any data connection is used (ADR-0006
  section 2; curl exits 19, row 14). `MLST` of a directory and `LIST`/`NLST` naming one file are
  not directory listings and are answered.
- **`LIST`**: the Unix `ls -l` form, one entry per line, CRLF-terminated, sorted ordinally by
  name, from `ContentStore.ListDirectory` (ADR-0009): a file `-rw-r--r-- 1 surl surl <size>
  <date> <name>`, a directory `drwxr-xr-x 1 surl surl 0 <date> <name>`, `<size>` right-aligned in
  12 columns, `<date>` `MMM dd HH:mm` (month in English, day space-padded) when the entry was
  written within the last 180 days by the injected `TimeProvider`, else `MMM dd  yyyy`; times in
  UTC. The curl tool prints a listing as sent and never parses one (row 57: it sends a glob
  literally), so the form is for people and for other clients that parse `ls -l`, which is what
  most FTP servers send. Arguments starting with `-` (`LIST -a`) are ignored. `LIST <file>` lists
  that one file.
- **`NLST`**: each name alone, CRLF-terminated, same order (row 12).
- **`MLSD`** and **`MLST`** (RFC 3659 section 7): facts `type=file;size=<n>;modify=<yyyyMMddHHmmss>;`
  or `type=dir;modify=...;` then a space and the name; `MLSD` over the data connection, one line
  per entry, no `cdir`/`pdir` lines; `MLST` on the control connection as `250-Listing <path>`,
  ` <facts> <path>`, `250 End`.
- Dot-files are left out unless `--serve-dot-files` (ADR-0006 section 2); `/.surl` never appears.
  A listing is sent in both `TYPE`s alike and replied `150 Opening data connection for directory
  listing` then `226 Transfer complete`.

### 8. Uploads and file management

Every command in this decision needs `--allow-uploads`; without it each is answered `550 Not
permitted` (ADR-0006 section 2), before any data connection is used (curl exits 25 for `STOR`,
row 18; 21 for a `-Q` command, row 27; 9 for `--ftp-create-dirs`, row 20).

| Command | With `--allow-uploads` |
| --- | --- |
| `STOR <path>` | `150 Opening data connection for <path>`, the bytes read to the data connection's end and written through `ContentStore.WriteUploadAsync` (a temporary dot-file renamed into place), then `226 Transfer complete`. A `REST <n>` before it: `n` equal to the current length appends as `APPE`; `0` writes afresh; anything else `554 Restart offset must equal the file's length`. The directory must exist: `553 No such directory` otherwise. |
| `APPE <path>` | as `STOR`, appending to the existing file (creating it when absent), rows 8 and 16 |
| `MKD <path>`, `XMKD` | `257 "<path>" created`; `550 Already exists` for an existing entry, `550 No such directory` for a missing parent (rows 19, 20) |
| `RMD <path>`, `XRMD` | `250 Directory removed` for an empty directory; `550 Directory not empty`, or `550 No such directory` |
| `DELE <path>` | `250 File deleted`; `550 No such file` for anything else (a directory too) |
| `RNFR <path>` | `350 Ready for RNTO` for an existing entry, `550 No such file` otherwise |
| `RNTO <path>` | after `RNFR`: `250 Renamed`, replacing an existing file; `553 Cannot rename onto a directory`, or `553 No such directory` for a missing target directory. Without `RNFR` immediately before: `503 Send RNFR first` |
| `SITE ...` | `504 SITE <word> is not supported` for every form: the content store has no permissions, owners or times to set (`SITE CHMOD`, `SITE UTIME`), so no form can be true. curl sends `SITE` only when `-Q` asks (row 26); a failing `-Q` command ends curl with 21 unless prefixed `*`. |

- **Upload past `--max-filesize`** (ADR-0006 section 5): the server stops reading at the limit,
  closes the data connection, deletes the partial upload (the temporary file is never renamed)
  and replies `552 Upload exceeds the size limit` (curl exits 70, row 17). An `APPE` counts the
  existing length plus the appended bytes.
- A data connection that ends with a reset or a TLS failure mid-upload: the partial upload is
  deleted and the reply is `426 Connection closed; transfer aborted`.
- A path under `/.surl`, or a dot-file without `--serve-dot-files`, is answered as absent, or
  `550 Not permitted` for a new name (ADR-0031 decision 5).
- `ContentStore` has `WriteUploadAsync` and nothing else that writes; delete, rename, directory
  creation and removal and append are filed as BL-226 (`Surl.Content`), on which BL-180 depends.

### 9. The data-connection seam

In `Surl.Protocol.Abstractions.UnitLibrary`, namespace `Surl.Protocol.Abstractions`, every type
from the shared framework (BL-174):

```csharp
/// Opens an exchange's FTP data connections. Only Surl.Networking implements it over sockets;
/// Surl.Core wraps it per exchange (logs, idle clock); protocol tests use InMemoryDataConnections.
public interface IDataConnectionOpener
{
    /// Binds a listener on controlLocal's address, ephemeral port, that accepts only from
    /// controlRemote's address (IPv4-mapped compared as IPv4). Throws DataConnectionException.
    ValueTask<IPassiveDataListener> StartPassiveListenerAsync(
        EndPoint controlLocal, EndPoint controlRemote, CancellationToken cancellationToken);

    /// Connects to target, which must have controlRemote's address and a port of 1024 or more.
    /// Throws DataConnectionException (Refused for another address, Unreachable, TimedOut).
    ValueTask<IConnection> ConnectActiveAsync(
        EndPoint controlRemote, IPEndPoint target, TimeSpan timeout, CancellationToken cancellationToken);
}

public interface IPassiveDataListener : IAsyncDisposable
{
    /// The address and port to announce in 229 and 227.
    IPEndPoint LocalEndPoint { get; }

    /// The first connection from the peer's address within timeout; others are closed unseen.
    /// Throws DataConnectionException(TimedOut) when none arrives.
    ValueTask<IConnection> AcceptAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

public enum DataConnectionFailure { Unavailable, Refused, Unreachable, TimedOut }

public sealed class DataConnectionException(DataConnectionFailure failure, string message)
    : Exception(message)
{
    public DataConnectionFailure Failure { get; } = failure;
}

/// The default: every request throws DataConnectionException(Unavailable).
public sealed class RefusingDataConnectionOpener : IDataConnectionOpener
{
    public static RefusingDataConnectionOpener Instance { get; } = new();
    // StartPassiveListenerAsync and ConnectActiveAsync throw Unavailable.
}

public sealed record ExchangeContext(/* ADR-0004's positional parameters, unchanged */)
{
    // ...existing members unchanged...
    public IDataConnectionOpener DataConnections { get; init; } = RefusingDataConnectionOpener.Instance;
}
```

- **No existing interface with outside implementers gains a member.** `IListenerFactory`
  (implemented by `Surl.Console.UnitTests/FakeListenerFactory.cs` and `Surl.Networking`),
  `IConnection` (eleven implementers across Core, Networking, Abstractions and five test
  projects) and `IConnectionProtocolServer` are unchanged. `ExchangeContext` keeps its positional
  constructor, so every call site compiles unchanged; the new member is an `init` property with a
  default, as `Limits` is.
- A data connection is an `IConnection`: the server reads, writes, completes writes, aborts and
  upgrades it to TLS (`UpgradeToTlsAsync`, decision 5) exactly as a control connection. Its
  `TlsSession` is its own.
- **`InMemoryDataConnections`** (BL-174), beside `InMemoryConnection` in Abstractions, lets a
  protocol test script what curl does: the passive listener's announced end point, an
  `InMemoryConnection` handed out by `AcceptAsync` or `ConnectActiveAsync`, a scripted failure,
  and a record of each call's arguments.
- **`Surl.Networking`** (BL-175) implements it as `SocketDataConnectionOpener`, constructed with
  the `ServerTlsSettings` the control listener uses (so `UpgradeToTlsAsync` has the certificate),
  reusing `StreamConnection`, `ServerTlsHandshake`, `SocketTransportControl` (ADR-0021's
  lingering close) and the accept-failure absorption of ADR-0022. It refuses an active target
  whose address is not the peer's with `Refused`, as the FTP server already did (defence in
  depth).
- **`Surl.Core`** (BL-176): `ServingEngine` takes an optional `IDataConnectionOpener` (default
  `RefusingDataConnectionOpener.Instance`, so `Surl.Console` compiles unchanged until BL-182 passes
  `Surl.Networking`'s) and gives each exchange an `ExchangeContext` whose `DataConnections` is a
  per-exchange wrapper that:
  - wraps every data connection in the same `RecordingConnection` and
    `IdleClockRestartingConnection` as the control connection, so data bytes restart the
    exchange's idle clock (ADR-0006 section 1: "Bytes on an FTP data connection belong to the
    exchange that opened it") and appear in its verbose and trace logs as `<= Recv data` /
    `=> Send data` events under the exchange's `#<id>` (ADR-0033);
  - writes a note when each opens and closes: `Data connection opened: passive from
    <remote>.` (or `active to <remote>.`) and `Data connection closed.`, so a trace reader can
    tell data bytes from control bytes;
  - disposes every listener and connection still open when the exchange ends.
- **Limits.** A data connection is part of its exchange: it is not counted against
  `--max-connections` or `--max-connections-per-address`, gets no `ExchangeId`, and ends with the
  exchange's idle timeout and maximum duration. An exchange has at most one passive listener and
  one data connection at a time (decision 6), so it cannot multiply sockets.

### 10. Limits (ADR-0006 section 5)

| Limit | FTP answer | curl (measured) |
| --- | --- | --- |
| Too many connections | `421 Too many connections`, then close, in place of the greeting | 28, row 46 |
| Head timeout (a command line not finished within `--head-timeout`) | `421 Timeout waiting for a command`, then close | 28, row 47 |
| Line past `--max-line` (8192 bytes, CRLF included) | `500 Command line too long`, then close | 67 when it answers `USER`, row 44 |
| Upload past `--max-filesize` | `552 Upload exceeds the size limit`, the partial upload deleted | 70, row 17 |
| Idle timeout, maximum duration | `421 Timeout, closing`, then close | 28, row 47 |
| A refused write (no `--allow-uploads`) | `550 Not permitted` | 25, 21 or 9 (decision 8) |

A line ends at LF; a CR before it is dropped; a bare CR inside a line is kept as a byte of the
argument. The head timeout runs from a command line's first byte to its LF (between commands the
idle timeout governs), as ADR-0006 section 1 says for the line-oriented servers.

### 11. Help

The `ftp` category, `FTP and FTPS protocol` (ADR-0034 decision 1's wording), claims the schemes
`ftp` and `ftps`, and holds every option the FTP server reads: the content options
(`--directory`, `--allow-uploads`, `--list-directories`, `--follow-symlinks`,
`--serve-dot-files`), the limits it applies (`--max-filesize`, `--max-line`, `--head-timeout`) and
the login options (`--user`, `--user-file`, `--allow-anonymous`, `--allow-plaintext-auth`).
BL-182 adds the category, its `--aihelp` topic, prose and example, and grows the pinned topic
lists, as root `CLAUDE.md` requires. Default ports are curl's: 21 for `ftp`, 990 for `ftps`.

### 12. What BL-183 proves with the pinned build

Each against `surl` over loopback, run through `Record-CurlExchange.ps1 -NoServer` or the
conformance harness, with the options named; `-k` wherever TLS is used with `--self-signed`:

| surl options | curl command line | Exit |
| --- | --- | --- |
| `--allow-anonymous` | `curl -sS ftp://127.0.0.1:<p>/a.txt` | 0, the file |
| none | `curl -sS ftp://127.0.0.1:<p>/a.txt` | 67 (`RefusedPlaintext`) |
| `--user tester:secret --allow-plaintext-auth` | `curl -sS -u tester:secret ftp://.../a.txt` | 0 |
| `--user tester:secret --allow-plaintext-auth` | `curl -sS -u tester:wrong ftp://.../a.txt` | 67 |
| `--user tester:secret --self-signed` | `curl -sS -k --ssl-reqd -u tester:secret ftp://.../a.txt` | 0 |
| `--user tester:secret --self-signed` | `curl -sS -k --ftp-ssl-control -u tester:secret ftp://.../a.txt` | 0 |
| `--user tester:secret --self-signed` | `curl -sS -k --ssl-reqd --ftp-ssl-ccc -u tester:secret ftp://.../a.txt` | 0 (CCC refused) |
| `--user tester:secret --self-signed` | `curl -sS -k -u tester:secret ftps://.../a.txt` | 0 |
| `--user tester:secret` (no certificate) | `curl -sS --ssl-reqd -u tester:secret ftp://.../a.txt` | 64 |
| `--allow-anonymous` | `curl -sS -I ftp://.../a.txt` | 0, `Last-Modified`, `Content-Length` |
| `--allow-anonymous` | `curl -sS -r 0-4 ftp://.../a.txt` | 0, 5 bytes |
| `--allow-anonymous` | `curl -sS -C 5 ftp://.../a.txt` | 0 |
| `--allow-anonymous` | `curl -sS -B ftp://.../a.txt` | 0 |
| `--allow-anonymous` | `curl -sS --ftp-method singlecwd ftp://.../d1/d2/a.txt`, and `nocwd`, and the default | 0 |
| `--allow-anonymous` | `curl -sS --disable-epsv ftp://.../a.txt` | 0 |
| `--allow-anonymous` | `curl -sS -P - ftp://.../a.txt`, and with `--disable-eprt` | 0 |
| `--allow-anonymous` | `curl -sS ftp://.../missing.txt` | 78 |
| `--allow-anonymous` | `curl -sS ftp://.../dir/` | 19 (listings off) |
| `--allow-anonymous --list-directories` | `curl -sS ftp://.../dir/`, and `-l`, and `-X MLSD` | 0 |
| `--allow-anonymous` | `curl -sS -T up.txt ftp://.../b.txt` | 25 (uploads off) |
| `--allow-anonymous --allow-uploads` | `curl -sS -T up.txt ftp://.../b.txt`, then `-a -T`, then `-C - -T` | 0 |
| `--allow-anonymous --allow-uploads` | `curl -sS --ftp-create-dirs -T up.txt ftp://.../new/deep/b.txt` | 0 |
| `--allow-anonymous --allow-uploads --max-filesize 4` | `curl -sS -T up.txt ftp://.../b.txt` | 70 |
| `--allow-anonymous --allow-uploads` | `curl -sS -Q "DELE b.txt" ftp://.../`, `-Q "RNFR a.txt" -Q "RNTO c.txt"`, `-Q "MKD x" -Q "-RMD x"` | 0 (listing off: add `--list-directories`, else 19 after the quotes) |
| `--allow-anonymous` | `curl -sS -Q "DELE a.txt" ftp://.../` | 21 |
| `--allow-anonymous` | `curl -sS -Q "SITE CHMOD 644 a.txt" ftp://.../a.txt` | 21 |
| `--allow-anonymous --max-connections 1`, a second client holding the first | `curl -sS ftp://.../a.txt` | 28 |

On Linux and macOS the pinned OpenSSL builds are run the same way; any exit that differs is
pinned per platform in its own test (root `CLAUDE.md`), and recorded against this ADR.

## Alternatives considered

- **Add passive and active members to `IListenerFactory`.** Rejected: its implementers outside
  the FTP tasks (`FakeListenerFactory`, `Surl.Networking`) would all change, and the factory
  starts listeners before any exchange exists, while a data connection belongs to one exchange.
- **A `DowngradeFromTlsAsync` on `IConnection` to accept `CCC`.** Rejected: eleven implementers,
  the Windows reference build fails an accepted `CCC` anyway (row 53), and leaving TLS sends
  later commands in clear; refusing it completes (row 54).
- **Require TLS session resumption on data connections** (as some servers do against data
  connection theft). Rejected: the passive listener already accepts only the peer's address, and
  requiring resumption would refuse clients that do not offer it.
- **Convert line ends under `TYPE A`.** Rejected: `SIZE`, `REST` and ranges would count bytes
  that are not the file's, and curl gives the server no reason to.
- **Anonymous FTP by default, like HTTP reads.** Rejected: ADR-0032 names HTTP, Gopher and TFTP as
  the anonymous protocols; FTP's anonymous login is a login, loosened by `--allow-anonymous`.
- **Answer `SITE CHMOD` with `200` and ignore it.** Rejected: "say what it does" - the store has
  no mode to set, so a success reply would be false.
- **A passive port-range option now.** Deferred to need, not refused: the contract takes the
  address from the control connection and leaves the port to the implementation, so a range is an
  option and a `Surl.Networking` change, with no contract change.
- **Count data connections against `--max-connections`.** Rejected: an exchange has at most one
  at a time, and counting them would refuse the data connection of an exchange already admitted.

## Consequences

- BL-174 adds decision 9's types and `InMemoryDataConnections` to Abstractions; BL-175
  implements `SocketDataConnectionOpener` in `Surl.Networking`; BL-176 wraps it per exchange in
  `Surl.Core`; BL-177 to BL-181 build `Surl.Protocol.Ftp` from decisions 1 to 8 and 10; BL-182
  composes it and adds decision 11's help; BL-183 proves decision 12's table.
- BL-226 (`Surl.Content`: delete, rename, create and remove a directory, append) is filed, and
  BL-180 depends on it.
- The recordings behind every row can be made again with the arguments in the table; BL-177 to
  BL-181 record their own fixtures from them.
