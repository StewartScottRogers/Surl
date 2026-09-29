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
completed: 2026-09-29
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

- [x] `TlsVersionRangeTests` (or the plan's name for them) prove the default range maps
      to `SslProtocols.Tls12 | SslProtocols.Tls13`, `--tlsv1.3` alone maps to `Tls13`,
      `--tls-max 1.2` maps to `Tls12`, and lowest 1.3 with highest 1.2 throws
      `ArgumentException`.
- [x] A fast test proves the server-side authentication options carry
      `AllowRenegotiation == false` and no `CipherSuitesPolicy`.
- [x] A fast test proves a client offering only TLS 1.2 completes against the defaults,
      and one offering only TLS 1.3 completes where the platform supports TLS 1.3 (gate
      it with `[OSCondition]` if a platform lacks it).
- [x] A fast test with a hand-written `TimeProvider` proves a client that never sends its
      ClientHello is dropped once `HeadTimeout` passes, with nothing written and one log
      note.
- [x] `Record-CurlExchange.ps1` has the renegotiation-off switch; the two recordings
      above are committed under `Surl.Networking.UnitTests/Fixtures/<case>/` with the
      command line and build SHA-256 in a `README.md`, each showing `exitcode.txt` 0.
- [x] `dotnet build Surl.Networking.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Networking.UnitLibrary`.

## Notes

- Plan as built: a public `TlsVersionRange` (lowest, highest, `AcceptedProtocols`;
  `Default` is TLS 1.2 to 1.3) is the one place naming `SslProtocols.Tls`/`Tls11`, under
  `#pragma warning disable SYSLIB0039`. `ServerTlsSettings.AcceptedVersions` is an init
  property defaulting to `TlsVersionRange.Default`, rather than a fifth constructor
  argument, so the existing 40 constructions stay unchanged and `Surl.Console` can set it
  when it wires the `--tlsv1.x`/`--tls-max` values it already parses.
  `CreateAuthenticationOptions` sets `EnabledSslProtocols` from it and
  `AllowRenegotiation = false`, and never sets `CipherSuitesPolicy`. A bound that is not
  exactly one TLS version is `ArgumentOutOfRangeException` (an `ArgumentException`).
- Head timeout: `Surl.Core.ServingEngine.CompleteImplicitHandshakeAsync` already runs the
  handshake under a `CancellationTokenSource(HeadTimeout, timeProvider)` and writes the
  note "TLS handshake failed: no handshake within the head timeout of 30 s"; its test
  `ServeAsync_NoHandshakeWithinTheHeadTimeout_ClosesTheConnectionUnserved` pins that one
  note. `Surl.Networking` has no exchange log, so the new Networking test
  `UpgradeToTlsAsync_NoClientHelloWithinTheHeadTimeout_IsCutOffAndDisposingWritesNothing`
  proves the other half over a real `SslStream` with a hand-written `ManualTimeProvider`
  (copied from `Surl.Core.UnitTests`): cut off exactly at `HeadTimeout`, and disposing the
  connection writes nothing (the client reads EOF, no FIN-after-close_notify path).
- Tests: `TlsVersionRangeTests` (6 methods), three in `ServerTlsSettingsTests` (options
  carry `Tls12 | Tls13`, `AllowRenegotiation == false`, `CipherSuitesPolicy` null; a set
  range reaches the options; null refused), three in `StreamConnectionTlsTests` (TLS
  1.2-only client completes against the defaults; TLS 1.3-only server refuses a TLS
  1.2-only client, excluded on macOS like the existing TLS 1.3 test; head-timeout drop).
  The existing `UpgradeToTlsAsync_ClientAsksForTls13Only_NegotiatesTls13` covers the
  TLS 1.3-only client.
- Recorder: Windows PowerShell 5.1 runs on .NET Framework, whose `SslStream` cannot refuse
  renegotiation, so `-TlsRenegotiationOff` writes a small C# file-based TLS relay to
  `%TEMP%\SurlRecorder` and runs it with `dotnet run` (no Python, no package); it does the
  handshake with `AllowRenegotiation = false` and forwards plaintext to the recorder's own
  server on an ephemeral loopback port. Two traps found: .NET Framework sockets are
  inheritable, so the relay must start before the backend listener exists (it reads the
  backend port from standard input) or a stopped listener's accept never returns; and the
  standard input writer leads with a byte order mark, so the relay keeps only the digits.
- Measurement: both recordings exited 0 (TLS 1.3 negotiated against 1.2+1.3, TLS 1.2
  against 1.2 only), so ADR-0006 section 4 stands and no superseding ADR is needed. A
  check run of `--tls-max 1.1` through the relay still exited 35.
- `dotnet format --verify-no-changes` reports ENDOFLINE in
  `Surl.Protocol.Mqtt.UnitLibrary\MqttRetainedMessages.cs`, outside this task's touches;
  nothing in the Networking projects.
- Follow-up filed: BL-082 applies `--tlsv1.x`/`--tls-max` to `AcceptedVersions` in
  `Surl.Console`.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Surl.Networking accepts TLS 1.2-1.3 by default via TlsVersionRange, refuses renegotiation, keeps OS cipher suites; pinned curl 8.21.0 completes with renegotiation off (exit 0, TLS 1.2 and 1.3)
