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
completed: 2026-11-04
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

- [x] Fed the protected client Initial from RFC 9001 Appendix A.2, the decryption step produces that
      appendix's unprotected `CRYPTO` frame bytes. A check proves it and is documented in the help, or
      in a test the app's `--self-test` runs.
- [x] Recording the static 8.21.0 Windows build with `-QuicInitial -CurlArgs -sS,-k,--http3-only,--connect-timeout,3,https://127.0.0.1:<port>/`
      writes `datagrams.bin` (each datagram length-prefixed), `clienthello.bin`, `stdout.bin`,
      `stderr.txt` and `exitcode.txt`. Notes record the ALPN list and transport parameters read from
      `clienthello.bin`, and the ALPN list includes `h3`.
- [x] The `.PARAMETER` help documents `-QuicInitial` and its output files. Every other mode runs as
      before (spot-check `-Tftp` and the default HTTP mode).

## Notes

- Design: the PowerShell script only records (a UDP socket in a runspace, like `-Tftp`, sending
  nothing); `Run-QuicInitialRecorder.cs` (`dotnet run --file`) decrypts afterwards, since Windows
  PowerShell's .NET Framework has no `HKDF` or `AesGcm`. It decrypts every version 1 Initial
  (coalesced packets walked by Length), reassembles the CRYPTO frames of the first DCID by offset
  (a retransmitted byte keeps its first value) and writes the contiguous run from offset 0.
- Choices: `datagrams.bin` prefixes each datagram with a two-byte big-endian length (a UDP payload
  never exceeds 65535). `request.bin` is not written in this mode; `clienthello.txt` is added, a
  per-packet and per-frame summary plus the parsed ClientHello. Only QUIC version 1 is decrypted:
  curl's Initial measured below is version 1, and version 2 can be added when a build sends it.
- `--self-test` passes: RFC 9001 A.2's protected Initial decrypts to its CRYPTO frame plus PADDING
  (1162 bytes), packet number 2. Documented under `.PARAMETER QuicInitial`.
- Measured 2026-11-04, static 8.21.0 Windows build (ngtcp2 1.23.0, OpenSSL 4.0.1), port 18443:
  exit 28, stderr `curl: (28) Connection timed out after 3005 milliseconds`, stdout empty. Four
  1200-byte-class datagrams (packet numbers 0-3, payload 1131 bytes each, one DCID of 20 bytes, SCID
  of 20 bytes, no token); ngtcp2 splits and shuffles the CRYPTO frames across packets and resends
  them in packets 2 and 3, with PING frames. `clienthello.bin` is 1501 bytes.
  - ALPN list: `h3`, `h3-29`. No server_name (the URL names an IP literal).
  - Extensions in order: 0x0039 0x000a 0x0016 0x0017 0x0031 0x000d 0x002b 0x002d 0x0033 0x0010.
  - quic_transport_parameters, in order: initial_source_connection_id (0x0f) = the SCID;
    initial_max_stream_data_bidi_local (0x05) = 32768; initial_max_stream_data_bidi_remote (0x06)
    = 32768; initial_max_stream_data_uni (0x07) = 1048576000; initial_max_data (0x04) =
    1048576000; initial_max_streams_bidi (0x08) = 262144; initial_max_streams_uni (0x09) = 262144;
    version_information (0x11) = chosen 0x00000001, available 0x00000001.
- Spot checks with the same build: `-Tftp -TftpData 'hello\n'` read wrote `hello`, exit 0; the
  default HTTP mode recorded `GET /x HTTP/1.1` with Host, User-Agent curl/8.21.0 and Accept, exit 0.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Record-CurlExchange.ps1 -QuicInitial records curl's QUIC Initial datagrams and Run-QuicInitialRecorder.cs decrypts them to the ClientHello (ALPN h3, h3-29), self-tested on RFC 9001 A.2
