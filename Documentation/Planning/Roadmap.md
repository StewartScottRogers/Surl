# Roadmap

Milestones, not dates. The phases themselves, what each delivers and what each proves,
are in `Documentation/Product/Product-Overview.md`, "Phasing"; work items live on the
task board in `Tasks/`, never here.

## Milestone 0 — Foundations

- **Status:** Done (2026-09-28)
- **Delivers:** `Surl.slnx` and every project ADR-0002 names, each production project
  with its `CLAUDE.md`; the `Documentation`, `Tasks` and `.claude/Claude` shared projects;
  the conventions, quality gates, agents, commands, scripts and workflows carried over from
  the Curl port; `UpstreamCurlBuilds.json` with the first pinned upstream curl build, and
  `Record-CurlExchange.ps1` refusing any other.
- **Exit criteria:** `dotnet build` clean with warnings as errors; the fast tests green;
  `Measure-CodeQuality.ps1` passing; the task board script working on an empty board.
- **Decisions:** ADR-0001 to ADR-0003.
- **GitHub repository:** https://github.com/StewartScottRogers/Surl, created by Stewart;
  the `CI` workflow ran green on Windows, Linux and macOS for pull request #1.

## Milestone 1 — Phase 1

- **Status:** Built, except `Surl.Cookies`, which holds no code yet (2026-09-30).
- **Delivers:** see the Phase 1 row of the product overview. `surl` serves `http` and
  `https`, `dict`, `gopher` and `gophers`, `mqtt` and `mqtts`, `telnet` and `tftp` over the
  listener seam, with `Surl.Authentication`'s logins and the curl-style command line, help,
  AI help and logging.
- **Decisions:** the listener seam and exchange context (ADR-0004), the exit-code table
  (ADR-0005), hardening (ADR-0006), the command line (ADR-0007), the server-side TLS contract
  (ADR-0010), and the ADRs after them up to ADR-0046, but for ADR-0029, which is the dark
  factory's.

## Milestone 2 — Phase 2

- **Status:** Built, and proven with the Windows reference build (2026-09-30). Not closed:
  what the Linux and macOS reference builds negotiate over SSH is pinned as
  predicted, not yet recorded as measured.
- **Delivers:** the FTP server (`Surl.Protocol.Ftp`, schemes `ftp` and `ftps`) with passive
  and active data connections through the data-connection seam (`IDataConnectionOpener`,
  implemented over sockets by `Surl.Networking`), explicit and implicit FTPS, and
  `USER`/`PASS` logins; the SSH server (`Surl.Protocol.Ssh`, schemes `scp` and `sftp`) with
  its transport, host keys, password, keyboard-interactive and public-key logins, SCP and the
  SFTP subsystem through the content store; and the nine hand-built primitive libraries
  under it (`Surl.Cryptography.Curve25519`, `.Ed25519`, `.ChaCha20`, `.Poly1305`, `.Rc4`,
  `.BcryptPbkdf`, `.Blowfish`, `.Cast128`, `.Ripemd160`). What each answers, and what pinned
  upstream curl has proven, is in the product overview, "Built for Phase 2: FTP, FTPS, SCP
  and SFTP".
- **Exit criteria:** the Phase 2 row's "Proves": pinned upstream curl completes every case of
  ADR-0052 decision 12 over `ftp` and `ftps` and every row of ADR-0054 decision 16 over `scp`
  and `sftp` against a live `surl`, in `Surl.Conformance.UnitTests`, on Windows, Linux and
  macOS.
- **Decisions:** ADR-0048 (the SSH primitive libraries), ADR-0051 (the SSH transport, host
  keys and logins), ADR-0052 (the FTP server and the data-connection seam), ADR-0054 (SCP and
  SFTP), ADR-0058 (key exchange and host-key reading choices), ADR-0059 (a limit told from
  shutdown), ADR-0060 (messages during a server-started re-exchange), ADR-0061 (Blowfish,
  CAST-128 and RIPEMD-160) and ADR-0062 (the libssh2 1.11.1 WinCNG key-exchange defect).

## Milestone 3 — Phase 3

- **Status:** Built, and proven with the Windows reference build (2026-09-30). Not closed:
  SASL `GSSAPI` and `EXTERNAL` are proven by unit tests only, since proving `GSSAPI` from pinned
  upstream curl needs a KDC and how to provide one is still to be decided (BL-242); a refused
  `GSSAPI` ticket's reason is not yet written to the verbose log (BL-260); and what the Linux
  and macOS reference builds do against the mail servers is not yet recorded against the
  servers' ADRs.
- **Delivers:** the mail store (`Surl.MailStore`: mailboxes per owner, the POP3 maildrop lock,
  bounds, persistence under `<path>/.surl/mail`) and the line machinery (`Surl.LineProtocol`:
  CRLF command lines, dot-stuffing, the `STARTTLS` discard, SASL continuation lines) the three
  servers share; the SMTP server (`Surl.Protocol.Smtp`, schemes `smtp` and `smtps`), the IMAP
  server (`Surl.Protocol.Imap`, schemes `imap` and `imaps`) and the POP3 server
  (`Surl.Protocol.Pop3`, schemes `pop3` and `pop3s`), each with `STARTTLS` or `STLS` and
  implicit TLS; and the mail logins in `Surl.Authentication` through `IMailAuthenticationPolicy`:
  nine SASL mechanisms, IMAP `LOGIN`, POP3 `USER`/`PASS` and `APOP`, with `GSSAPI` over the
  `--keytab` keys of `Surl.Kerberos`. What each answers, and what pinned upstream curl has
  proven, is in the product overview, "Built for Phase 3: SMTP, IMAP and POP3".
- **Exit criteria:** the Phase 3 row's "Proves": pinned upstream curl completes every row of
  ADR-0053 decision 10 over `smtp` and `smtps`, of ADR-0055 decision 15 over `imap` and `imaps`
  and of ADR-0056 decision 12 over `pop3` and `pop3s` against a live `surl`, reading back over
  IMAP and POP3 the mail it sent over SMTP, in `Surl.Conformance.UnitTests`, on Windows, Linux
  and macOS.
- **Decisions:** ADR-0049 (the SASL and `APOP` logins and `IMailAuthenticationPolicy`),
  ADR-0050 (the mail store and the line machinery), ADR-0053 (the SMTP server), ADR-0055 (the
  IMAP server), ADR-0056 (the POP3 server), ADR-0057 (the keytab and the AP-REQ check behind
  SASL `GSSAPI`) and ADR-0059 (a limit told from shutdown, first for SMTP).

## Milestone 4 — Phase 4

- **Status:** Built, and proven with the Windows reference build and its `libcurl-4.dll`
  (2026-09-30). Not closed: what the Linux and macOS reference builds do against the WebSocket
  server is not yet recorded against ADR-0071, and the libcurl cases cannot run there, since
  those builds carry no shared library (ADR-0071 decision 10).
- **Delivers:** the HTTP message library the HTTP, WebSocket and RTSP servers share
  (`Surl.HttpMessage`: the bounded HTTP/1.x request-head reader and its head timeout, request-line
  and field-line parsing, response heads and `WWW-Authenticate` challenge fields), with the HTTP
  server moved onto it byte for byte; and the WebSocket server (`Surl.Protocol.Ws`, schemes `ws`
  and `wss`): the upgrade and its refusals, logins through the HTTP authentication session, a
  file or listing sent as one message in 65536-byte frames, `--ws-echo`, `PING`, `PONG` and
  `CLOSE` answered, invalid frames closed with 1002, 1007 or 1009, `CLOSE` 1001 at a limit, and
  the one-second lingering close. What it answers, and what pinned upstream curl has proven, is
  in the product overview, "Built for Phase 4: WebSocket".
- **Exit criteria:** the Phase 4 row's "Proves", the upgrade from HTTP: pinned upstream curl
  completes every row of ADR-0071 decision 11 over `ws` and `wss` against a live `surl`, and the
  pinned `libcurl-4.dll` every row of ADR-0071's Amendment 1 against `surl --ws-echo`, in
  `Surl.Conformance.UnitTests`; the tool rows on Windows, Linux and macOS, the libcurl rows on
  Windows.
- **Decisions:** ADR-0070 (`Surl.HttpMessage`, and the upgrade answered only on `ws://` and
  `wss://` listen URLs) and ADR-0071 (how the WebSocket server answers upstream curl, with its
  Amendment 1, libcurl's client frames measured).

## Later

Phases 5 and 6 follow the product overview. Phase 7, the last, turns Surl on the Curl port:
the port runs the same conversations against Surl beside pinned upstream curl, and every
disagreement is filed as the port's defect on the port's own board (ADR-0003).
