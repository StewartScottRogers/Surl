# Product Overview

- **Status:** Draft. Written for the Phase 0 shell and kept true as the phases are built; the
  numbers below are measured, not estimated. No section is awaiting a decision.
- **Last updated:** 2026-11-12
- **Measured against:** upstream curl 8.21.0 (released 2026-06-24), tag `curl-8_21_0` of
  https://github.com/curl/curl, and the builds pinned in `UpstreamCurlBuilds.json`

## What is Surl?

Surl ("Server URL") is the server-side mate of curl, written in C# on .NET 10. curl is
the client: `curl [options] <url>` fetches what the URL names. Surl is the server that
answers it: `surl [options] <url>` listens where the URL names - the scheme picks the
protocol, the host and port pick the bind address, and several URLs mean several
listeners at once.

The name stays as the scope grows: whatever upstream curl can ask, Surl answers.

## Two programs named curl

Surl's measure of success is **upstream curl** - the original C implementation at
https://github.com/curl/curl - and nothing else (Stewart, 2026-09-28; ADR-0003). A Surl
behaviour is right when a pinned upstream curl build completes the exchange against it as
the protocol's specification and upstream curl's behaviour say.

**The Curl port** (https://github.com/StewartScottRogers/Curl), Stewart's C# port of curl,
is the other program, and the relationship runs one way. Once Surl stands on its own it
becomes the instrument that measures the port: the port runs the same conversations
against Surl beside upstream curl, and wherever the two disagree, upstream is right and
the disagreement is a defect in the port. The port is never used to validate Surl,
because a Surl built to satisfy the port would carry the port's defects, and the later
check would then pass on exactly those defects.

Telling the two apart takes the file, not the name. Measured on Stewart's machine on
2026-09-28:

| What runs | Is | Notes |
| --- | --- | --- |
| `curl` in Windows PowerShell 5.1 | `Invoke-WebRequest` | A built-in alias, not curl at all |
| The first `curl.exe` on `PATH` | WinGet curl 8.18.0, LibreSSL | Not the reference release; has SMB, HTTP/2 and HTTP/3 |
| `C:\Program Files\Git\mingw64\bin\curl.exe` | upstream curl 8.21.0, Schannel | Pinned; the Curl port's Windows reference too |
| `C:\Windows\System32\curl.exe` | upstream curl 8.21.0, Schannel | Microsoft's build; lacks `rtsp`, `scp`, `sftp`, `smb`, NTLM |

The Curl port reports upstream's own version number on purpose, so neither the name nor
`--version` identifies upstream curl. `UpstreamCurlBuilds.json` pins each build by path
and SHA-256, and the tools refuse anything else.

## Problem

curl can request 29 URL schemes. Exercising a client across that surface needs a server
for every one of them, and today that means a zoo. Upstream curl's own test suite, at tag
`curl-8_21_0`, runs 2,013 test cases against seven test servers written in C (`sws` for
HTTP, `rtspd`, `tftpd`, `mqttd`, `socksd`, `dnsd`, and `sockfilt` beneath the FTP family)
and eleven Perl and Python programs that start or stand in for more (`ftpserver.pl` for
FTP, IMAP, POP3 and SMTP; `dictserver.py`, `negtelnetserver.py`, `smbserver.py`,
`sshserver.pl`, `secureserver.pl`, `http-server.pl`, `http2-server.pl`,
`http3-server.pl`, `rtspserver.pl`, `tftpserver.pl`), several of them wrappers around
third-party servers for TLS, SSH, SMB and HTTP/2 and HTTP/3.

There is no single, cross-platform server that answers everything curl can request, speaks
each protocol the way curl expects it, and can itself be unit tested without a network.
So testing curl, or anything that claims to behave like curl, means assembling that zoo
first - and the Curl port needs exactly that to be measured.

## Users

- Developers testing curl, or any client that claims curl's behaviour, who want one
  `surl` on `PATH` that answers every scheme.
- The Curl port, in the last phase: Surl is the instrument that measures it.
- .NET test suites that stand up a server in-process, without launching a process, through
  `Surl.Api` (Stewart, 2026-11-04;
  [ADR-0098](../Planning/Decisions/ADR-0098-surl-api-and-surl-mcp-join-surl-as-products.md)).
- AI agents that start, list and stop Surl servers and see what a client sent, through the
  `Surl.Mcp` MCP server host, the `surl-mcp` executable (ADR-0098).
- Contributors adding a protocol server, who need the seams to be obvious.

`surl` is one of three products: `Surl.Api` and `Surl.Mcp` sit beside it in `Application/` and
keep its secure-by-default rules (ADR-0098). `Surl.Api`'s design is
[ADR-0099](../Planning/Decisions/ADR-0099-one-server-composition-for-surl-and-surl-api-and-surl-apis-programming-interface.md)
and `Surl.Mcp`'s is
[ADR-0103](../Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md);
all three start servers through `Surl.Composition`.

Surl is built to be exposed to the internet, not only to answer on loopback in a test
(Stewart, 2026-09-28). The security scope that hardening sets - connection, time and size
limits, what a server exposes by default, what a peer may learn, and the TLS minimums -
is decided in
[ADR-0006](../Planning/Decisions/ADR-0006-hardening-for-internet-facing-use.md).

## Non-goals

- **Validating Surl against anything but upstream curl.** Not the Curl port, not another
  server, not a specification read in isolation from what curl actually sends. The one
  exception is the extended servers (ADR-0083), which upstream curl cannot speak to: each
  is judged by its own specification.
- **Beating dedicated servers on throughput.** Fidelity to what upstream curl expects is
  the target, not nginx's request rate.
- **Protocols upstream curl does not request**, except the Curl port's extended protocols
  (ADR-0083, Stewart 2026-11-04), served on a parallel "Extended servers" track and turned
  off with `SURL_EXTENSIONS=0`. Otherwise Surl answers curl; a protocol curl cannot speak has
  no mate to be.

## Scope: the surface Surl must answer

| Surface | Size | Source |
| --- | ---: | --- |
| URL schemes | 29 | `Protocols` lines of the builds measured on 2026-09-28: 27 in the pinned 8.21.0 build, plus `smb` and `smbs` from the WinGet 8.18.0 build |
| Upstream test cases | 2,013 | `tests/data` at tag `curl-8_21_0` |
| Upstream test servers | 7 in C, 11 scripts | `tests/server` and `tests/` at tag `curl-8_21_0` |

### 29 schemes, 15 protocol servers and a content store

| Library | Schemes answered |
| --- | --- |
| `Surl.Protocol.Http` | `http`, `https`, and the `/ipfs/` and `/ipns/` gateway paths curl turns `ipfs://` and `ipns://` into |
| `Surl.Protocol.Ws` | `ws`, `wss` |
| `Surl.Protocol.Ftp` | `ftp`, `ftps` |
| `Surl.Protocol.Ssh` | `scp`, `sftp` |
| `Surl.Protocol.Smtp` | `smtp`, `smtps` |
| `Surl.Protocol.Imap` | `imap`, `imaps` |
| `Surl.Protocol.Pop3` | `pop3`, `pop3s` |
| `Surl.Protocol.Ldap` | `ldap`, `ldaps` |
| `Surl.Protocol.Mqtt` | `mqtt`, `mqtts` |
| `Surl.Protocol.Smb` | `smb`, `smbs` |
| `Surl.Protocol.Gopher` | `gopher`, `gophers` |
| `Surl.Protocol.Rtsp` | `rtsp` |
| `Surl.Protocol.Tftp` | `tftp` |
| `Surl.Protocol.Telnet` | `telnet` |
| `Surl.Protocol.Dict` | `dict` |
| none | `file` has no wire and no server; `Surl.Content` serves files to every protocol that needs them instead (ADR-0002) |

### Built for Phase 2: FTP, FTPS, SCP and SFTP

`surl` registers both Phase 2 servers (`Surl.Composition`'s `ComposeProtocolServers`), each with
its help category and `--aihelp` topic, `ftp` and `ssh`. The terms below are defined in the
[glossary](../Wiki/Glossary.md), sections "FTP" and "SSH, SCP and SFTP".

- **FTP and FTPS** (`Surl.Protocol.Ftp`,
  [ADR-0052](../Planning/Decisions/ADR-0052-how-the-ftp-server-answers-and-the-ftp-data-connection-seam.md)):
  a control connection with `USER`/`PASS` logins through the authentication policy; downloads
  (`RETR` with `REST`, `SIZE`, `MDTM`, `ABOR`) over passive (`EPSV`, `PASV`) or active (`EPRT`,
  `PORT`) data connections, to and from the client's own address only; listings (`LIST`,
  `NLST`, `MLSD`, `MLST`) only with `--list-directories`; uploads and file management (`STOR`,
  `APPE`, `MKD`, `RMD`, `DELE`, `RNFR`/`RNTO`) only with `--allow-uploads`; explicit FTPS
  (`AUTH TLS`, `PBSZ`, `PROT`, with `CCC` refused) when a certificate is configured, and
  implicit FTPS on `ftps://`; and the limit replies of ADR-0052 decision 10, a limit told from
  shutdown as [ADR-0059](../Planning/Decisions/ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md)
  decides. The data connections come through a seam, `IDataConnectionOpener`, that only
  `Surl.Networking` implements over sockets, so the FTP server stays off the network in its tests.
- **SCP and SFTP** (`Surl.Protocol.Ssh`,
  [ADR-0051](../Planning/Decisions/ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md)
  for the transport, host keys and logins,
  [ADR-0054](../Planning/Decisions/ADR-0054-how-the-ssh-server-answers-upstream-curls-scp-and-sftp-requests.md)
  for SCP and SFTP): one SSH server for both schemes. The transport offers the key exchanges,
  host-key algorithms, ciphers, MACs and compression of ADR-0051 decision 2, among them
  `curve25519-sha256`, `ssh-ed25519` and `chacha20-poly1305@openssh.com` over the hand-built
  primitive libraries of
  [ADR-0048](../Planning/Decisions/ADR-0048-the-hand-built-ssh-primitive-libraries.md), with
  strict key exchange and key re-exchange
  ([ADR-0058](../Planning/Decisions/ADR-0058-the-ssh-key-exchange-and-host-key-reading-choices-adr-0051-left-open.md),
  [ADR-0060](../Planning/Decisions/ADR-0060-messages-before-the-clients-kexinit-in-a-server-started-ssh-re-exchange.md));
  host keys from `--hostkey` or `--throwaway-hostkey`; logins by password,
  keyboard-interactive and public key (`--authorized-keys`); session channels running an SCP
  command (download and upload of one file) or the SFTP subsystem (reads, listings with
  `--list-directories`, writes, appends, resumed uploads and tree changes with
  `--allow-uploads`, and curl's `-Q` commands), all through the content store.
- **Served only when asked for:** the weak SSH algorithms of ADR-0051 decision 2 and
  [ADR-0061](../Planning/Decisions/ADR-0061-blowfish-cast-128-and-ripemd-160-for-curls-openssl-builds.md)
  (SHA-1, MD5, CBC, RC4, 3DES, Blowfish, CAST-128, RIPEMD-160 and 1024-bit Diffie-Hellman),
  with RSA host and user keys shorter than 2048 bits and DSA keys, are offered after the
  default algorithms only when a start gives `--allow-weak-ssh-algorithms`, which then writes
  `surl: warning: --allow-weak-ssh-algorithms: SHA-1, MD5, CBC, RC4, 3DES and 1024-bit
  Diffie-Hellman SSH algorithms are offered` from the info level up (ADR-0051 decision 11).
- **Host certificates:** `--hostcert <file>` serves an OpenSSH host certificate for a
  `--hostkey` key, offered under the key's algorithms with `-cert-v01@openssh.com` added, each
  just before the plain one (`ssh-rsa-cert-v01@openssh.com` only with
  `--allow-weak-ssh-algorithms`), and sent as the host key when curl picks it (ADR-0051
  decision 4 and Amendment 2).

**What pinned upstream curl has proven.** The integration tests in `Surl.Conformance.UnitTests`
run each Phase 2 case against a live `surl` on loopback. With the Windows reference build
(curl 8.21.0, libssh2 1.11.1 on WinCNG) every case of ADR-0052 decision 12 over `ftp` and
`ftps` passed, and every row of ADR-0054 decision 16 over `scp` and `sftp`, with the logins and
host-key checks of ADR-0051, as ADR-0054's Amendment 1 records; that build negotiates
`diffie-hellman-group-exchange-sha256`, `rsa-sha2-512` and `chacha20-poly1305@openssh.com`, and
cannot use an ECDSA-only or Ed25519-only `surl`, since WinCNG offers only RSA host-key
algorithms. About one SSH connection in 256 with that build fails its key exchange, a libssh2
1.11.1 defect `surl` does not work around
([ADR-0062](../Planning/Decisions/ADR-0062-surl-keeps-ks-canonical-mpint-though-libssh2-1-11-1-on-wincng-fails-1-exchange-in-256.md)).
The same tests run on CI's Linux and macOS legs with the OpenSSL reference builds; what those
builds negotiate (`curve25519-sha256`, and `ecdsa-sha2-nistp256` or `ssh-ed25519` for such a
host key) is pinned there as predicted and not yet recorded as measured (ADR-0051 decision 2).
An algorithm no run negotiates - the weak ones among them - is proven by unit tests only.

### Built for Phase 3: SMTP, IMAP and POP3

`surl` registers the three mail servers (`Surl.Composition`'s `ComposeProtocolServers`), each with
its help category and `--aihelp` topic, `smtp`, `imap` and `pop3`. Each server claims its
plaintext scheme only; `smtps`, `imaps` and `pop3s` are the same server behind
`ImplicitTlsSchemeServer`, over a connection the engine has already secured, and the server
tells the two apart by `IConnection.TlsSession`, never by the scheme. All three are handed the
one mail store, so mail sent over `smtp://` is read over `imap://` and `pop3://` in the same run.
The terms below are defined in the [glossary](../Wiki/Glossary.md), section "Mail".

- **The mail store** (`Surl.MailStore`,
  [ADR-0050](../Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md)
  decisions 2 to 7): `MailboxStore` keeps a set of mailboxes per owner - every named account, or
  under `--allow-anonymous` the one anonymous owner every session and recipient reaches - each
  mailbox with its `UIDVALIDITY`, next UID and messages carrying the five system flags. It is
  bounded (100000 messages, 256 MiB of message bytes, 10000 mailboxes besides the `INBOX`es,
  one message by `--max-filesize`), every refusal a peer can cause is a `MailStoreOutcome`, and
  with `--directory` it persists under `<path>/.surl/mail` (an index and one file per distinct
  message) and is loaded at start, a malformed store ending surl with 37. Without `--directory`
  it lives in memory. `--allow-uploads` gates none of it: mail is service state, not a served
  file.
- **The line machinery** (`Surl.LineProtocol`, ADR-0050 decision 8): `CrlfLineReader` reads
  command lines that end only at CRLF, bounded by `--max-line` and `--head-timeout`, SMTP's
  dot-stuffed `DATA` body, IMAP literals and SASL continuation lines, and discards what was
  pipelined after `STARTTLS` or `STLS`; `DotStuffedBodyWriter` writes POP3's `RETR` and `TOP`;
  `ReplyLineWriter` refuses any byte that could inject a reply line.
- **The mail logins** ([ADR-0049](../Planning/Decisions/ADR-0049-the-mail-servers-sasl-and-apop-logins.md)):
  `IMailAuthenticationPolicy`, implemented by `Surl.Authentication`'s `AuthenticationPolicy`,
  offers the SASL mechanisms `--auth` accepts - `GSSAPI`, `DIGEST-MD5`, `CRAM-MD5`, `NTLM`,
  `OAUTHBEARER`, `XOAUTH2`, `PLAIN`, `LOGIN` and `EXTERNAL`, in that order - and POP3 `APOP`,
  and runs each exchange; the plain-text ones (`PLAIN`, `LOGIN`, `XOAUTH2`, `OAUTHBEARER`, IMAP
  `LOGIN` and POP3 `USER`/`PASS`) are offered only over TLS or with `--allow-plaintext-auth`.
  With the default `--auth` set curl's `-u` logs in with `CRAM-MD5` over plaintext and never
  sends the password in clear. `GSSAPI` checks a Kerberos ticket against the `--keytab` keys
  ([ADR-0057](../Planning/Decisions/ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md)
  decision 9), and `--auth gssapi` without `--keytab` ends surl with 2.
- **SMTP** (`Surl.Protocol.Smtp`,
  [ADR-0053](../Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md)):
  `EHLO` with `SIZE`, `8BITMIME`, `SMTPUTF8`, `PIPELINING`, `ENHANCEDSTATUSCODES`, `STARTTLS` and
  `AUTH`; `MAIL`, `RCPT` (at most 100 per transaction) and `DATA`, each message stored with a
  `Return-Path` and a `Received` trace field in the `INBOX` of every recipient whose local part
  names an account, a recipient that names none answered alike and its copy discarded, and the
  recipient's domain ignored unless a `--mail-domain` is configured (see "Shared stores and
  configuring a real server" below); `VRFY`
  and `EXPN` answered `252` whatever is asked. `MAIL` needs a login unless `--allow-anonymous`
  is given. The body streams into a pending message (a pending file with a data directory) as
  it is read, bounded by `--max-filesize`, and a message file that cannot be written is
  answered `451 4.3.0`.
- **IMAP** (`Surl.Protocol.Imap`,
  [ADR-0055](../Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md)):
  IMAP4rev1 with `SASL-IR`, `UIDPLUS`, `UNSELECT`, `NAMESPACE`, `CHILDREN`, `ID`, `MOVE` and
  `APPENDLIMIT`; `LOGIN` and `AUTHENTICATE`; mailbox listing, `SELECT`, `EXAMINE` and
  `STATUS`; `FETCH` of every data item and section, with its own RFC 5322 and MIME reader;
  `SEARCH` with every RFC 3501 key; `APPEND` streamed into the store; `STORE`, `COPY`, `MOVE`,
  `EXPUNGE`, `CREATE`, `DELETE` and `RENAME`, and the `UID` forms. `IDLE` is not offered: curl
  cannot send its `DONE`. Every command that reads or changes a mailbox needs a login unless
  `--allow-anonymous` is given.
- **POP3** (`Surl.Protocol.Pop3`,
  [ADR-0056](../Planning/Decisions/ADR-0056-how-the-pop3-server-answers-upstream-curl.md)):
  `CAPA`, `USER`/`PASS`, `APOP` (only with `--auth apop`, when the greeting carries a
  timestamp), `AUTH` and `STLS`; the maildrop commands `STAT`, `LIST`, `UIDL`, `RETR`, `TOP`,
  `DELE`, `RSET` and `NOOP` over a view of the owner's `INBOX` fixed at login and held under
  the maildrop lock (a second session gets `-ERR [IN-USE]`); `QUIT` alone removes what `DELE`
  marked. Every maildrop command needs a login unless `--allow-anonymous` is given.
- **TLS and limits:** with `--cert` or `--self-signed`, `smtp://` and `imap://` offer
  `STARTTLS` and `pop3://` `STLS`; without a certificate each is refused in its protocol's
  words. A line past `--max-line` or one not finished within `--head-timeout` is answered and
  closed; the idle timeout and maximum duration are answered `421` by SMTP and `* BYE` by IMAP
  and closed silently by POP3, and shutdown writes no farewell
  ([ADR-0059](../Planning/Decisions/ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md)).

**What pinned upstream curl has proven.** The integration tests in `Surl.Conformance.UnitTests`
run each case against a live `surl` on loopback. With the Windows reference build (curl 8.21.0,
Schannel) every row of ADR-0053 decision 10 over `smtp` and `smtps` passed
(`UpstreamCurlSendsMailToSurlOverSmtpTests`), every row of ADR-0055 decision 15 over `imap` and
`imaps` (`UpstreamCurlReadsMailFromSurlOverImapTests`) and every row of ADR-0056 decision 12
over `pop3` and `pop3s` (`UpstreamCurlReadsMailFromSurlOverPop3Tests`); the IMAP and POP3 rows
read back, byte for byte, mail the same curl first sent to the same `surl` over `smtp`. Those
rows log in with `CRAM-MD5`, `PLAIN`, `LOGIN`, `XOAUTH2`, `OAUTHBEARER`, `DIGEST-MD5`, `NTLM`,
IMAP `LOGIN`, POP3 `USER`/`PASS` and `APOP`. The same tests run on CI's Linux and macOS legs
with the OpenSSL reference builds; no result from them is recorded against these ADRs yet.
`EXTERNAL` is proven by unit tests only. `GSSAPI` is proven with the Windows reference build
through the hand-built loopback test KDC of
[ADR-0065](../Planning/Decisions/ADR-0065-kerberos-logins-are-proved-against-pinned-upstream-curl-through-a-hand-built-loopback-kdc.md)
(`UpstreamCurlLogsInToSurlWithKerberosTests`): the same curl logs in to `surl --keytab` over
`smtp`, `imap` and `pop3`, with and without `--sasl-ir`, and is refused for a principal with no
account. A refused `GSSAPI` ticket's reason (`Kerberos: ticket expired`) is written to the
verbose log.

### Built for Phase 4: WebSocket

`surl` registers the WebSocket server (`Surl.Composition`'s `ComposeProtocolServers`) with its help
category and `--aihelp` topic, `websocket`. `WsProtocolServer` claims `ws` only; `wss` is the
same server behind `ImplicitTlsSchemeServer`, over a connection the engine has already secured
with TLS from the first byte. It is handed the one content store and the one
`AuthenticationPolicy` the HTTP server is. An upgrade is answered only on a `ws://` or `wss://`
listen URL: an `http://` listener answers a request carrying `Upgrade: websocket` as the
ordinary `GET` it also is
([ADR-0070](../Planning/Decisions/ADR-0070-the-http-message-library-the-http-websocket-and-rtsp-servers-share.md)
decision 6). The terms below are defined in the [glossary](../Wiki/Glossary.md), section
"WebSocket".

- **The upgrade** (`Surl.Protocol.Ws`,
  [ADR-0071](../Planning/Decisions/ADR-0071-how-the-websocket-server-answers-upstream-curl.md)
  decisions 1 and 2): the request head is read with `Surl.HttpMessage`'s `HttpConnectionReader`
  within `--max-request-head` and `--head-timeout` (`431`, `408`, `505` or `400`, as the HTTP
  server answers them), then checked in ADR-0071's order, the first failure answering: one
  `Host` (`400`), the login (`401` or `403`), `GET` (`405`, `Allow: GET`), `HTTP/1.1` (`400`),
  `Upgrade: websocket` (`426`), `Connection: Upgrade` (`400`), no body announced (`400`), one
  `Sec-WebSocket-Key` of 16 base64 bytes (`400`), `Sec-WebSocket-Version: 13` (`426`), and a
  request path naming a file, or a directory with `--list-directories`, in the content store
  (`404`). Every refusal has an empty body and closes the connection, but a `401` keeps it. The
  `101 Switching Protocols` carries `Date`, `Server: surl`, `Upgrade`, `Connection` and the
  `Sec-WebSocket-Accept` RFC 6455 requires; no subprotocol or extension is ever selected.
- **The logins** (ADR-0071 decision 3): the upgrade is judged through
  `IHttpAuthenticationSession` as an HTTP `GET` is - no login needed with no account configured,
  one needed once any is, Basic and Bearer over `ws://` refused `403` unchecked without
  `--allow-plaintext-auth` - and a `401` carries the HTTP challenges.
- **What surl sends** (decision 4): the file at the request path as one binary message, or a
  directory's listing as one text message (each name, `/` after a directory, then LF), in frames
  of at most 65536 payload bytes, then an empty `CLOSE` (`88 00`), since curl writes a `CLOSE`'s
  code into its output; a file that cannot be read once upgraded is answered `CLOSE` 1011.
  With `--ws-echo` the path is not looked up, and every complete client message is sent back
  whole with its opcode until the client's `CLOSE`, a protocol error or a limit. surl never
  sends `PING`.
- **How client frames are answered** (decision 6): a `PING` with a `PONG` carrying its payload;
  a `PONG` ignored; a data message discarded unless echoed; a client `CLOSE` with a `CLOSE`
  echoing its code (two bytes, no reason), or an empty one for an empty one; an unmasked frame,
  a reserved bit or opcode, a control frame over 125 bytes or fragmented, a misplaced
  continuation or data frame, a non-minimal length or a `CLOSE` code not allowed on the wire
  with `CLOSE` 1002; text or a `CLOSE` reason that is not UTF-8 with 1007; a frame or message
  past `--max-message` with 1009. The codes allowed on the wire are 1000 to 1003, 1007 to 1014
  and 3000 to 4999 (`WebSocketCloseCodes`).
- **Closing and limits** (decisions 5 and 7): after its `CLOSE` surl half-closes and reads and
  discards what the client still sends for at most one second, never waiting for a `CLOSE`
  answer, which curl never sends. An exchange past `--idle-timeout` or `--max-time` is answered
  `CLOSE` 1001; shutdown writes no farewell
  ([ADR-0059](../Planning/Decisions/ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md)).
- **`wss`** (decision 8): TLS from the first byte with the certificate from `--cert` or
  `--self-signed`, then `ws`'s exchange byte for byte.

**What pinned upstream curl has proven.** The integration tests in `Surl.Conformance.UnitTests`
run each case against a live `surl` on loopback. With the Windows reference build (curl 8.21.0,
Schannel) every tool row of ADR-0071 decision 11 over `ws` and `wss` passed
(`UpstreamCurlTalksToSurlOverWebSocketTests`): a file downloaded byte for byte to stdout and with
`-o`, an empty file, a listing only with `--list-directories`, the `404`, `405`, `401` and `403`
refusals ending curl with 22, logins with Basic, Bearer and AWS Signature Version 4, Digest and
NTLM ending 22 at the `401` (curl answers no challenge on an upgrade), and an echo ended by
surl's `--idle-timeout` or `--max-time` with `CLOSE` 1001. Through the pinned `libcurl-4.dll`,
driven by `Run-LibcurlWebSocketScript.cs` against `--ws-echo`, every row of ADR-0071's
Amendment 1 passed (`PinnedLibcurlTalksToSurlOverWebSocketTests`, Windows only): text, binary
and a three-fragment message echoed whole, a `PING` answered by its `PONG`, `CLOSE` 1000
echoed, a 2 MiB message answered 1009 and invalid UTF-8 1007. The tool tests run on CI's Linux
and macOS legs with the OpenSSL reference builds; no result from them is recorded against
ADR-0071 yet, and the libcurl tests report `Inconclusive` there, since those builds carry no
shared library. A `PING` from the tool (it sends none) and the 1002 answers (libcurl sends no
invalid frame) are proven by the unit tests in `Surl.Protocol.Ws.UnitTests` only, and Negotiate
on the upgrade has no row in decision 11.

### Built for Phase 5: LDAP, SMB and RTSP

`surl` registers the three Phase 5 servers (`Surl.Composition`'s `ComposeProtocolServers`), each with
its help category and `--aihelp` topic, `ldap`, `smb` and `rtsp`. `LdapProtocolServer` claims
`ldap` only, and `ldaps` is the same server behind `ImplicitTlsSchemeServer`; `SmbProtocolServer`
claims `smb` and `smbs` itself, as the FTP server claims `ftps`; `RtspProtocolServer` claims
`rtsp`, and there is no `rtsps`, since upstream curl has none. All three are handed the one
`AuthenticationPolicy`, and the SMB and RTSP servers the one content store. No library
was added for Phase 5: the LDAP server's BER codec is the BCL's `System.Formats.Asn1`
(ADR-0072 decision 9), the SMB codec lives in `Surl.Protocol.Smb` (ADR-0073 decision 10), and the
RTSP server reads and writes its heads with `Surl.HttpMessage` (ADR-0070). The terms below are
defined in the [glossary](../Wiki/Glossary.md), section "LDAP, SMB and RTSP".

- **The LDAP directory** (`Surl.Protocol.Ldap`,
  [ADR-0072](../Planning/Decisions/ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md)
  decision 1): with `--directory`, `<path>/.surl/ldap/directory.ldif` (RFC 2849 LDIF content
  records, UTF-8) is read once at start, after the data-directory lock and before any listener
  binds; a missing file, and in-memory mode, is an empty directory. The directory is read-only:
  surl never writes the file, and LDAP's add, delete, modify and rename are answered
  `unwillingToPerform`. A file that cannot be read or is not LDIF the directory can hold ends surl
  with 37, `surl: (37) Could not read <file>: line <n>: <what>`. It is bounded
  (`LdapDirectory.DefaultMaxEntries`, 100000 entries; 256 MiB; one value at most 1 MiB), and one
  search returns at most `LdapDirectory.DefaultMaxSearchEntries`, 10000, then `sizeLimitExceeded`.
- **LDAP searches** (decisions 1 and 3): base, one-level and subtree scopes; every filter of RFC
  4511, evaluated three-valued with RFC 4517's matching rules chosen by attribute type; attribute
  selection and `typesOnly`; `noSuchObject` with the nearest existing superior; compare; the root
  DSE, computed per connection, listing the SASL mechanisms offered and, with a certificate,
  `StartTLS`. A request the codec cannot read, or an unknown operation, gets the Notice of
  Disconnection and the close.
- **LDAP binds** (decisions 2 and 4): every search but the root DSE's needs a successful bind on
  the connection unless `--allow-anonymous` is given (`insufficientAccessRights` otherwise). A
  simple bind is checked through `IAuthenticationPolicy`, the bind name being the account name or
  a DN's leading `cn` or `uid` value, and is refused `confidentialityRequired` unchecked over
  plaintext without `--allow-plaintext-auth`; a version 2 bind is answered as version 3. Sicily
  NTLM binds (`WinLDAP`'s `--ntlm`) and SASL binds - `GSS-SPNEGO` carrying NTLM, `DIGEST-MD5`, and
  the mail servers' mechanisms - run through `ISaslAuthenticationPolicy`, offered as `--auth`
  accepts them; once a bind negotiates NTLM sealing or signing or `DIGEST-MD5` integrity or
  confidentiality, every later message both ways is one buffer the `ISaslSecurityLayer` protects.
  Kerberos inside `GSS-SPNEGO` is answered with NTLM selected (ADR-0040's rule); BL-327 is to
  decide and build Kerberos there, with RFC 4121 wrap tokens as its security layer.
- **`StartTLS` and `ldaps`** (decision 5): `StartTLS` is offered and accepted only when a
  certificate is configured (`--cert` or `--self-signed`), and refused `protocolError` otherwise;
  `ldaps` is TLS from the first byte, then `ldap`'s exchange.
- **SMB** (`Surl.Protocol.Smb`,
  [ADR-0073](../Planning/Decisions/ADR-0073-how-the-smb-server-answers-upstream-curl-and-checks-its-ntlmv1-session-setup.md)):
  SMB version 1, `NT LM 0.12` without extended security, over NetBIOS session framing, answering
  the eight requests curl sends - negotiate, session setup, tree connect, NT create, read, write,
  close and tree disconnect - with DOS-class statuses (`SmbStatus`). A share is a top-level
  directory of the content store, so `smb://h/docs/a.txt` reads what `http://h/docs/a.txt` reads;
  files at the store's root are not reachable over SMB. Up to 16 trees and 16 open files per
  session. Reads answer at most 61440 bytes each; uploads need `--allow-uploads`, are written at
  each write's offset, answered with exactly the count received, bounded by `--max-filesize` and
  committed by the close. The server keeps answering every request it reads, because curl does not
  notice the server closing while it waits; it closes only after a refused session setup, a
  negotiate without `NT LM 0.12`, a limit or shutdown. `smbs` is TLS from the first byte.
- **The SMB login** (decision 3): curl's only login is an NTLMv1 session setup. It is checked
  through `ISmbAuthenticationPolicy` against the account's NT hash, the LM response ignored and the
  domain not matched, and only when `--auth` names `ntlmv1`, a word that is not in the default set;
  a refusal is `ERRSRV/ERRbadpw` and the close. Under `--allow-anonymous` every session setup is
  accepted as a guest. The NTLMv1 arithmetic is `NtlmV1Calculation` in `Surl.Authentication`, over
  `Surl.Cryptography`'s hand-built `Des` and `Md4`.
- **RTSP** (`Surl.Protocol.Rtsp`,
  [ADR-0074](../Planning/Decisions/ADR-0074-how-the-rtsp-server-answers-upstream-curl.md)):
  RTSP/1.0 heads read with `HttpConnectionReader` for `HttpMessageProtocol.Rtsp10`, judged in
  decision 2's order; every answer to a request carries `Date`, `Server: surl` and, once the
  request's one `CSeq` has been read, that `CSeq`, and only `DESCRIBE`'s has a body. `OPTIONS` answers `Public` with all ten methods; `DESCRIBE` answers
  an SDP describing the file as one stream whose RTP payloads are its bytes; `SETUP` accepts only
  interleaved `RTP/AVP/TCP` (`461` otherwise) and makes the connection's one session, 16
  upper-case hex digits, ending after 60 seconds without a request unless playing or recording;
  `PLAY` streams the file in interleaved RTP packets of 1400 payload bytes, then an RTCP sender
  report and `BYE`; `PAUSE`, `TEARDOWN`, `GET_PARAMETER` and `SET_PARAMETER` as decision 5 says,
  with a torn-down session's ID kept by the connection and reused by a later `SETUP` naming it
  (Amendment 1). `ANNOUNCE` stores its body as `<path>.sdp`, and `SETUP` with `mode=record`,
  `RECORD` and `TEARDOWN` store the RTP payloads the client sends, both only with
  `--allow-uploads` and within `--max-filesize`.
- **The RTSP login** (decision 7): every request is judged through `IHttpAuthenticationSession`
  as an HTTP request is: no login for a read while no account is configured, one for every
  request, `OPTIONS` included, once any is, and always for an upload; Basic and Bearer refused
  `403` unchecked without `--allow-plaintext-auth`; a `401` carrying the HTTP challenges, which
  curl's `--digest` answers on the same connection.
- **Limits** (ADR-0072 decision 6, ADR-0073 decision 7, ADR-0074 decision 8): an LDAP message
  past `--max-message` gets the Notice of Disconnection, and the idle timeout and maximum duration
  the Notice `unavailable`; an SMB message past `--max-message` (at most 131071 bytes) is answered
  `ERRSRV/ERRerror` and the session goes on; an RTSP head past `--max-request-head` or
  `--head-timeout` is answered `431` or `408` and closed, and a connection past the connection
  limits `503`. Shutdown writes no farewell
  ([ADR-0059](../Planning/Decisions/ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md)).

**What pinned upstream curl has proven.** The integration tests in `Surl.Conformance.UnitTests`
run each case against a live `surl` on loopback.

- **LDAP**, with the Windows reference build (curl 8.21.0 over `WinLDAP`): every row of ADR-0072
  decision 10 over `ldap` and `ldaps` passed (`UpstreamCurlSearchesSurlOverLdapTests`):
  searches by scope, filter and attributes, a non-ASCII value in base64, `noSuchObject` (39), the
  10000-entry search bound, the simple bind accepted and refused (38), the plain-text refusal
  (38), `--allow-anonymous`, the Sicily NTLM, `GSS-SPNEGO` and `DIGEST-MD5` binds completed through
  their security layers, the in-memory directory, and `--ssl-reqd` (4). `WinLDAP` checks an
  `ldaps` certificate itself and ignores `-k`, so against surl's throwaway certificate it ends 38
  `Server Down` (60 without `-k`): no test proves an `ldaps` search succeeding. These tests run the
  platform's reference build only and are Inconclusive on Linux and macOS, whose reference builds
  list no `ldap`. **LDAP over the OpenLDAP-backed upstream curl is not yet proven**: ADR-0076 pins
  that build (`linux-x64`, supplementary, built reproducibly in CI) and measured what its
  `lib/openldap.c` sends - `STARTTLS`, the root-DSE `supportedSASLMechanisms` search, SASL binds
  through curl's own SASL code and the anonymous bind, none of which `WinLDAP` sends - but the tests
  that run it against `surl` are BL-312's and not yet written. SASL `GSSAPI` over LDAP is
  unmeasured, since that build has no GSS-API (BL-342).
- **SMB**, with ADR-0030's static-curl Windows build (the reference build has no `smb`): every
  row of ADR-0073 decision 11 over `smb` and `smbs` passed
  (`UpstreamCurlTransfersFilesWithSurlOverSmbTests`): downloads, a domain in `-u` ignored, the
  wrong password and user-name case (67), missing shares, files, hidden paths and directories
  (78), uploads refused (9), stored, and past `--max-filesize` (25), `ntlmv1` not accepted (67),
  `--allow-anonymous`, and `smbs` with and without `-k` (0, 60). The same tests run on CI's Linux
  and macOS legs with the reference builds, which list `smb`; no result from them is recorded
  against ADR-0073 yet.
- **RTSP**, with the Windows reference build: every tool row of ADR-0074 decision 11 passed
  (`UpstreamCurlTalksToSurlOverRtspTests`): the `OPTIONS *` answer and its head under `-i`, the
  upload that sends no body, the `401`, Basic and Bearer refused `403` in plain text and accepted
  with `--allow-plaintext-auth`, Digest on one connection, `--anyauth`, `--allow-anonymous` and the
  `431`. Through the pinned `libcurl-4.dll`, every libcurl row of ADR-0074's Amendment 1 passed
  (`PinnedLibcurlTalksToSurlOverRtspTests`, Windows only): `DESCRIBE`'s SDP, a `SETUP` session,
  `PLAY` packets joining to the file then the RTCP `BYE`, `PAUSE`, `GET_PARAMETER`, `TEARDOWN`
  and a second `SETUP` on the same ID, the `461`, `454`, `455`, `451`, `401` and `403` refusals,
  and `ANNOUNCE` and `RECORD` storing their files. The tool tests run on CI's Linux leg with the
  reference build; no result from it is recorded against ADR-0074 yet. On macOS, whose pinned
  build has no `rtsp`, they report Inconclusive
  ([ADR-0026](../Planning/Decisions/ADR-0026-rtsp-conformance-on-macos-where-the-pinned-build-has-no-rtsp.md)).
  NTLM and Negotiate over RTSP have no row in decision 11.

### Built for Phase 6: HTTP/2, HTTP/3, upstream's test cases and native publishing

Phase 6 has four parts: `surl` serves HTTP/2 and, with `--http3`, HTTP/3 over a QUIC it builds
by hand; `Surl.Conformance` plays the server half of upstream curl's own test cases; and a
published native `surl` is proven on six runtime identifiers. The terms below are defined in
the [glossary](../Wiki/Glossary.md), sections "HTTP/2", "HTTP/3 and QUIC", "Upstream test
cases" and "Native publishing". Phase 6 is built, not closed: "What is not proven" below lists
what is open, and no release is tagged.

**HTTP/2** ([ADR-0081](../Planning/Decisions/ADR-0081-how-surl-serves-http-2.md), with
[ADR-0090](../Planning/Decisions/ADR-0090-how-the-http-2-prior-knowledge-connection-reads-ends-and-answers.md)
and [ADR-0091](../Planning/Decisions/ADR-0091-how-an-upgrade-h2c-request-becomes-http-2-stream-1.md);
the frames, streams and connection live in `Surl.Http2` since
[ADR-0086](../Planning/Decisions/ADR-0086-how-the-extended-servers-share-http-2-http-3-and-quic.md)):

- **How a connection becomes HTTP/2.** An `https` listener offers ALPN `h2`, then `http/1.1`
  (`TlsApplicationProtocols`), and serves HTTP/2 on a connection that agreed `h2`; `wss` offers
  only `http/1.1`. The `http` listener serves HTTP/2 when the connection opens with the 24-byte
  connection preface (prior knowledge, `Http2ConnectionPreface`), and upgrades a body-less
  `Upgrade: h2c` request: `101 Switching Protocols`, then the request is stream 1
  (`Http2CleartextUpgrade`). No `surl` option turns HTTP/2 off; the client chooses, and curl's
  `--http1.1` offers only HTTP/1.1.
- **What it answers.** The same answers as HTTP/1.x: `HttpRequestAnswerer` decides, and
  `Http2StreamAnswerWriter` writes them as `HEADERS`, `CONTINUATION` and `DATA` frames, so the
  content store, request-bound logins and uploads carry over per stream. A stream whose login
  is NTLM or Negotiate, which are bound to a connection, is reset `HTTP_1_1_REQUIRED` and curl
  retries it over HTTP/1.1. The server's `SETTINGS` carry `MAX_CONCURRENT_STREAMS` 100 and
  `MAX_HEADER_LIST_SIZE`, and each limit of ADR-0006 ends with the `GOAWAY` or `RST_STREAM` code
  ADR-0081 decision 5 names. HPACK is hand-built in `Surl.Hpack`. There is no server push and no
  WebSocket over HTTP/2.

**HTTP/3** ([ADR-0082](../Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md) and its
amendments; [ADR-0096](../Planning/Decisions/ADR-0096-how-a-surl-quic-connection-carries-streams-and-flow-control.md)
for streams and flow control and
[ADR-0140](../Planning/Decisions/ADR-0140-how-surl-quic-detects-loss-and-controls-congestion.md)
for loss detection and congestion control):

- **How it is turned on.** `surl --http3 https://...` also listens on UDP at the `https`
  listener's address and port; it is off without `--http3`, and the status line still names one
  port. `--http3` with no `https` listen URL, or with `--tls-max` below 1.3, exits 2. With
  `--cacert`, the QUIC handshake requires a client certificate that chains to those anchors, as
  the TCP listener does (ADR-0082 Amendment 5). With `--http3`, every answer over HTTP/1.x and
  HTTP/2 carries `Alt-Svc: h3=":<port>"; ma=86400`.
- **What is hand-built.** QUIC version 1 (`Surl.Quic`), the TLS 1.3 handshake for it
  (`Surl.Tls13`), QPACK (`Surl.Qpack`), ML-KEM (`Surl.Cryptography.MlKem`), and for the key
  exchange groups curl offers only when told `--curves` one alone (ADR-0147) x448
  (`Surl.Cryptography.Curve448`) and curveSM2 (`Surl.Cryptography.Sm2`); never `System.Net.Quic`.
  `TlsServerNegotiation.GroupPreference` serves twelve groups: the five of ADR-0082 decision 3,
  then the seven of ADR-0147. ALPN is `h3` only; there are no session tickets, no 0-RTT, no
  Retry and no connection migration.
- **What it answers.** The same answers as HTTP/1.x and HTTP/2 through `HttpRequestAnswerer`
  and `Http3StreamAnswerWriter`; `Http3ProtocolServer` is the protocol server and
  `Http3ConnectionServer` (in `Surl.Http3`) serves each connection. A stream whose login is
  NTLM or Negotiate is reset `H3_VERSION_FALLBACK`.

**Upstream's test cases** ([ADR-0080](../Planning/Decisions/ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md)):
`Surl.Conformance` parses each of the 2,013 cases at tag `curl-8_21_0`, skips each one a
platform's pinned reference build cannot run with one named reason, and runs the rest with
that build as the client against Surl's own protocol server for the case's scheme
(`UpstreamTestCasesAgainstSurlTests`, categories `Integration` and `UpstreamTestCases`). The
server halves are composed in the test project, never in `Surl.Composition`, so the shipped
`surl` has no replay mode. Server halves exist for the HTTP and HTTPS family (including
`http-unix`), RTSP, FTP and FTPS, IMAP, POP3 and SMTP in plaintext, DICT, Gopher, TELNET, MQTT,
TFTP, SCP, SFTP and SMB. The table in `UpstreamTestCaseServers` marks `http/2`, `http/3`,
the proxies, `httptls+srp` and `dns` as not built, so those cases skip as
`server-not-built:<name>`. The pass rate, stated per platform and per release, is in
[Upstream-Test-Case-Pass-Rate.md](../Conformance/Upstream-Test-Case-Pass-Rate.md), the passing
cases are listed beside it, and a fast test fails when the table and the lists disagree, so
the rate only rises. As of 2026-11-12:

| Platform | Runnable | Passed | Failed | Skipped | Pass rate |
| --- | ---: | ---: | ---: | ---: | ---: |
| `win-x64` | 1,128 | 1,078 | 50 | 885 | 95.6% |
| `linux-x64` | 1,237 | 1,154 | 83 | 776 | 93.3% |
| `osx-arm64` | 1,237 | 1,153 | 84 | 776 | 93.2% |

The Windows row is from a local run on 2026-11-11 (CI has no pinned Windows reference build);
the Linux and macOS rows are CI run 42203619501 of 2026-11-06. The pass-rate file's
"Releases" table has no row, because no release is tagged.

**What pinned upstream curl has proven for HTTP/2 and HTTP/3.**
`UpstreamCurlFetchesFromSurlOverHttp2Tests` (15 tests) and
`UpstreamCurlFetchesFromSurlOverHttp3Tests` (13 tests) run the static-curl 8.21.0 build
(ADR-0081 decision 1, ADR-0082 decision 1) on Windows and the reference build elsewhere
against a live `surl`. They cover prior knowledge, the `h2c` upgrade, ALPN, `-I`, a range,
Basic and Digest, an upload under `--allow-uploads`, a file larger than the initial window (a
10 MiB file over HTTP/3), two URLs reusing one connection, and, for HTTP/3, `--http3-only` and
`--http3` with HTTP/3 off (the exit code, and the fall back to HTTP/2). They passed on Windows
when the tasks were done (BL-371, BL-385).

**Native publishing** ([ADR-0085](../Planning/Decisions/ADR-0085-what-proves-the-native-surl-on-each-published-runtime-identifier.md)):
every leg of `.github/workflows/release.yml` publishes `surl` natively and runs
`Test-PublishedSurl.ps1` on a runner of its own operating system and architecture: one native
file and no `runtimeconfig.json` or `*.dll`, `--version` exits 0 naming the version and the
runtime identifier, one file served byte for byte, a stop on SIGTERM, and no .NET shown by an
`ubuntu:22.04` container on Linux or a scrubbed `DOTNET_*` and `PATH` plus import checks
elsewhere. `Surl.Conformance` runs against the published binary when `SURL_EXECUTABLE` names
it. A release dry run on 2026-11-06 (CI run 42225649001, BL-603) was green on all six legs.

| Runtime identifier | Runner | Proven by | Upstream curl completes an exchange with the binary |
| --- | --- | --- | --- |
| `linux-x64` | `ubuntu-22.04` | the smoke test and the conformance job | yes, in CI |
| `osx-arm64` | `macos-latest` | the smoke test and the conformance job | yes, in CI |
| `win-x64` | `windows-latest` | the smoke test; conformance runs locally only (CI has no pinned Windows reference build) | locally; a clean full run is open (BL-604, BL-1059) |
| `win-arm64` | `windows-11-arm` | the smoke test and the fast tests | not shown: no pinned upstream curl build (BL-361) |
| `linux-arm64` | `ubuntu-22.04-arm` | the smoke test and the fast tests | not shown: no pinned upstream curl build (BL-361) |
| `osx-x64` | `macos-15-intel` | the smoke test and the fast tests | not shown: no pinned upstream curl build (BL-361) |

**What is not proven.**

- No release is tagged, so there are no downloadable binaries and no release row in the
  pass-rate file.
- `win-arm64`, `linux-arm64` and `osx-x64` have no pinned upstream curl build, so they have the
  smoke test and the fast tests but neither conformance nor a pass rate. BL-361 would pin
  them; it is Blocked until arm64 and Intel-macOS runners can measure the builds.
- `win-x64` conformance against the published binary runs only locally. BL-604 found the start
  timeouts were thread-pool starvation in the test host; a clean full run still waits on
  BL-1059 (the HTTP/3 UDP bind when the TCP port drawn sits in a Windows UDP excluded range).
- The HTTP/2 and HTTP/3 conformance tests are recorded passing on Linux and macOS only by
  elimination from CI run 42324374116, since the CI logger prints a skipped Curl-port test of
  the same name; BL-724 and BL-768 are to confirm them.
- No upstream test case runs over HTTP/2 or HTTP/3 yet: their server halves are not built.
- Some behaviour the ADRs ask to be measured is unmeasured: for HTTP/2 the `413`,
  `FRAME_SIZE_ERROR`, `FLOW_CONTROL_ERROR` and idle `GOAWAY` cases (BL-371's notes), and for
  HTTP/3 curl past each decision 7 limit and `Alt-Svc` with `--alt-svc` (BL-385's notes).

### Built for Phase 7: the Curl port measured beside pinned upstream curl

Phase 7 measures the Curl port; it does not validate Surl
([ADR-0003](../Planning/Decisions/ADR-0003-upstream-curl-is-surls-only-oracle.md) decision 6,
[ADR-0089](../Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md)).
The port's `curl` and the pinned upstream build run the same conversation, each against its own fresh
`surl`, and the two results are compared after the normalisation of ADR-0089 decision 4. Where upstream
curl completed the exchange as the mirrored conformance test asserts and the port's run differs, the
verdict is a **port defect**; it is written as a **Phase 7 finding**, and Surl does not change to make the
port pass. The terms are defined in the [glossary](../Wiki/Glossary.md), section "Two programs named curl".

- **Measured:** the Curl port at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (`curl.exe` SHA-256
  `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`, pinned in `CurlPortBuilds.json`),
  on `win-x64` only, on 2026-11-05, 2026-11-06 and, for the extended clients, up to 2026-11-11.
- **Result:** 1,518 cases, 1,420 agreements, 96 port defects behind 18 findings, 2 cases not
  comparable, and no Surl defect. The per-family numbers, the Surl commit each family was measured at
  and the findings themselves are in
  [`Documentation/Conformance/CurlPort/README.md`](../Conformance/CurlPort/README.md) and the family
  files beside it.
- **Harness** (`Surl.Conformance`): `CurlPortClientPins` and `CurlPortClientLocator` find the pinned port
  binary, `CurlPortClientRunner` runs it, `CurlPortResultNormaliser` and `CurlPortComparison` compare
  the runs, `CurlPortVerdict` and `CurlPortFinding` record the outcome, `CurlPortLibcurlComparison`
  does the same for the libcurl rows, and `CurlPortTestCaseClient` lets the port play the client of
  upstream's own test cases. One `CurlPortBesideUpstreamCurlOver<Family>Tests` class per family reads its
  family file's findings table, so a finding that appears unlisted or a listed one that disappears
  fails the test.
- **Handoff:** [`Port-Board-Handoff.md`](../Conformance/CurlPort/Port-Board-Handoff.md) holds the 18
  findings as tasks in the port's own task shape. Stewart files them on the port's board himself; no
  Surl session writes into the port's repository (ADR-0089 decision 7).

**What is not measured.**

- Every platform but `win-x64`: `CurlPortBuilds.json` pins the port for `win-x64` alone, so off Windows
  every Phase 7 test is Inconclusive (ADR-0089 decision 8).
- The 47 `OpenLdap_` cases, Inconclusive until a `linux-x64` port pin exists, and the cases excluded
  on Windows by their `[OSCondition]` (6 SSH cases, 3 FTP listing twins).
- The extended clients beyond `socketio`, `webtransport`, `coap` and `xmpp`: the other 15 `Integration`
  tasks have not run (`Extended.md`). They are judged by their specifications, not beside upstream
  curl (ADR-0089 decision 9).
- Phase 7 is a snapshot at one port commit; a later port commit needs a new measurement.

### Shared stores and configuring a real server

Beyond answering one request at a time, `surl` can be a server that several protocols share and
an operator configures. The terms are defined in the [glossary](../Wiki/Glossary.md), section
"Shared stores and the configuration command line".

- **One store per job, per `surl` run**
  ([ADR-0100](../Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decision 1). Files are the one `ContentStore`, handed to every file-serving server, so a file
  uploaded over FTP is fetched over HTTP, SFTP, SMB or `s3`. Mail is the one `MailboxStore`, so
  mail sent over SMTP is read over IMAP and POP3. Messages are the one `MessageBus`: today
  `mqtt`, `mqtts`, `nats`, `natss`, `stomp` and `stomps` are registered on it, so a message
  published over `mqtt://` is consumed over NATS or STOMP, and `mqtt5` keeps no retained
  messages of its own (decision 3.9). The `mqtt5`, `amqp`, `amqp1`, `kafka` and `redis`
  libraries are written against the bus, and registering each in `surl` is intent: BL-975,
  BL-507, BL-514, BL-973 and BL-492. Accounts are the one `AccountBook` every login reads.
- **The configuration command line**
  ([ADR-0101](../Planning/Decisions/ADR-0101-surls-configuration-surface-the-config-file-repeatable-item-options-and-one-configuration-model.md)
  for the surface, [ADR-0102](../Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  for what each option means). `-K`/`--config <file>` reads options from a file in curl's
  config-file syntax, and `--url <listen URL>` gives a listen URL in it. Repeatable item
  options configure the stores: `--mail-domain`, `--mailbox`, `--mailbox-file`, `--mail-alias`,
  `--mail-catch-all` and `--shared-folder` for mail; `--share`, `--share-access` and `--bucket`
  for files; `--topic`, `--queue`, `--topic-map` and `--topic-access` for messages; and
  `--group` for accounts. `surl` reads no config file it is not told to, and a surl given none
  of these options behaves as before (ADR-0102 decision 0). Anything that widens access
  (`--mail-catch-all`, and `@everyone` in `--share-access` or `--topic-access`) warns at every
  start. `surl --aihelp mail`, `messages`, `content`, `auth` and `surl` list each option; the
  README holds a worked config file.
- **The hosts.** `Surl.Api` takes the same items as `SurlServerOptions` properties and
  `ConfigFiles` (ADR-0101 decision 2, BL-668). `surl-mcp`'s `start_server` takes `surl`'s own
  options, so the same items. Both are documented in the README's "Using Surl from a test suite
  or an AI agent" and in `Application/Surl.Api/README.md` and `Application/Surl.Mcp/README.md`.
- **Not built yet, stated as intent.** LDAP as an optional source of accounts is decided by
  BL-671 and built by BL-672 and BL-673. The Curl port's own clients reading the shared files
  and messages are BL-669 and BL-670; they run in `Integration` tests only and prove nothing
  (ADR-0083). Test control of the stores - seeding, resetting, snapshotting and restoring them,
  the store change events and `--fault` - is documented in
  `Documentation/Wiki/Surl-As-A-Stand-In.md`.

### Also in scope

- **HTTP versions:** 1.0, 1.1, 2 and 3 over QUIC, on the server side.
- **Authentication:** issuing the challenge and verifying the answer for every scheme
  upstream curl sends, secure by default
  ([ADR-0032](../Planning/Decisions/ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)):
  with no account configured every login is refused, a password or token sent in clear
  over an unencrypted connection is refused unchecked, and each of the five loosening
  options (`--allow-anonymous`, `--allow-plaintext-auth`, `--auth`, `--self-signed`,
  `--throwaway-hostkey`) warns on every start it takes effect in. Accounts come from
  `-u`/`--user` and `--user-file`. Built today:
  HTTP Basic, Bearer, Digest (MD5, SHA-256 and SHA-512-256), NTLM (NTLMv2), Negotiate
  carrying NTLM (bare or in SPNEGO) and AWS Signature Version 4; the MQTT `CONNECT`
  user name and password; FTP `USER`/`PASS`, whose password is a plain-text secret on `ftp://`
  before `AUTH TLS` (ADR-0052 decision 3); SSH `password`, `keyboard-interactive` and
  `publickey` logins, the last against `--authorized-keys`, through `ISshAuthenticationPolicy`
  (ADR-0051 decisions 6 and 7); and the mail servers' logins through
  `IMailAuthenticationPolicy` (ADR-0049): the SASL mechanisms `GSSAPI` (a Kerberos ticket
  checked against `--keytab`, ADR-0057 decision 9), `DIGEST-MD5`, `CRAM-MD5`, `NTLM`,
  `OAUTHBEARER`, `XOAUTH2`, `PLAIN`, `LOGIN` and `EXTERNAL` (the TLS client certificate
  `--cacert` verifies), IMAP `LOGIN`, POP3 `USER`/`PASS` and POP3 `APOP`; the WebSocket
  upgrade's, judged through `IHttpAuthenticationSession` as an HTTP `GET` and challenged with the
  HTTP challenges (ADR-0071 decision 3), of which upstream curl completes only the ones it sends
  unasked - Basic, Bearer and AWS Signature Version 4 - since it answers no `401` on an upgrade;
  the LDAP binds (ADR-0072 decisions 2 and 4): the simple bind through `IAuthenticationPolicy`,
  a plain-text secret on `ldap://` before `StartTLS`, and `WinLDAP`'s Sicily NTLM bind and the
  SASL binds - `GSS-SPNEGO` carrying NTLM, `DIGEST-MD5` and the mail servers' mechanisms -
  through `ISaslAuthenticationPolicy`, with the NTLM and `DIGEST-MD5` security layers; SMB's
  NTLMv1 session setup through `ISmbAuthenticationPolicy`, accepted only when `--auth` names
  `ntlmv1` (ADR-0073 decision 3); and RTSP's, judged through `IHttpAuthenticationSession` as an
  HTTP request with the HTTP challenges, which upstream curl's `--digest` answers on the same
  connection (ADR-0074 decision 7). Kerberos inside Negotiate is built for HTTP
  ([ADR-0064](../Planning/Decisions/ADR-0064-kerberos-inside-negotiate-the-choices-adr-0057-decision-8-left-open.md));
  inside LDAP's `GSS-SPNEGO` it is in scope, not built yet (BL-327), as is `Proxy-Authenticate`
  for the proxies.
- **Proxies:** acting as the HTTP `CONNECT` proxy, HTTPS proxy and SOCKS4, SOCKS4a,
  SOCKS5 and SOCKS5h server that curl's proxy options talk to.
- **TLS on the server side:** certificates and keys, client-certificate verification for
  curl's `--cert`, ALPN.
- **Cookies:** setting cookies with every attribute curl parses, and recording what curl
  sends back.
- **Scripted exchanges:** the server half of upstream curl's own test cases
  (`Surl.Conformance`), so curl's suite, not Surl's, decides what correct means.
- **Seeing the exchange:** curl-shaped output from the server's side
  ([ADR-0033](../Planning/Decisions/ADR-0033-console-log-levels-trace-dumps-and-the-log-file.md)).
  Built today: five log levels - `none` (`-s`), `error` (`-s -S`), `info` (the default,
  one line per exchange), `verbose` (`-v`) and `trace` (`--trace` and `--trace-ascii`
  dumps in curl's layout) - with `--log-level`, `--trace-time` and an appended
  `--log-file`; and curl-style help, `--help` with categories, `--help <option>` and
  `--manual`
  ([ADR-0034](../Planning/Decisions/ADR-0034-curl-style-help-categories-and-the-manual.md)).
  Beside it, `--aihelp [topic]`: Markdown help for an AI agent learning to call surl - an
  overview, one page per topic (every help category, plus `exit-codes` and
  `listen-urls`) or `all` - generated from the same option table and categories as
  `--help`. It is a deliberate addition with no upstream curl equivalent: curl 8.21.0
  refuses `--aihelp` as an unknown option
  ([ADR-0046](../Planning/Decisions/ADR-0046-surl-aihelp-markdown-help-for-ai-agents.md)).
  In scope, not built yet: `-w` style output per exchange.

The command-line surface - which of curl's option names carry a server-side meaning and
what each does, how listen URLs are read, and the exact text surl prints - is decided in
[ADR-0007](../Planning/Decisions/ADR-0007-the-phase-1-command-line-surface.md).

## Architecture

Two rules carry the design, the same two the Curl port is built on, turned around.

**Rule 1 - protocol servers depend on abstractions, never on each other.** A protocol
server references `Surl.Protocol.Abstractions` and the horizontal libraries ADR-0002
lists, as later ADRs amend it (`Surl.Content`, `Surl.Cryptography`, the four SSH primitive
libraries of ADR-0048, the two of ADR-0051 decision 3 and the three of ADR-0061, and the mail
servers' `Surl.MailStore` and `Surl.LineProtocol` of ADR-0050, the messaging servers'
`Surl.MessageBus` of ADR-0100, and the HTTP, WebSocket and RTSP
servers' `Surl.HttpMessage` of ADR-0070, and the HTTP server's HTTP/2 and HTTP/3 libraries
of ADR-0081, ADR-0082 and ADR-0086); referencing another protocol
server is a build break, and `Surl.Protocol.Abstractions.UnitTests` asserts the reference graph.
The same rule holds the extended servers (`Surl.Extended.Protocol.*`, ADR-0083), which never
reference an original server either. What they share with one is decided into three more
horizontal libraries by
[ADR-0084](../Planning/Decisions/ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md),
all three built: `Surl.HttpServing` (HTTP/1.1 request answering: bodies, response writing,
keep-alive, draining, the lingering close and the `IHttpRequestHandler` shape; BL-407),
`Surl.WebSocket` (RFC 6455 handshake, framing and closing; BL-408) and the hand-built
`Surl.Dtls` (DTLS 1.2 for `coaps`; BL-412 to BL-415).

**Rule 2 - the transport is an injected seam.** A protocol server receives an accepted
connection (or a datagram channel, for TFTP) from a listener seam; it never constructs a
`Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`. Its tests replay the
request bytes measured from pinned upstream curl through a fake connection, with no
network. Only `Surl.Networking` constructs those types.

The listener seam and the exchange context are decided in
[ADR-0004](../Planning/Decisions/ADR-0004-the-listener-seam-and-the-exchange-context.md):
a protocol server implements `IConnectionProtocolServer` (or `IDatagramProtocolServer`
for TFTP), receives an `IConnection` (or an `IDatagramFlow`) and an `ExchangeContext`,
and its tests replay byte scripts through `InMemoryConnection`.

TLS on the server side uses the base class library's `SslStream`. How a protocol server
receives a secured connection or upgrades one, where the certificate comes from, ALPN and
client-certificate verification are decided in
[ADR-0010](../Planning/Decisions/ADR-0010-the-server-side-tls-contract.md). What the BCL has no
primitive for on some platform - QUIC for HTTP/3, the SSH ciphers - is built by hand, each
in its own `Surl.<Area>.UnitLibrary` (`CLAUDE.md`, "Decisions").

### Layers

| Layer | Projects | Depends on |
| --- | --- | --- |
| Hosts | `Surl.Console` (the `surl` executable), `Surl.Api` (the in-process .NET programming interface, ADR-0099) and `Surl.Mcp` (the `surl-mcp` executable, ADR-0103), over `Surl.Composition`; the hosts are composition roots, and no protocol server or horizontal library may reference one | `Surl.Composition`, `Surl.Cli`, and for `Surl.Mcp` also `Surl.Mcp.UnitLibrary` and `Surl.McpServing` |
| Composition | `Surl.Composition` (turns a parsed command line into running protocol servers, the stores they share and the event stream; the one start `surl`, `Surl.Api` and `Surl.Mcp` all run, ADR-0099) | `Surl.Cli`, `Surl.Core`, the protocol servers and the services below |
| MCP serving | `Surl.McpServing` (JSON-RPC over a line stream, the MCP session, tool and resource registries; shared by `Surl.Mcp` and the extended MCP server, ADR-0103), in `Protocols/Http/`; `Surl.Mcp.UnitLibrary` (the tools and the running-server book) | `Surl.McpServing`: nothing; `Surl.Mcp.UnitLibrary`: `Surl.Api`, `Surl.Cli`, `Surl.McpServing` |
| Control endpoint | `Surl.Control` (the loopback HTTP and JSON endpoint of `--control`, ADR-0124 decision 7; the library is built, and no host serves it yet, BL-816) | Abstractions, `Surl.HttpServing` |
| Command line | `Surl.Cli` | `Surl.Core`, `Surl.Output`, Abstractions |
| Serving engine | `Surl.Core` | Abstractions |
| Protocol servers | `Surl.Protocol.<Name>` (15) | Abstractions, and the horizontal libraries of ADR-0002's table where needed: `Surl.Content`, `Surl.Cryptography` and its SSH primitives, for SMTP, IMAP and POP3 `Surl.MailStore` and `Surl.LineProtocol`, and for HTTP, WebSocket and RTSP `Surl.HttpMessage` (ADR-0070) |
| Services | `Surl.Networking`, `Surl.Authentication`, `Surl.Cookies`, `Surl.Output`, `Surl.Content` | Abstractions; `Surl.Authentication` also `Surl.Cryptography`, for MD4 and SHA-512/256 (ADR-0032 decision 7) and for the DES of SMB's NTLMv1 (ADR-0073 decision 10), `Surl.Cryptography.Rc4`, for NTLM's key exchange and the LDAP security layers (ADR-0072 decision 4), and `Surl.Kerberos` (ADR-0057 decision 6) |
| Mail servers' shared libraries | `Surl.MailStore` (the mail store: mailboxes per account, messages with UIDs, bounds, persistence under `<path>/.surl/mail`) and `Surl.LineProtocol` (bounded CRLF command lines, dot-stuffing, the `STARTTLS` discard, SASL continuation lines), decided by [ADR-0050](../Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md) | Abstractions; `Surl.MailStore` also `Surl.Content`, for `IContentFileSystem` |
| Message bus | `Surl.MessageBus` (the one `MessageBus` of a `surl` run: live fan-out, retained messages, named queues and topic logs over canonical topics, with the declared topics, queues, topic maps and topic access of `--topic`, `--queue`, `--topic-map` and `--topic-access`; persisted under `<path>/.surl/bus`), in `Protocols/Messaging/`, beside the content store and the mail store as the third store the servers of one job share, used by `Surl.Protocol.Mqtt` and by the extended NATS and STOMP servers `surl` registers, decided by [ADR-0100](../Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md) decision 3 | Abstractions and `Surl.Content`, for `IContentFileSystem` |
| HTTP message library | `Surl.HttpMessage` (the bounded HTTP/1.x request-head reader and its head timeout, request-line and field-line parsing for `HTTP/1.x` and `RTSP/1.0`, response heads and `WWW-Authenticate` challenge fields), used by `Surl.Protocol.Http`, `Surl.Protocol.Ws` and `Surl.Protocol.Rtsp`, decided by [ADR-0070](../Planning/Decisions/ADR-0070-the-http-message-library-the-http-websocket-and-rtsp-servers-share.md) | Abstractions |
| HTTP serving and WebSocket libraries | `Surl.HttpServing` (answering HTTP/1.1 requests on a connection: `HttpRequest`, `HttpAnswer`, request bodies with `100 Continue`, response writing, keep-alive, draining, the lingering close, `IHttpRequestHandler` and `Http1ConnectionServer`; BL-407) and `Surl.WebSocket` (RFC 6455 upgrade checks and `101` answer with an optional subprotocol, framing, reassembly, close codes, lingering close; BL-408), in `Protocols/Http/`, for `Surl.Protocol.Http`, `Surl.Protocol.Ws` and the extended servers, decided by [ADR-0084](../Planning/Decisions/ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md) | Abstractions and `Surl.HttpMessage`; `Surl.HttpServing` also `Surl.Content` |
| Hand-built DTLS | `Surl.Dtls` (DTLS 1.2 server for `coaps`), in `Cryptography/`, decided by [ADR-0084](../Planning/Decisions/ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md) decision 3; BL-412 to BL-415 | Abstractions, for `IDatagramFlow` |
| HTTP/2 and HTTP/3 libraries | `Surl.Hpack` (HPACK, RFC 7541), `Surl.Http2` (frames, streams, flow control and the connection; [ADR-0086](../Planning/Decisions/ADR-0086-how-the-extended-servers-share-http-2-http-3-and-quic.md) decision 1), `Surl.Qpack` (QPACK, RFC 9204), `Surl.Quic` (QUIC version 1 for a server: packets, protection, streams, flow control, loss detection and congestion control, no I/O; [ADR-0082](../Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md) decision 2) and `Surl.Http3` (HTTP/3 frames, the control and QPACK streams and the connection; ADR-0086 decision 2), in `Protocols/Http/`; used by `Surl.Protocol.Http` and, for HTTP/2 and HTTP/3, by the extended servers | `Surl.Hpack`: nothing; `Surl.Http2`: Abstractions and `Surl.Hpack`; `Surl.Qpack`: `Surl.Hpack`; `Surl.Quic`: Abstractions, `Surl.Tls13`, `Surl.Cryptography.ChaCha20` and `Surl.Cryptography.Poly1305`; `Surl.Http3`: Abstractions, `Surl.Qpack` and `Surl.Quic` |
| Hand-built TLS 1.3 for QUIC | `Surl.Tls13` (the TLS 1.3 server handshake without a record layer, ADR-0082 decision 2) and the key exchange primitives it uses: `Surl.Cryptography.MlKem` (ML-KEM-768 and ML-KEM-1024), `Surl.Cryptography.Curve448` (x448) and `Surl.Cryptography.Sm2` (curveSM2), the last two decided by [ADR-0147](../Planning/Decisions/ADR-0147-the-remaining-tls-1-3-groups-curl-offers-over-quic.md), in `Cryptography/` | `Surl.Tls13`: `Surl.Cryptography.Curve25519`, `Surl.Cryptography.Curve448`, `Surl.Cryptography.MlKem` and `Surl.Cryptography.Sm2`; the other three: nothing |
| Hand-built primitives | `Surl.Cryptography`; for SSH, `Surl.Cryptography.ChaCha20`, `Surl.Cryptography.Curve25519`, `Surl.Cryptography.Ed25519` and `Surl.Cryptography.Poly1305` ([ADR-0048](../Planning/Decisions/ADR-0048-the-hand-built-ssh-primitive-libraries.md)), `Surl.Cryptography.Rc4` and `Surl.Cryptography.BcryptPbkdf` ([ADR-0051](../Planning/Decisions/ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md) decision 3), and `Surl.Cryptography.Blowfish`, `Surl.Cryptography.Cast128` and `Surl.Cryptography.Ripemd160` ([ADR-0061](../Planning/Decisions/ADR-0061-blowfish-cast-128-and-ripemd-160-for-curls-openssl-builds.md)); for Kerberos, `Surl.Kerberos` ([ADR-0057](../Planning/Decisions/ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md)) | nothing; `Surl.Cryptography.Ed25519` references `Surl.Cryptography.Curve25519`, and `Surl.Cryptography.BcryptPbkdf` references `Surl.Cryptography.Blowfish` |
| Test fixtures | `Surl.Kerberos.TestKdc`, the hand-built loopback KDC for realm `SURL.TEST` ([ADR-0065](../Planning/Decisions/ADR-0065-kerberos-logins-are-proved-against-pinned-upstream-curl-through-a-hand-built-loopback-kdc.md) decision 1). Not a protocol server and not a horizontal library of ADR-0002's table: only test code references it - its own test project, `Surl.Conformance.UnitTests` (ADR-0065 decision 1 allows test projects to) and the `Run-KerberosTestKdc.cs` file-based app behind `Record-CurlExchange.ps1 -KerberosTestKdc` - and `Surl.Console` never does | `Surl.Kerberos`, Abstractions |
| Contracts | `Surl.Protocol.Abstractions` | nothing |
| Upstream's test cases | `Surl.Conformance` | Abstractions |

## Project layout

Every project sits in a category folder - `Application/`, `Conformance/`, `Cryptography/`,
`Foundation/` and, for the protocol servers and the libraries they share, `Protocols/` with
`DataStore/`, `FileTransfer/`, `Http/`, `Lookup/`, `Mail/`, `Messaging/`, `Proxy/` and `Terminal/` - each
production project beside its `.UnitTests` twin ([ADR-0079](../Planning/Decisions/ADR-0079-projects-are-grouped-in-category-folders.md);
`CLAUDE.md`, "Repository layout"). `DataStore/` is decided by
[ADR-0095](../Planning/Decisions/ADR-0095-the-protocols-datastore-category-folder.md) for the
Redis server and is created with its first project (BL-488). `Proxy/` is decided by
[ADR-0121](../Planning/Decisions/ADR-0121-how-surl-serves-socks4-socks4a-socks5-and-socks5h.md)
decision 1 for the SOCKS server, `Surl.Protocol.Socks`, created with it (BL-558). Every
project of ADR-0002's map exists from the first commit; the nine hand-built SSH primitive
libraries (four of ADR-0048, two of ADR-0051 and three of ADR-0061), the Kerberos library of
ADR-0057, the mail servers' two shared libraries of ADR-0050, the loopback test KDC of
ADR-0065 and the HTTP message library of ADR-0070 were added later, each with its twin, and so
were Phase 6's nine: `Surl.Hpack`, `Surl.Http2`, `Surl.Http3`, `Surl.Qpack`, `Surl.Quic`,
`Surl.Tls13` and the three hand-built key exchange primitives `Surl.Cryptography.MlKem`,
`Surl.Cryptography.Curve448` and `Surl.Cryptography.Sm2` (ADR-0081, ADR-0082, ADR-0086,
ADR-0147):

| Production | Tests |
| --- | --- |
| `Surl.Api` (ADR-0099), in `Application/`: no `.UnitLibrary` suffix | `Surl.Api.UnitTests` |
| `Surl.Authentication.UnitLibrary` | `Surl.Authentication.UnitTests` |
| `Surl.Cli.UnitLibrary` | `Surl.Cli.UnitTests` |
| `Surl.Composition.UnitLibrary` (ADR-0099) | `Surl.Composition.UnitTests` |
| `Surl.Conformance.UnitLibrary` | `Surl.Conformance.UnitTests` |
| `Surl.Console` | `Surl.Console.UnitTests` |
| `Surl.Control.UnitLibrary` (ADR-0124), in `Application/` | `Surl.Control.UnitTests` |
| `Surl.Content.UnitLibrary` | `Surl.Content.UnitTests` |
| `Surl.Cookies.UnitLibrary` | `Surl.Cookies.UnitTests` |
| `Surl.Core.UnitLibrary` | `Surl.Core.UnitTests` |
| `Surl.Cryptography.BcryptPbkdf.UnitLibrary` | `Surl.Cryptography.BcryptPbkdf.UnitTests` |
| `Surl.Cryptography.Blowfish.UnitLibrary` | `Surl.Cryptography.Blowfish.UnitTests` |
| `Surl.Cryptography.Cast128.UnitLibrary` | `Surl.Cryptography.Cast128.UnitTests` |
| `Surl.Cryptography.ChaCha20.UnitLibrary` | `Surl.Cryptography.ChaCha20.UnitTests` |
| `Surl.Cryptography.Curve25519.UnitLibrary` | `Surl.Cryptography.Curve25519.UnitTests` |
| `Surl.Cryptography.Curve448.UnitLibrary` (ADR-0147) | `Surl.Cryptography.Curve448.UnitTests` |
| `Surl.Cryptography.Ed25519.UnitLibrary` | `Surl.Cryptography.Ed25519.UnitTests` |
| `Surl.Cryptography.MlKem.UnitLibrary` (ADR-0082) | `Surl.Cryptography.MlKem.UnitTests` |
| `Surl.Cryptography.Poly1305.UnitLibrary` | `Surl.Cryptography.Poly1305.UnitTests` |
| `Surl.Cryptography.Rc4.UnitLibrary` | `Surl.Cryptography.Rc4.UnitTests` |
| `Surl.Cryptography.Ripemd160.UnitLibrary` | `Surl.Cryptography.Ripemd160.UnitTests` |
| `Surl.Cryptography.Sm2.UnitLibrary` (ADR-0147) | `Surl.Cryptography.Sm2.UnitTests` |
| `Surl.Cryptography.UnitLibrary` | `Surl.Cryptography.UnitTests` |
| `Surl.Hpack.UnitLibrary` (ADR-0081) | `Surl.Hpack.UnitTests` |
| `Surl.Http2.UnitLibrary` (ADR-0086) | `Surl.Http2.UnitTests` |
| `Surl.Http3.UnitLibrary` (ADR-0086) | `Surl.Http3.UnitTests` |
| `Surl.HttpMessage.UnitLibrary` (ADR-0070) | `Surl.HttpMessage.UnitTests` |
| `Surl.Kerberos.TestKdc.UnitLibrary` (test fixture: only test code references it) | `Surl.Kerberos.TestKdc.UnitTests` |
| `Surl.Kerberos.UnitLibrary` | `Surl.Kerberos.UnitTests` |
| `Surl.LineProtocol.UnitLibrary` | `Surl.LineProtocol.UnitTests` |
| `Surl.MailStore.UnitLibrary` | `Surl.MailStore.UnitTests` |
| `Surl.Mcp` (the `surl-mcp` executable, ADR-0103), in `Application/`: no `.UnitLibrary` suffix | `Surl.Mcp.UnitTests` (which also tests `Surl.Mcp.UnitLibrary`) |
| `Surl.Mcp.UnitLibrary` (the tools and the running-server book, ADR-0103), in `Application/` | `Surl.Mcp.UnitTests` |
| `Surl.McpServing.UnitLibrary` (ADR-0103), in `Protocols/Http/` | `Surl.McpServing.UnitTests` |
| `Surl.MessageBus.UnitLibrary` (ADR-0100), in `Protocols/Messaging/` | `Surl.MessageBus.UnitTests` |
| `Surl.Networking.UnitLibrary` | `Surl.Networking.UnitTests` |
| `Surl.Output.UnitLibrary` | `Surl.Output.UnitTests` |
| `Surl.Protocol.Abstractions.UnitLibrary` | `Surl.Protocol.Abstractions.UnitTests` |
| `Surl.Protocol.<Name>.UnitLibrary`, one per server above | `Surl.Protocol.<Name>.UnitTests` |
| `Surl.Qpack.UnitLibrary` (ADR-0082) | `Surl.Qpack.UnitTests` |
| `Surl.Quic.UnitLibrary` (ADR-0082) | `Surl.Quic.UnitTests` |
| `Surl.Tls13.UnitLibrary` (ADR-0082) | `Surl.Tls13.UnitTests` |

## Success criteria

1. **Upstream curl completes every exchange.** For a corpus of invocations of the pinned
   upstream curl 8.21.0 builds against Surl, curl's exit code, output and the bytes on the
   wire are what a correct server for that protocol produces. A disagreement is a Surl
   defect until measurement shows otherwise.
2. **Upstream's own test cases pass with Surl as the server.** Surl plays the server half
   of upstream curl's test cases, with a pinned upstream build as the client, and each
   case's verify section passes. The pass rate is stated per release and only rises.
3. **Unit tests need no network.** `dotnet test --filter "TestCategory!=Integration"` is
   green on Windows, Linux and macOS with no socket opened.
4. **It drops onto `PATH`.** A published `surl` runs with no .NET runtime installed.
5. **Then, and only then, it measures the Curl port** (Phase 7).

## Constraints

- **Runtime:** .NET 10, pinned in `global.json`. Base class library only; the MSTest
  meta-package is the one package, for tests (`CLAUDE.md`).
- **Publishing:** native ahead-of-time, one file, no runtime needed, for two executables:
  `surl` and `surl-mcp` (`Surl.Mcp`, ADR-0103). `Surl.Api` and `Surl.Mcp` are also packed as
  the NuGet packages `Surl.Api` and `Surl.Mcp` (a .NET tool of type `McpServer`); no release is
  tagged, so none is published yet.
- **Platforms:** Windows, Linux and macOS, on x64 and Arm64.
- **Licensing:** MIT (`LICENSE.txt`). Upstream test data copied in for `Surl.Conformance`
  keeps curl's own notice beside it.
- **Clean room is a choice:** Surl is written from the protocols' specifications, curl's
  documentation and measurement of what upstream curl sends - not by translating
  upstream's test servers.

## Phasing

| Phase | Delivers | Proves |
| --- | --- | --- |
| 0 | The solution, every project, conventions, quality gates, the dark factory, the first pinned upstream build | The shell holds (Milestone 0) |
| 1 | The listener seam and contracts, `Surl.Networking`, `Surl.Core`, `Surl.Cli`, `Surl.Output`, `Surl.Console`, `Surl.Content`, the HTTP/1.x server with `Surl.Authentication` and `Surl.Cookies`; DICT, Gopher, TELNET, TFTP and MQTT alongside | `surl http://...` serves and upstream curl fetches from it; the seams hold |
| 2 | FTP, then SSH with SCP and SFTP over the hand-built primitive libraries of ADR-0048 | A control channel and data channels; the hand-built SSH primitives |
| 3 | SMTP, IMAP, POP3 | The line-oriented servers share their machinery |
| 4 | WebSocket | The upgrade from HTTP |
| 5 | LDAP, SMB, RTSP | The awkward remainder |
| 6 | The upstream test-case push, HTTP/2 and HTTP/3, native publish on every platform | Surl answers everything upstream curl asks |
| 7 | The Curl port measured against Surl, beside upstream curl | The port's quality, judged by an instrument upstream curl validated |

## Open questions

None open. The four questions this section listed are answered (Stewart, 2026-09-28), and the answer to question 4 was reversed (Stewart, 2026-11-04):

| # | Question | Answer | Carried out by |
| --- | --- | --- | --- |
| 1 | Pin an upstream build with SMB, HTTP/2 and HTTP/3? | Answered: yes, the latest. curl.se's current Windows build (8.22.0_2) is pinned as a supplementary build, used only for SMB, HTTP/2 and HTTP/3; 8.21.0 stays the reference release. Stewart, 2026-09-28. | BL-026 |
| 2 | Upstream builds on Linux and macOS? | Answered: download them. The approval covers upstream 8.21.0 builds for Linux and macOS; which builds, and how CI obtains them, is decided by ADR. Stewart, 2026-09-28. | BL-027 and BL-028 |
| 3 | Local testing only, or hardened for internet-facing use? | Answered: internet-facing. Surl is hardened to be exposed to the internet (see "Users"). Stewart, 2026-09-28. | BL-024 |
| 4 | A managed NuGet API, or the `surl` executable only? | Reversed 2026-11-04. First answered "the executable" (Stewart, 2026-09-28); Surl ships `Surl.Api`, a .NET programming interface that starts and stops its servers in-process, and `Surl.Mcp`, an MCP server host for AI agents, beside `surl` (Stewart, 2026-11-04; [ADR-0098](../Planning/Decisions/ADR-0098-surl-api-and-surl-mcp-join-surl-as-products.md)); both are built. | ADR-0099 and ADR-0103 |

## Sources

- `curl --version` of each build on Stewart's machine, 2026-09-28 (`UpstreamCurlBuilds.json`)
- Tag `curl-8_21_0` of https://github.com/curl/curl, commit `68720b48`, `tests/` counted 2026-09-28
- https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html
- https://github.com/StewartScottRogers/Curl - the Curl port, whose project map Surl
  mirrors (ADR-0002) and which is never Surl's oracle (ADR-0003)
