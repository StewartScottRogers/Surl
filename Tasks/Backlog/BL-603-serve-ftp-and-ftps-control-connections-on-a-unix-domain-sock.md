---
id: BL-603
title: Serve FTP and FTPS control connections on a Unix domain socket
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-547, BL-548]
touches: [Conformance/Surl.Conformance.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-603 — Serve FTP and FTPS control connections on a Unix domain socket

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

- [ ] Fixtures recorded with `Record-CurlExchange.ps1 -UnixSocket -Ftp` from the Windows reference
      build show which address curl connects each data connection to, for `EPSV`, `PASV` and
      active mode, and the task's Notes cite them.
- [ ] `Surl.Protocol.Ftp.UnitTests` has named tests replaying those bytes, where the `PASV` and
      `EPSV` replies name a loopback data listener when the control connection came over a Unix
      socket.
- [ ] `surl --unix-socket <path> ftp://localhost/` no longer exits `FailedInit` (2), with a named
      test in `Surl.Cli.UnitTests`, and a conformance test has pinned upstream curl list and
      download a file through it.
- [ ] `dotnet build` is clean, the fast tests are green, and the touched libraries keep 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
