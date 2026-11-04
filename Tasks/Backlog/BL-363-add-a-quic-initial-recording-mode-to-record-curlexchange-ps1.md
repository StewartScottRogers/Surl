---
id: BL-363
title: Add a QUIC Initial recording mode to Record-CurlExchange.ps1
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1, Run-QuicInitialRecorder.cs]
requirement: none
created: 2026-11-04
completed:
---
# BL-363 — Add a QUIC Initial recording mode to Record-CurlExchange.ps1

## Goal

`Record-CurlExchange.ps1 -QuicInitial` binds a loopback UDP port and records every datagram that a
pinned upstream curl sends to it for `--http3-only`. It removes the Initial packet protection, using
the keys that RFC 9001 section 5.2 derives from the client's Destination Connection ID, and writes the
decrypted `CRYPTO` data: the TLS ClientHello, with its ALPN list and `quic_transport_parameters`.
BL-372 can then pin what curl offers before any QUIC server exists.

## Context

- RFC 9000 (QUIC) sections 17.2 (long header) and 19.6 (`CRYPTO`). RFC 9001 sections 5.2 to 5.4: the
  version 1 initial salt `0x38762cf7f55934b34d179ae6a4c80cadccbb7f0a`, `HKDF-Expand-Label` with
  `client in`, `quic key`, `quic iv` and `quic hp`, AEAD_AES_128_GCM, and AES-ECB header protection.
  RFC 9001 Appendix A.2 gives a worked client Initial to test against.
- Windows PowerShell's .NET Framework has no `HKDF` and no `AesGcm`. Do the decryption in a C#
  file-based app at the repository root started with `dotnet run --file`, as
  `Run-KerberosTestKdc.cs` and `Run-LibcurlRtspScript.cs` are. The planning name is
  `Run-QuicInitialRecorder.cs`. The script starts it and keeps `-Curl`'s pin check
  (`Assert-PinnedUpstreamCurl`).
- Nothing answers, so curl gives up. Pass `--connect-timeout` in `-CurlArgs` and record the exit code
  and stderr as every mode does.
- Builds whose `curl --version` lists `HTTP3`: the `linux-x64` and `osx-arm64` reference builds, and
  the `win-x64` supplementary 8.22.0_2 (ADR-0017) and static 8.21.0 builds. Upstream curl 8.21.0's
  `docs/cmdline-opts/http3-only.md` says: "This option makes curl fail if a QUIC connection cannot be
  established".
- The existing `-Tftp` mode is the script's other UDP recorder. Follow its shape for binding and idle
  timing.

## Acceptance criteria

- [ ] Fed the protected client Initial from RFC 9001 Appendix A.2, the decryption step produces that
      appendix's unprotected `CRYPTO` frame bytes. A check proves it and is documented in the help, or
      in a test the app's `--self-test` runs.
- [ ] Recording the static 8.21.0 Windows build with `-QuicInitial -CurlArgs -sS,-k,--http3-only,--connect-timeout,3,https://127.0.0.1:<port>/`
      writes `datagrams.bin` (each datagram length-prefixed), `clienthello.bin`, `stdout.bin`,
      `stderr.txt` and `exitcode.txt`. Notes record the ALPN list and transport parameters read from
      `clienthello.bin`, and the ALPN list includes `h3`.
- [ ] The `.PARAMETER` help documents `-QuicInitial` and its output files. Every other mode runs as
      before (spot-check `-Tftp` and the default HTTP mode).

## Notes

## Log

- 2026-11-04: Created.
