# Product Overview

- **Status:** Draft. Written for the Phase 0 shell and kept true as the phases are built; the
  numbers below are measured, not estimated. No section is awaiting a decision.
- **Last updated:** 2026-09-30
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
- Contributors adding a protocol server, who need the seams to be obvious.

Surl is built to be exposed to the internet, not only to answer on loopback in a test
(Stewart, 2026-09-28). The security scope that hardening sets - connection, time and size
limits, what a server exposes by default, what a peer may learn, and the TLS minimums -
is decided in
[ADR-0006](../Planning/Decisions/ADR-0006-hardening-for-internet-facing-use.md).

## Non-goals

- **Validating Surl against anything but upstream curl.** Not the Curl port, not another
  server, not a specification read in isolation from what curl actually sends.
- **Beating dedicated servers on throughput.** Fidelity to what upstream curl expects is
  the target, not nginx's request rate.
- **Protocols upstream curl does not request.** Surl answers curl; a protocol curl cannot
  speak has no mate to be.
- **A managed NuGet API.** The `surl` executable is the only product; its libraries are
  implementation, not a published API, and no package is shipped (Stewart, 2026-09-28).

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

`surl` registers both Phase 2 servers (`Surl.Console`'s `ComposeProtocolServers`), each with
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

`surl` registers the three mail servers (`Surl.Console`'s `ComposeProtocolServers`), each with
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
  names an account, a recipient that names none answered alike and its copy discarded; `VRFY`
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
`EXTERNAL` and `GSSAPI` are proven by unit tests only: proving `GSSAPI` from pinned upstream
curl needs a KDC, and how to provide one is still to be decided (BL-242).

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
  `--cacert` verifies), IMAP `LOGIN`, POP3 `USER`/`PASS` and POP3 `APOP`. In scope, not built
  yet: Kerberos inside Negotiate (BL-241), `Proxy-Authenticate` for the proxies, and the logins
  of the servers not yet built - SMB and LDAP.
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
servers' `Surl.MailStore` and `Surl.LineProtocol` of ADR-0050, and, once BL-292 creates it,
the HTTP, WebSocket and RTSP servers' `Surl.HttpMessage` of ADR-0070); referencing another protocol
server is a build break, and `Surl.Protocol.Abstractions.UnitTests` asserts the reference graph.

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
| Executable | `Surl.Console` | everything below, as the composition root |
| Command line | `Surl.Cli` | `Surl.Core`, `Surl.Output`, Abstractions |
| Serving engine | `Surl.Core` | Abstractions |
| Protocol servers | `Surl.Protocol.<Name>` (15) | Abstractions, and the horizontal libraries of ADR-0002's table where needed: `Surl.Content`, `Surl.Cryptography` and its SSH primitives, for SMTP, IMAP and POP3 `Surl.MailStore` and `Surl.LineProtocol`, and for HTTP, WebSocket and RTSP `Surl.HttpMessage` (ADR-0070) |
| Services | `Surl.Networking`, `Surl.Authentication`, `Surl.Cookies`, `Surl.Output`, `Surl.Content` | Abstractions; `Surl.Authentication` also `Surl.Cryptography`, for MD4 and SHA-512/256 (ADR-0032 decision 7), and `Surl.Kerberos` (ADR-0057 decision 6) |
| Mail servers' shared libraries | `Surl.MailStore` (the mail store: mailboxes per account, messages with UIDs, bounds, persistence under `<path>/.surl/mail`) and `Surl.LineProtocol` (bounded CRLF command lines, dot-stuffing, the `STARTTLS` discard, SASL continuation lines), decided by [ADR-0050](../Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md) | Abstractions; `Surl.MailStore` also `Surl.Content`, for `IContentFileSystem` |
| HTTP message library | `Surl.HttpMessage` (filled by BL-293: the bounded HTTP/1.x request-head reader and its head timeout, request-line and field-line parsing for `HTTP/1.x` and `RTSP/1.0`, response heads and `WWW-Authenticate` challenge fields), used by `Surl.Protocol.Http` and to be shared with `Surl.Protocol.Ws` and `Surl.Protocol.Rtsp`, decided by [ADR-0070](../Planning/Decisions/ADR-0070-the-http-message-library-the-http-websocket-and-rtsp-servers-share.md) | Abstractions |
| Hand-built primitives | `Surl.Cryptography`; for SSH, `Surl.Cryptography.ChaCha20`, `Surl.Cryptography.Curve25519`, `Surl.Cryptography.Ed25519` and `Surl.Cryptography.Poly1305` ([ADR-0048](../Planning/Decisions/ADR-0048-the-hand-built-ssh-primitive-libraries.md)), `Surl.Cryptography.Rc4` and `Surl.Cryptography.BcryptPbkdf` ([ADR-0051](../Planning/Decisions/ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md) decision 3), and `Surl.Cryptography.Blowfish`, `Surl.Cryptography.Cast128` and `Surl.Cryptography.Ripemd160` ([ADR-0061](../Planning/Decisions/ADR-0061-blowfish-cast-128-and-ripemd-160-for-curls-openssl-builds.md)); for Kerberos, `Surl.Kerberos` ([ADR-0057](../Planning/Decisions/ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md)) | nothing; `Surl.Cryptography.Ed25519` references `Surl.Cryptography.Curve25519`, and `Surl.Cryptography.BcryptPbkdf` references `Surl.Cryptography.Blowfish` |
| Test fixtures | `Surl.Kerberos.TestKdc`, the hand-built loopback KDC for realm `SURL.TEST` ([ADR-0065](../Planning/Decisions/ADR-0065-kerberos-logins-are-proved-against-pinned-upstream-curl-through-a-hand-built-loopback-kdc.md) decision 1). Not a protocol server and not a horizontal library of ADR-0002's table: only its own test project references it (BL-267's `Run-KerberosTestKdc.cs` file-based app is to be the other user), and `Surl.Console` never does | `Surl.Kerberos`, Abstractions |
| Contracts | `Surl.Protocol.Abstractions` | nothing |
| Upstream's test cases | `Surl.Conformance` | Abstractions |

## Project layout

Flat: every project is a directory immediately under the repository root, each production
project followed by its `.UnitTests` twin (`CLAUDE.md`, "Repository layout"). Every
project of ADR-0002's map exists from the first commit; the nine hand-built SSH primitive
libraries (four of ADR-0048, two of ADR-0051 and three of ADR-0061), the Kerberos library of
ADR-0057, the mail servers' two shared libraries of ADR-0050 and the loopback test KDC of
ADR-0065 were added later, each with its twin; the HTTP message library of ADR-0070 is to
be added the same way:

| Production | Tests |
| --- | --- |
| `Surl.Authentication.UnitLibrary` | `Surl.Authentication.UnitTests` |
| `Surl.Cli.UnitLibrary` | `Surl.Cli.UnitTests` |
| `Surl.Conformance.UnitLibrary` | `Surl.Conformance.UnitTests` |
| `Surl.Console` | `Surl.Console.UnitTests` |
| `Surl.Content.UnitLibrary` | `Surl.Content.UnitTests` |
| `Surl.Cookies.UnitLibrary` | `Surl.Cookies.UnitTests` |
| `Surl.Core.UnitLibrary` | `Surl.Core.UnitTests` |
| `Surl.Cryptography.BcryptPbkdf.UnitLibrary` | `Surl.Cryptography.BcryptPbkdf.UnitTests` |
| `Surl.Cryptography.Blowfish.UnitLibrary` | `Surl.Cryptography.Blowfish.UnitTests` |
| `Surl.Cryptography.Cast128.UnitLibrary` | `Surl.Cryptography.Cast128.UnitTests` |
| `Surl.Cryptography.ChaCha20.UnitLibrary` | `Surl.Cryptography.ChaCha20.UnitTests` |
| `Surl.Cryptography.Curve25519.UnitLibrary` | `Surl.Cryptography.Curve25519.UnitTests` |
| `Surl.Cryptography.Ed25519.UnitLibrary` | `Surl.Cryptography.Ed25519.UnitTests` |
| `Surl.Cryptography.Poly1305.UnitLibrary` | `Surl.Cryptography.Poly1305.UnitTests` |
| `Surl.Cryptography.Rc4.UnitLibrary` | `Surl.Cryptography.Rc4.UnitTests` |
| `Surl.Cryptography.Ripemd160.UnitLibrary` | `Surl.Cryptography.Ripemd160.UnitTests` |
| `Surl.Cryptography.UnitLibrary` | `Surl.Cryptography.UnitTests` |
| `Surl.HttpMessage.UnitLibrary` (ADR-0070) | `Surl.HttpMessage.UnitTests` |
| `Surl.Kerberos.TestKdc.UnitLibrary` (test fixture: only test code references it) | `Surl.Kerberos.TestKdc.UnitTests` |
| `Surl.Kerberos.UnitLibrary` | `Surl.Kerberos.UnitTests` |
| `Surl.LineProtocol.UnitLibrary` | `Surl.LineProtocol.UnitTests` |
| `Surl.MailStore.UnitLibrary` | `Surl.MailStore.UnitTests` |
| `Surl.Networking.UnitLibrary` | `Surl.Networking.UnitTests` |
| `Surl.Output.UnitLibrary` | `Surl.Output.UnitTests` |
| `Surl.Protocol.Abstractions.UnitLibrary` | `Surl.Protocol.Abstractions.UnitTests` |
| `Surl.Protocol.<Name>.UnitLibrary`, one per server above | `Surl.Protocol.<Name>.UnitTests` |

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
- **Publishing:** native ahead-of-time, one file, no runtime needed.
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

None open. The four questions this section listed are answered (Stewart, 2026-09-28):

| # | Question | Answer | Carried out by |
| --- | --- | --- | --- |
| 1 | Pin an upstream build with SMB, HTTP/2 and HTTP/3? | Answered: yes, the latest. curl.se's current Windows build (8.22.0_2) is pinned as a supplementary build, used only for SMB, HTTP/2 and HTTP/3; 8.21.0 stays the reference release. Stewart, 2026-09-28. | BL-026 |
| 2 | Upstream builds on Linux and macOS? | Answered: download them. The approval covers upstream 8.21.0 builds for Linux and macOS; which builds, and how CI obtains them, is decided by ADR. Stewart, 2026-09-28. | BL-027 and BL-028 |
| 3 | Local testing only, or hardened for internet-facing use? | Answered: internet-facing. Surl is hardened to be exposed to the internet (see "Users"). Stewart, 2026-09-28. | BL-024 |
| 4 | A managed NuGet API, or the `surl` executable only? | Answered: the executable. `surl` is the only product; its libraries are implementation, not a published API (see "Non-goals"). Stewart, 2026-09-28. | none |

## Sources

- `curl --version` of each build on Stewart's machine, 2026-09-28 (`UpstreamCurlBuilds.json`)
- Tag `curl-8_21_0` of https://github.com/curl/curl, commit `68720b48`, `tests/` counted 2026-09-28
- https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html
- https://github.com/StewartScottRogers/Curl - the Curl port, whose project map Surl
  mirrors (ADR-0002) and which is never Surl's oracle (ADR-0003)
