---
id: BL-605
title: Serve FTP and FTPS control connections on a Unix domain socket
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-547, BL-548]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Record-CurlExchange.ps1, Conformance/Surl.Conformance.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-605 — Serve FTP and FTPS control connections on a Unix domain socket

## Goal

`surl --unix-socket <path> ftp://localhost/` (and `ftps://`) serves its FTP control connection on the Unix socket, with passive data connections on loopback TCP, so a pinned upstream curl run with `--unix-socket <path>` completes a listing, a download and an upload.

## Context

- Decision: ADR-0094 decision 2 (BL-571). BL-548 refuses `ftp` and `ftps` with `--unix-socket`
  (`FailedInit`, 2) because FTP's data connections are TCP connections to the address a `PASV` or
  `EPSV` reply names, and a Unix socket listener has no IP address to name. This task lifts that
  refusal.
- Measure first: extend `Record-CurlExchange.ps1`'s `-UnixSocket` (today HTTP responses only) to
  `-Ftp`, and record what curl 8.21.0's reference build does over `--unix-socket` with `EPSV`, with
  `--disable-epsv` (`PASV`) and with `--ftp-port` (active mode): which address it connects the
  data connection to. Pin those bytes before deciding where the passive data listener binds.
- Code: `Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary` (the `PASV`/`EPSV` reply's address),
  `Application/Surl.Cli.UnitLibrary` (BL-548's refusal) and `Application/Surl.Console`.

## Acceptance criteria

- [x] Fixtures recorded with `Record-CurlExchange.ps1 -UnixSocket -Ftp` from the Windows reference
      build show which address curl connects each data connection to, for `EPSV`, `PASV` and
      active mode, and the task's Notes cite them.
- [x] `Surl.Protocol.Ftp.UnitTests` has named tests replaying those bytes, where the `PASV` and
      `EPSV` replies name a loopback data listener when the control connection came over a Unix
      socket.
- [x] `surl --unix-socket <path> ftp://localhost/` no longer exits `FailedInit` (2), with a named
      test in `Surl.Cli.UnitTests`, and a conformance test has pinned upstream curl list and
      download a file through it.
- [x] `dotnet build` is clean, the fast tests are green, and the touched libraries keep 100% line
      and branch coverage.

## Notes

- Measured 2026-11-11 with Record-CurlExchange.ps1 -Ftp -UnixSocket (extended: its FTP session now accepts on the Unix socket; data listener stays on loopback TCP), reference curl 8.21.0 (Windows). Passive (EPSV, --disable-epsv, --ftp-skip-pasv-ip): curl exits 6 Could not resolve host: <socket path> after the reply, whatever address it names, so curl cannot use a passive data connection over --unix-socket. Active -P -: exit 30 command PORT failed (no local address). Active -P 127.0.0.1: EPRT |1|127.0.0.1|port| / with --disable-eprt PORT 127,0,0,1,hi,lo, and the server dials it. Details in ADR-0094 decision 2 (amended; decided by Claude under Stewart's delegation).
- Surl: passive listener binds IPv4 loopback and PASV names 127,0,0,1 for a Unix control connection; active and passive peer checks accept any loopback address for a Unix peer. Replay tests in FtpDataConnectionsTests; Surl.Networking tests for DataConnectionPeer and SocketDataConnectionOpener; CLI refusal removed (CommandLineParserUnixSocketTests); conformance in UpstreamCurlUsesSurlOverFtpOnAUnixSocketTests (list, download, upload with -P 127.0.0.1, and the passive exit 6 pinned).
- touches extended with Foundation/Surl.Networking.* and Record-CurlExchange.ps1: no other Doing task names them.
## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. FTP and FTPS control connections are served on a Unix socket; data connections stay on loopback TCP, pinned curl lists, downloads and uploads with -P 127.0.0.1
