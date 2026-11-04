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
completed: 2026-11-04
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

- [x] `-TlsAlpn` takes a list of ALPN protocol IDs. Without it, the TLS server offers no ALPN, as it
      does today.
- [x] Run with the static 8.21.0 Windows build as
      `-Tls -TlsAlpn h2,http/1.1 -Raw -RawReply '\x00\x00\x00\x04\x00\x00\x00\x00\x00' -CurlArgs -sS,-k,--http2,https://127.0.0.1:<port>/`
      (the reply is an empty `SETTINGS` frame), the script writes a `request.bin` that starts with the
      24 bytes `PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n`. Notes record the command and the first 64 bytes in
      hex.
- [x] The agreed ALPN protocol is written where a recording keeps it (a file or stderr; choose one and
      document it in the help), so each recording states what was agreed.
- [x] The `.PARAMETER` help documents `-TlsAlpn`. `-Tls` without `-TlsAlpn` and `-TlsRenegotiationOff`
      are spot-checked and still record as before.

## Notes

- Choice: `-TlsAlpn` is `[string[]]` and each element is also split on commas, because
  `powershell -File` binds `h2,http/1.1` as one string while an in-process caller passes an array.
- Choice: the ALPN list rides on the existing `TlsRelay.cs` (now taking renegotiation allowed/refused,
  the ALPN IDs and an `alpn.txt` path as arguments). `-TlsAlpn` alone keeps renegotiation allowed, as
  plain `-Tls` does; with `-TlsRenegotiationOff` too, both apply. Both relay modes now accept `-Raw`
  (the raw session then gets no certificate and serves the relay's plaintext); other session modes and
  `-Reset` are still refused.
- Choice: the agreed protocol goes to `alpn.txt` in OutDirectory (one line per completed handshake:
  the ID or `none`; empty means no handshake completed), only when `-TlsAlpn` is given, so recordings
  without it are unchanged. The relay's stderr line also names it (`ALPN h2`).
- Measurement (2026-11-04, static-curl 8.21.0 win-x64,
  `C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`):
  `Record-CurlExchange.ps1 -Port 47362 -OutDirectory %TEMP%\bl362-h2 -Tls -TlsAlpn h2,http/1.1 -Raw -RawReply '\x00\x00\x00\x04\x00\x00\x00\x00\x00' -Curl <static> -CurlArgs -sS,-k,--http2,https://127.0.0.1:47362/`
  - relay: `handshake completed: Tls12, TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, ALPN h2`; `alpn.txt` = `h2`.
  - `request.bin` is 139 bytes; curl exit 16, stderr `curl: (16) Error in the HTTP2 framing layer`
    (the scripted server sends only SETTINGS, never a response).
  - First 64 bytes:
    `50 52 49 20 2a 20 48 54 54 50 2f 32 2e 30 0d 0a 0d 0a 53 4d 0d 0a 0d 0a 00 00 12 04 00 00 00 00 00 00 03 00 00 00 64 00 04 00 01 00 00 00 02 00 00 00 00 00 00 04 08 00 00 00 00 00 3e 7f 00 01`
    - the 24-byte preface, then a SETTINGS frame (length 18): MAX_CONCURRENT_STREAMS 100,
      INITIAL_WINDOW_SIZE 65536, ENABLE_PUSH 0; then WINDOW_UPDATE on stream 0 (increment 0x3E7F0001).
- Spot checks with the same build: `-Tls -CurlArgs -sS,-k,https://127.0.0.1:47363/` and the same
  with `-TlsRenegotiationOff` both record `GET / HTTP/1.1`, exit 0, and write no `alpn.txt`
  (the relay reports `ALPN none`).
- `UpstreamCurlBuilds.json`'s origin note for the static Windows build lists the uses it is pinned
  for (SMB, Negotiate, SSH algorithms); HTTP/2 over TLS is not among them. That file is outside this
  task's `touches`; BL-364, which measures with it, records the new use.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Record-CurlExchange.ps1 -TlsAlpn offers ALPN via the TLS relay, works with -Raw, writes alpn.txt; h2 preface recorded from static-curl 8.21.0
