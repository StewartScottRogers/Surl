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
  `surl` refuses `--allow-weak-ssh-algorithms` and `--hostcert` with exit 2 as not available
  in this build, and what the Linux and macOS reference builds negotiate over SSH is pinned as
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

## Later

Phases 3 to 6 follow the product overview. Phase 7, the last, turns Surl on the Curl port:
the port runs the same conversations against Surl beside pinned upstream curl, and every
disagreement is filed as the port's defect on the port's own board (ADR-0003).
