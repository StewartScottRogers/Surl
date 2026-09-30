---
id: BL-181
title: Secure FTP with AUTH TLS, PROT and implicit ftps in Surl.Protocol.Ftp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-178]
touches: [Surl.Protocol.Ftp.UnitLibrary, Surl.Protocol.Ftp.UnitTests]
requirement: FR-037
created: 2026-09-29
completed:
---
# BL-181 — Secure FTP with AUTH TLS, PROT and implicit ftps in Surl.Protocol.Ftp

## Goal

`FtpProtocolServer` answers `AUTH TLS`, `PBSZ`, `PROT C`/`PROT P` and `CCC` on `ftp://`, and
serves implicit `ftps://`, with TLS data connections when protection is private, as BL-173's
ADR decides, so `curl --ssl-reqd ftp://...` and `curl ftps://...` complete.

## Context

- Decisions: BL-173's ADR (replies, default protection on `ftps`, `CCC`, data-connection TLS);
  ADR-0010 (the server calls `IConnection.UpgradeToTlsAsync` itself after `234`, and discards
  every byte it has read beyond the end of the `AUTH` line before upgrading - the STARTTLS
  command-injection rule; `TlsSchemes.IsImplicitTls("ftps")`); ADR-0032 section 10 (with
  neither `--cert` nor `--self-signed` the upgrade command is refused in FTP's own words, and
  `PASS` then stays refused as plain-text without `--allow-plaintext-auth`).
- `ftps`: the engine completes the implicit handshake before `ServeAsync` (BL-065), so the server
  claims `ftps` too (or `Surl.Console` wraps it with `ImplicitTlsSchemeServer`, as BL-182
  decides); record which in Notes.
- Tests: `InMemoryConnection`'s upgrade simulation (ADR-0010: `UpgradeRequested`, a session to
  hand out, an option to make the upgrade throw) and BL-174's fake data connection with TLS.
- Fixtures: BL-173's recordings of `--ssl-reqd`, `--ftp-ssl-control`, `--ftp-ssl-ccc` and
  `ftps://`.

## Acceptance criteria

- [ ] A fast test replays each fixture named in Context and asserts surl's replies and upgrade
      points are the ADR's.
- [ ] Fast tests cover: bytes pipelined after `AUTH TLS` discarded, never run as commands; a
      failed upgrade; `AUTH TLS` when no certificate is configured; `PASS` accepted after the
      upgrade and refused as plain-text before it; `PROT P` making the next data connection TLS;
      `PROT P` before `PBSZ`; `CCC`.
- [ ] `dotnet build Surl.Protocol.Ftp.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
