---
id: BL-048
title: Accept only TLS 1.2 and 1.3 by default and refuse renegotiation in Surl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-012]
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed:
---
# BL-048 — Accept only TLS 1.2 and 1.3 by default and refuse renegotiation in Surl.Networking

## Goal

`Surl.Networking`'s server-side TLS (BL-012) accepts TLS 1.2 and TLS 1.3 by default,
takes a lowest and a highest accepted version mapped to `SslProtocols`, never allows
renegotiation, runs the handshake under the exchange's head timeout, and ends a failed or
timed-out handshake with no bytes after the TLS alert and a verbose-log note - and pinned
upstream curl 8.21.0 is shown still to complete with renegotiation off.

## Context

- Specification: `Documentation/Planning/Decisions/ADR-0006-hardening-for-internet-facing-use.md`,
  section 4 ("TLS minimums"), with section 1's head-timeout rule ("On a new connection
  the clock starts at accept and includes the TLS handshake") and section 5 ("a secure
  refusal is a bare close": `Surl.Core` never hands a refused connection to TLS).
- Versions: lowest is one of 1.0, 1.1, 1.2 (default), 1.3; highest is one of 1.0, 1.1,
  1.2, 1.3 (default 1.3). Build the accepted `SslProtocols` from every version in the
  range. A lowest above the highest is an `ArgumentException` here; `Surl.Cli` turns it
  into `FailedInit` (2). `SslProtocols.Tls` and `Tls11` are obsolete (`SYSLIB0039`) and
  warnings are errors, so the one place that names them carries a
  `#pragma warning disable SYSLIB0039` with a comment citing ADR-0006 section 4. Whether
  the platform then negotiates 1.0 or 1.1 is the platform's (Windows 11 Schannel refuses
  both server-side by default); pin no test on it.
- `SslServerAuthenticationOptions.AllowRenegotiation = false` always. Cipher suites are
  the operating system's defaults: never set `CipherSuitesPolicy`.
- The handshake takes `ExchangeLimits.HeadTimeout` (BL-046's contract, measured on the
  injected `TimeProvider`) as a cancellation; a handshake that fails or times out ends the
  connection with no bytes written after the alert, and a note in the exchange log naming
  the failure.
- Measurement (ADR-0003): ADR-0006 recorded with the pinned build
  (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
  `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`) that `-sS -k`
  completes against TLS 1.2 only, 1.3 only, and both, and fails with exit 35 for
  `--tls-max 1.1`. Its recorder server calls `AuthenticateAsServer(…, checkCertificateRevocation: false)`,
  which leaves renegotiation allowed. Extend `Record-CurlExchange.ps1` with a switch
  (documented in its comment-based help) that serves with `AllowRenegotiation = false`,
  and record `-Tls -TlsProtocol Tls12AndTls13` and `-TlsProtocol Tls12` with it. If
  either does not exit 0, stop and file a `docs` task for a new ADR superseding that point
  of ADR-0006 section 4, as the ADR requires. No reply bytes are pinned from this
  measurement beyond the exit codes.
- Fast tests run `SslStream` over an in-memory duplex stream pair with a certificate
  from `CertificateRequest`, as BL-012 set up. Pass on Windows, Linux and macOS.

## Acceptance criteria

- [ ] `TlsVersionRangeTests` (or the plan's name for them) prove the default range maps
      to `SslProtocols.Tls12 | SslProtocols.Tls13`, `--tlsv1.3` alone maps to `Tls13`,
      `--tls-max 1.2` maps to `Tls12`, and lowest 1.3 with highest 1.2 throws
      `ArgumentException`.
- [ ] A fast test proves the server-side authentication options carry
      `AllowRenegotiation == false` and no `CipherSuitesPolicy`.
- [ ] A fast test proves a client offering only TLS 1.2 completes against the defaults,
      and one offering only TLS 1.3 completes where the platform supports TLS 1.3 (gate
      it with `[OSCondition]` if a platform lacks it).
- [ ] A fast test with a hand-written `TimeProvider` proves a client that never sends its
      ClientHello is dropped once `HeadTimeout` passes, with nothing written and one log
      note.
- [ ] `Record-CurlExchange.ps1` has the renegotiation-off switch; the two recordings
      above are committed under `Surl.Networking.UnitTests/Fixtures/<case>/` with the
      command line and build SHA-256 in a `README.md`, each showing `exitcode.txt` 0.
- [ ] `dotnet build Surl.Networking.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
