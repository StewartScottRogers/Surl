# Product Overview

- **Status:** Draft. Written for the Phase 0 shell; the numbers below are measured, not
  estimated. Sections still awaiting a decision are marked `> **TODO**`.
- **Last updated:** 2026-09-29
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

### Also in scope

- **HTTP versions:** 1.0, 1.1, 2 and 3 over QUIC, on the server side.
- **Authentication:** issuing the challenge and verifying the answer for every scheme
  upstream curl sends, secure by default
  ([ADR-0032](../Planning/Decisions/ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)):
  with no account configured every login is refused, a password or token sent in clear
  over an unencrypted connection is refused unchecked, and each of the four loosening
  options (`--allow-anonymous`, `--allow-plaintext-auth`, `--auth`, `--self-signed`)
  warns on every start. Accounts come from `-u`/`--user` and `--user-file`. Built today:
  HTTP Basic, Bearer, Digest (MD5, SHA-256 and SHA-512-256), NTLM (NTLMv2), Negotiate
  carrying NTLM (bare or in SPNEGO) and AWS Signature Version 4, and the MQTT `CONNECT`
  user name and password. In scope, not built yet: Kerberos inside Negotiate,
  `Proxy-Authenticate` for the proxies, and the logins of the servers not yet built -
  FTP, the SASL mechanisms of the mail protocols, SSH, SMB and LDAP.
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
lists (`Surl.Content`, `Surl.Cryptography`, and the four SSH primitive libraries of
ADR-0048 once BL-149 adds them); referencing another protocol server is a
build break, and `Surl.Protocol.Abstractions.UnitTests` asserts the reference graph.

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
| Protocol servers | `Surl.Protocol.<Name>` (15) | Abstractions, and `Surl.Content` or `Surl.Cryptography` where needed |
| Services | `Surl.Networking`, `Surl.Authentication`, `Surl.Cookies`, `Surl.Output`, `Surl.Content` | Abstractions; `Surl.Authentication` also `Surl.Cryptography`, for MD4 and SHA-512/256 (ADR-0032 decision 7) |
| Hand-built primitives | `Surl.Cryptography`; for SSH, `Surl.Cryptography.ChaCha20`, `Surl.Cryptography.Curve25519`, `Surl.Cryptography.Ed25519` and `Surl.Cryptography.Poly1305`, decided by [ADR-0048](../Planning/Decisions/ADR-0048-the-hand-built-ssh-primitive-libraries.md) and added by BL-149 | nothing; `Surl.Cryptography.Ed25519` will reference `Surl.Cryptography.Curve25519` |
| Contracts | `Surl.Protocol.Abstractions` | nothing |
| Upstream's test cases | `Surl.Conformance` | Abstractions |

## Project layout

Flat: every project is a directory immediately under the repository root, each production
project followed by its `.UnitTests` twin (`CLAUDE.md`, "Repository layout"). Every
project of ADR-0002's map exists from the first commit; the four hand-built SSH primitive
libraries of ADR-0048 and their twins will be added by BL-149:

| Production | Tests |
| --- | --- |
| `Surl.Authentication.UnitLibrary` | `Surl.Authentication.UnitTests` |
| `Surl.Cli.UnitLibrary` | `Surl.Cli.UnitTests` |
| `Surl.Conformance.UnitLibrary` | `Surl.Conformance.UnitTests` |
| `Surl.Console` | `Surl.Console.UnitTests` |
| `Surl.Content.UnitLibrary` | `Surl.Content.UnitTests` |
| `Surl.Cookies.UnitLibrary` | `Surl.Cookies.UnitTests` |
| `Surl.Core.UnitLibrary` | `Surl.Core.UnitTests` |
| `Surl.Cryptography.ChaCha20.UnitLibrary` (added by BL-149) | `Surl.Cryptography.ChaCha20.UnitTests` (added by BL-149) |
| `Surl.Cryptography.Curve25519.UnitLibrary` (added by BL-149) | `Surl.Cryptography.Curve25519.UnitTests` (added by BL-149) |
| `Surl.Cryptography.Ed25519.UnitLibrary` (added by BL-149) | `Surl.Cryptography.Ed25519.UnitTests` (added by BL-149) |
| `Surl.Cryptography.Poly1305.UnitLibrary` (added by BL-149) | `Surl.Cryptography.Poly1305.UnitTests` (added by BL-149) |
| `Surl.Cryptography.UnitLibrary` | `Surl.Cryptography.UnitTests` |
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
