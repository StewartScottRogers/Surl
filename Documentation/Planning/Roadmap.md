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
  SASL `EXTERNAL` over the mail servers is proven by unit tests only, and what the Linux and
  macOS reference builds do against the mail servers is not yet recorded against the servers'
  ADRs. SASL `GSSAPI` is proven with the Windows reference build through the hand-built
  loopback test KDC of ADR-0065 (`UpstreamCurlLogsInToSurlWithKerberosTests`, over `smtp`,
  `imap` and `pop3`), and a refused `GSSAPI` ticket's reason is written to the verbose log.
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
  SASL `GSSAPI`), ADR-0059 (a limit told from shutdown, first for SMTP) and ADR-0065 (Kerberos
  logins proved through a hand-built loopback test KDC).

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

## Milestone 5 — Phase 5

- **Status:** Built, and proven with the Windows builds (2026-09-30): the reference build over
  `WinLDAP` for LDAP, ADR-0030's static-curl build for SMB, and the reference build and its
  `libcurl-4.dll` for RTSP. Not closed: LDAP over the OpenLDAP-backed upstream curl that ADR-0076
  pins - its `STARTTLS`, root-DSE SASL discovery, SASL binds, anonymous bind and a succeeding
  `ldaps` search - is measured but not yet proven against `surl` (BL-312); Kerberos inside LDAP's
  `GSS-SPNEGO` is answered with NTLM selected and is still to be decided and built (BL-327); LDAP
  SASL `GSSAPI` is unmeasured, since the pinned OpenLDAP build has no GSS-API (BL-342); what the
  Linux and macOS reference builds do over SMB, and the Linux one over RTSP, is not yet recorded
  against ADR-0073 and ADR-0074; and choices the building tasks made where ADR-0072, ADR-0073 and
  ADR-0074 were silent are recorded in those tasks, not yet in the ADRs.
- **Delivers:** the LDAP server (`Surl.Protocol.Ldap`, schemes `ldap` and `ldaps`): a BER codec on
  `System.Formats.Asn1`, a read-only directory loaded from `<path>/.surl/ldap/directory.ldif`
  (empty in memory), searches, compares, the root DSE, simple, Sicily and SASL binds with their
  security layers, and `StartTLS`; the SMB server (`Surl.Protocol.Smb`, schemes `smb` and `smbs`):
  SMB version 1 over NetBIOS framing, the content store's top-level directories as shares, reads,
  uploads under `--allow-uploads`, and the NTLMv1 session setup under the new `--auth` word
  `ntlmv1`; the RTSP server (`Surl.Protocol.Rtsp`, scheme `rtsp`): all ten RTSP/1.0 methods,
  every file a one-stream presentation of its bytes, one session per connection, interleaved RTP
  and RTCP, and `ANNOUNCE` and `RECORD` uploads; the protocol-neutral SASL contract
  (`ISaslAuthenticationPolicy`, `ISaslSecurityLayer`) and the SMB login contract
  (`ISmbAuthenticationPolicy`) in `Surl.Protocol.Abstractions`; and in `Surl.Authentication` NTLM
  sealing and signing, `DIGEST-MD5` integrity and confidentiality, and the NTLMv1 check over
  `Surl.Cryptography`'s `Des` and `Md4`. What each answers, and what pinned upstream curl has proven, is in the product
  overview, "Built for Phase 5: LDAP, SMB and RTSP".
- **Exit criteria:** the Phase 5 row's "Proves", the awkward remainder: pinned upstream curl
  completes every row of ADR-0072 decision 10 over `ldap` and `ldaps` with the `WinLDAP` build,
  and every case ADR-0076 measured with the OpenLDAP build; every row of ADR-0073 decision 11 over
  `smb` and `smbs`; and every tool row of ADR-0074 decision 11 over `rtsp` and every libcurl row
  of its Amendment 1, against a live `surl`, in `Surl.Conformance.UnitTests`, on each platform
  whose pinned build lists the scheme.
- **Decisions:** ADR-0072 (the LDAP server and its directory, as ADR-0076 amends decision 4),
  ADR-0073 (the SMB server and its NTLMv1 session setup), ADR-0074 (the RTSP server, with its
  Amendment 1, libcurl's requests measured), ADR-0075 (the four NT hashes an NTLM account keeps
  for a non-ASCII password) and ADR-0076 (the OpenLDAP upstream curl build, built reproducibly in
  CI).

## Milestone 6 — Phase 6

- **Status:** Built, and proven in part (2026-11-12). Not closed: no release is tagged, and the
  release row of the pass-rate file is empty. `win-arm64`, `linux-arm64` and `osx-x64` have no
  pinned upstream curl build, so they have the smoke test and the fast tests but neither
  conformance nor a pass rate (BL-361, Blocked until arm64 and Intel-macOS runners can measure
  the builds). `win-x64` conformance against the published binary runs only locally, and a clean
  full run waits on BL-604 and BL-1059. The HTTP/2 and HTTP/3 conformance passes on Linux and
  macOS are read by elimination from one CI run, until BL-724 and BL-768 confirm them. No
  upstream test case runs over HTTP/2 or HTTP/3, since those server halves are not built.
- **Delivers:** HTTP/2 on `https` by ALPN `h2`, on `http` by prior knowledge and by
  `Upgrade: h2c`, over the hand-built `Surl.Hpack` and `Surl.Http2`; HTTP/3 over QUIC version 1
  with `surl --http3`, over the hand-built `Surl.Quic`, `Surl.Tls13`, `Surl.Qpack` and
  `Surl.Cryptography.MlKem`, serving twelve TLS 1.3 key exchange groups (the last seven with
  `Surl.Cryptography.Curve448` and `Surl.Cryptography.Sm2`) and requiring a client certificate
  with `--cacert`; `Surl.Http3`, which with `Surl.Http2` the extended servers share; the
  upstream test-case suite in `Surl.Conformance` (parser, selector, verify checker, server
  halves for the HTTP and HTTPS family, RTSP, FTP and FTPS, IMAP, POP3 and SMTP, DICT, Gopher,
  TELNET, MQTT, TFTP, SCP, SFTP and SMB) with a stated pass rate per platform; and native
  publishing proven on six runtime identifiers by `Test-PublishedSurl.ps1` and, on `linux-x64`
  and `osx-arm64`, by `Surl.Conformance` run against the published binary. What each answers,
  and what pinned upstream curl has proven, is in the product overview, "Built for Phase 6:
  HTTP/2, HTTP/3, upstream's test cases and native publishing".
- **Exit criteria:** the Phase 6 row's "Proves", that Surl answers everything upstream curl
  asks: success criterion 2, every upstream test case a pinned reference build can run passing
  and the rate stated per release; `UpstreamCurlFetchesFromSurlOverHttp2Tests` and
  `UpstreamCurlFetchesFromSurlOverHttp3Tests` green against a live `surl` on each platform;
  and success criterion 4, a release dry run of the commit to be tagged green on all six
  runtime identifiers with the local `win-x64` conformance run passed (ADR-0085 decision 5).
- **Decisions:** ADR-0080 (how Surl plays the server half of upstream's test cases), ADR-0081
  (HTTP/2), ADR-0082 (HTTP/3 over QUIC, with its amendments), ADR-0085 (what proves the native
  `surl` on each runtime identifier), ADR-0086 (the libraries the extended servers share for
  HTTP/2, HTTP/3 and QUIC), ADR-0090 (the HTTP/2 prior-knowledge connection), ADR-0091
  (`Upgrade: h2c`), ADR-0096 (QUIC streams and flow control), ADR-0140 (QUIC loss detection and
  congestion control) and ADR-0147 (the remaining TLS 1.3 groups curl offers over QUIC).

## Milestone 7 — Phase 7

- **Status:** Built for `win-x64` and measured once; not closed (2026-11-12). The Curl port was
  measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, the one commit every family
  file measured: 1,518 cases, 1,420 agreements, 96 port defects behind 18 findings, 2 cases not
  comparable, and no Surl defect. Still open: off Windows every Phase 7 test is Inconclusive,
  because `CurlPortBuilds.json` pins the port for `win-x64` alone; the 47 `OpenLdap_` cases wait
  for a `linux-x64` port pin; 15 of the extended clients' `Integration` tasks have not run, so
  `Extended.md` records 4 of the 19 extended clients; and Stewart has not yet filed the 18
  findings on the port's board.
- **Delivers:** the harness in `Surl.Conformance` (`CurlPortClientPins`, `CurlPortClientLocator`,
  `CurlPortClientRunner`, `CurlPortResultNormaliser`, `CurlPortComparison`, `CurlPortVerdict`,
  `CurlPortFinding`, `CurlPortLibcurlComparison` and `CurlPortTestCaseClient`); one
  `CurlPortBesideUpstreamCurlOver<Family>Tests` class per family, running the port beside the
  pinned upstream build against a fresh `surl` each; the findings files and their summary in
  `Documentation/Conformance/CurlPort/`; and `Port-Board-Handoff.md`, the 18 findings as tasks
  in the port's own task shape. What was measured and found is in the product overview, "Built
  for Phase 7: the Curl port measured beside pinned upstream curl".
- **Exit criteria:** the Phase 7 row's "Proves", success criterion 5: the Curl port measured
  beside pinned upstream curl with every disagreement written down as a finding a test reads
  and handed to the port's board. Met for `win-x64` and the original protocols. Not met for
  other platforms, the `OpenLdap_` cases or the remaining extended clients (above).
- **Decisions:** ADR-0003 decision 6 (Phase 7 is a measurement of the port, never a check of
  Surl) and ADR-0089 (how Phase 7 measures it: the normalisation, the verdicts, the findings,
  the handoff and the single port commit).

## Track — Shared stores and the configuration command line

Beside the phases, not after them: the extended servers' track of ADR-0083 reaches into the
original servers' stores, and the stand-in direction (ADR-0124) builds on both.

- **Status:** Built, except the parts named under "Not built" (2026-11-12).
- **Delivers:** one store per job in a `surl` run (ADR-0100): the content store, the mail
  store, `Surl.MessageBus` in `Protocols/Messaging/` (the MQTT 3.1.1 server's retained messages
  moved onto it, and the extended NATS and STOMP servers are registered on it) and the account
  book; then the configuration surface on top of them (ADR-0101, ADR-0102): `-K`/`--config`
  and `--url`, the item options for mail (`--mail-domain`, `--mailbox`, `--mailbox-file`,
  `--mail-alias`, `--mail-catch-all`, `--shared-folder`), files (`--share`, `--share-access`,
  `--bucket`), messages (`--topic`, `--queue`, `--topic-map`, `--topic-access`) and accounts
  (`--group`), their `Surl.Api` spellings, and the `--aihelp` topics `mail` and `messages`.
  The build tasks are BL-629 to BL-668 (BL-649 to BL-652 in that range are not part of it),
  with BL-638 to BL-640 proving the cross-protocol exchanges with pinned upstream curl.
- **Exit criteria:** a `surl` given none of the new options answers every original server
  exactly as before (ADR-0102 decision 0), and pinned upstream curl completes a file, a mail and
  a message exchange across protocols in one `surl` (BL-638 to BL-640).
- **Not built:** the `mqtt5`, `amqp`, `amqp1`, `kafka` and `redis` servers are not registered in
  `surl` (BL-975, BL-507, BL-514, BL-973, BL-492); LDAP as an optional account source is
  decided by BL-671 and built by BL-672 and BL-673; the Curl port's clients reading the shared
  files and messages are BL-669 and BL-670; test control of the stores is BL-821's page,
  `Documentation/Wiki/Surl-As-A-Stand-In.md`.
- **Decisions:** ADR-0100, ADR-0101 and ADR-0102.

## Track — The hosts: Surl.Api and Surl.Mcp

Beside the phases: `surl` is one of three hosts (ADR-0098), and the stand-in direction
(ADR-0124) is built on the other two.

- **Status:** The hosts are built and proved (2026-11-12). The stand-in features on them are
  built in part; BL-821 documents them.
- **Delivers:** `Surl.Composition`, the one start all three hosts run (ADR-0099); `Surl.Api`,
  the in-process .NET interface with typed options, exchange recording, queries and waits, fault
  rules, hooks, seeding, reset and snapshots and a test's own protocol servers; `Surl.Mcp`, the
  `surl-mcp` stdio MCP server with its tools and `surl://aihelp` resources, on `Surl.McpServing`
  and `Surl.Mcp.UnitLibrary` (ADR-0103); the `surl-mcp` release archives and the NuGet packages,
  packed but not yet published. Decided by BL-610 to BL-612 and built by BL-609, BL-613 to
  BL-617, BL-619 to BL-621, BL-623 to BL-628, BL-637 and BL-643.
- **Exit criteria:** met by BL-618, which proves `Surl.Api` against pinned upstream curl, and
  BL-632, which drives the built `surl-mcp` over stdio with pinned upstream curl. BL-649 states the
  hosts as built in the README, the product overview, the glossary and the root `CLAUDE.md`.
- **Not built:** serving the control endpoint from `surl` and `Surl.Api` (the `Surl.Control`
  library and its options exist, BL-814 and BL-815; BL-816, which serves it, is deferred) and the stand-in page
  `Documentation/Wiki/Surl-As-A-Stand-In.md` (BL-821); publishing the packages waits for the
  first release tag.
- **Decisions:** ADR-0098, ADR-0099, ADR-0103 and ADR-0124.

## Later

Phase 7 is the last phase in the product overview's phasing, so no later milestone is planned. What is still open is listed in the milestone it belongs to,
and the work itself is on the task board.
