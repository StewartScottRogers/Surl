---
id: BL-362
title: Add a server ALPN list to Record-CurlExchange.ps1's TLS recording
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-11-04
completed:
---
# BL-362 — Add a server ALPN list to Record-CurlExchange.ps1's TLS recording

## Goal

`Record-CurlExchange.ps1 -Tls -TlsAlpn h2,http/1.1 ...` makes the recorder's TLS server offer those
ALPN protocol IDs. Once a pinned upstream curl agrees `h2`, the script records what curl sends as raw
bytes: the HTTP/2 connection preface, `SETTINGS` and the first `HEADERS`. BL-364 measures with this.

## Context

- ADR-0010's measurement table notes that the recorder offers no ALPN. The `-Tls` and session modes are
  documented at the script's `.PARAMETER Tls` and `.PARAMETER TlsRenegotiationOff`. Parameters are
  declared around lines 779-838.
- Windows PowerShell's .NET Framework `SslStream` has no ALPN (`SslServerAuthenticationOptions` arrived
  in .NET Core 2.1). The script already works around a missing .NET Framework feature for
  `-TlsRenegotiationOff`: it writes `TlsRelay.cs` under `%TEMP%\SurlRecorder`, starts it with
  `dotnet run`, and forwards the plaintext to its own server. Extend that relay with the ALPN list
  rather than writing another one.
- What a recording with `h2` agreed needs: the HTTP/1 reader would stop at the preface
  `PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n`, so the bytes must go to `-Raw` (`-RawReply`, `-RawReplyFirst`).
  Today `-TlsRenegotiationOff` refuses session modes, and `-Raw` is one. Make `-TlsAlpn` work with
  `-Raw`.
- h2c needs nothing new. Prior knowledge (`--http2-prior-knowledge` on `http://`) already records with
  `-Raw`, and the `Upgrade: h2c` request of `--http2` on `http://` already records with the default
  HTTP mode.
- Builds whose `curl --version` lists `HTTP2` (`UpstreamCurlBuilds.json`): the `linux-x64` and
  `osx-arm64` reference builds, and two `win-x64` supplementary builds: curl.se's 8.22.0_2 (ADR-0017)
  and stunnel/static-curl's 8.21.0 (`C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`).
  The Windows reference build lacks `HTTP2`. Upstream curl 8.21.0's `docs/cmdline-opts/http2.md`
  (tag `curl-8_21_0`) says: "For HTTPS, this means curl negotiates HTTP/2 in the TLS handshake. curl
  does this by default."

## Acceptance criteria

- [ ] `-TlsAlpn` takes a list of ALPN protocol IDs. Without it, the TLS server offers no ALPN, as it
      does today.
- [ ] Run with the static 8.21.0 Windows build as
      `-Tls -TlsAlpn h2,http/1.1 -Raw -RawReply '\x00\x00\x00\x04\x00\x00\x00\x00\x00' -CurlArgs -sS,-k,--http2,https://127.0.0.1:<port>/`
      (the reply is an empty `SETTINGS` frame), the script writes a `request.bin` that starts with the
      24 bytes `PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n`. Notes record the command and the first 64 bytes in
      hex.
- [ ] The agreed ALPN protocol is written where a recording keeps it (a file or stderr; choose one and
      document it in the help), so each recording states what was agreed.
- [ ] The `.PARAMETER` help documents `-TlsAlpn`. `-Tls` without `-TlsAlpn` and `-TlsRenegotiationOff`
      are spot-checked and still record as before.

## Notes

## Log

- 2026-11-04: Created.
