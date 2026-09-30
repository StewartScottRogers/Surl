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
completed: 2026-09-30
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

- [x] A fast test replays each fixture named in Context and asserts surl's replies and upgrade
      points are the ADR's.
- [x] Fast tests cover: bytes pipelined after `AUTH TLS` discarded, never run as commands; a
      failed upgrade; `AUTH TLS` when no certificate is configured; `PASS` accepted after the
      upgrade and refused as plain-text before it; `PROT P` making the next data connection TLS;
      `PROT P` before `PBSZ`; `CCC`.
- [x] `dotnet build Surl.Protocol.Ftp.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- **Fixtures recorded here.** BL-173 recorded the TLS rows of ADR-0052 but left no fixture
  folders, so this task recorded `ssl-reqd`, `ftp-ssl-control`, `ftp-ssl-ccc` and `ftps` on
  2026-09-30 with `Record-CurlExchange.ps1 -Ftp` and the pinned win-x64 curl 8.21.0, feeding it
  surl's exact replies (Fixtures/README.md, "Secure FTP (BL-181)"). All four exited 0.
  `RecordedTlsTests` replays them; `FtpTlsTests` covers the rest.
- **`ftps` is claimed by `FtpProtocolServer.Schemes` directly**, as Gopher and MQTT claim their
  TLS schemes, not wrapped by `ImplicitTlsSchemeServer`: the server needs the scheme anyway for
  the default protection level (`P` on `ftps`, `C` on `ftp`, from `ExchangeContext.Scheme`), and
  the engine already completes the implicit handshake before `ServeAsync`. BL-182 composes it.
- **Certificate known through a constructor flag**, `isAuthTlsAvailable` (default `false`), the
  same shape as `SmtpProtocolServer`'s `isStartTlsAvailable`; BL-182 passes it from `--cert` /
  `--self-signed`. `FEAT` adds ` AUTH TLS`, ` PBSZ`, ` PROT` after ` UTF8` only then.
- **Data-connection handshake after the `150`** (ADR-0052 decision 5 says only "as soon as the
  connection is open"): the data connection is still opened before `150`, and the handshake runs
  right after it. That is the order the recorder used, which pinned curl completed for
  `--ssl-reqd` and `ftps://`, and it cannot deadlock whether curl starts TLS before or after
  reading the `150`. A failed handshake (`TlsHandshakeException` or `IOException`) resets the
  data connection and answers `425 Cannot open data connection` in place of `226`; an upload
  that fails it leaves nothing written. The handshake waits on the exchange's token, so the
  idle timeout bounds a peer that never sends its ClientHello.
- **Replies not in ADR-0052's table**, chosen from RFC 4217 section 9: `PROT` with a level
  other than C, S, E or P is `504 Protection level not understood`; `AUTH`, `PBSZ` or `PROT`
  with no argument is `501 Syntax error in arguments`. `CCC` stays outside the before-login set
  (ADR-0052 decision 1), so before login it is `530`.
- An `AUTH` after a login keeps the login: RFC 4217 leaves it to the server, and curl always
  sends `AUTH` first.
- No new ADR: every choice above refines ADR-0052 decision 5 inside this library.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl answers AUTH TLS/SSL, PBSZ, PROT C/P and CCC (refused) on ftp://, serves implicit ftps://, and runs TLS on data connections under PROT P, replayed against four pinned-curl recordings
