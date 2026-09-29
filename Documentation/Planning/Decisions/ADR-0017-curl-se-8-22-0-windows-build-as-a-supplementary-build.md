# ADR-0017 — curl.se's 8.22.0 Windows build as a supplementary build, for HTTP/2 and HTTP/3 only

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28.
  Stewart approved downloading the curl project's own Windows build on 2026-09-28 (BL-022).
  Asked whether to take the latest release, he chose on 2026-09-28 to take the latest only
  for this extra build and to keep 8.21.0 as the reference release (option "A" in the
  `/task-plan` session that filed BL-026). Which file, where it lives and how it is marked
  he left to this ADR.

## Context

ADR-0003 decision 2 makes 8.21.0 the reference release, because the Curl port targets it
and the last phase must compare like with like. Decision 4 pins Git for Windows' 8.21.0
build, which has no `smb`/`smbs` and neither `HTTP2` nor `HTTP3`. Decision 5 names an
upstream 8.21.0 build with SMB, HTTP/2 and HTTP/3 as needed, with the curl project's own
Windows build from curl.se as the candidate, and says the SMB and HTTP/2 and HTTP/3
servers are not validated until one is pinned.

curl.se's Windows page no longer offers an 8.21.0 build. What was checked on 2026-09-28:

- https://curl.se/windows/ offers curl 8.22.0, build `8.22.0_2` (release date
  2026-09-02). The x64 file is
  `https://curl.se/windows/dl-8.22.0_2/curl-8.22.0_2-win64-mingw.zip`, with the published
  SHA-256 `7c8c6b953b4eb2953d2bdc08cca1d5f09a964e9f86c361693559400c9a6d6db0`.
- The archive downloaded from that URL hashes to
  `7C8C6B953B4EB2953D2BDC08CCA1D5F09A964E9F86C361693559400C9A6D6DB0`, matching the
  published value.
- Its `bin\curl.exe` hashes to
  `B028548A8C0D3DC2C899FE415DCE1EF1D2406801B3ADC43DF9921683026E86CC`, and its full
  `curl --version` output is:

  ```
  curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 LibreSSL/4.3.2 zlib/1.3.1.zlib-ng brotli/1.2.0 zstd/1.5.7 WinIDN libpsl/0.23.3 libssh2/1.11.1 nghttp2/1.70.0 ngtcp2/1.25.0 nghttp3/1.18.0 WinLDAP
  Release-Date: 2026-09-02
  Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt mqtts pop3 pop3s rtsp scp sftp smtp smtps telnet tftp ws wss
  Features: alt-svc AsynchDNS brotli HSTS HTTP2 HTTP3 HTTPS-proxy HTTPSIG HTTPSRR IDN IPv6 Kerberos Largefile libz NativeCA proxy-HTTP3 PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd
  ```

- The build has **HTTP/2 and HTTP/3 but not SMB**: neither `smb` nor `smbs` is among its
  protocols, and `NTLM`, which curl's SMB needs, is not among its features.

## Decision

1. **Pin the build as a supplementary build.** `UpstreamCurlBuilds.json` gains a `win-x64`
   entry for it with `"role": "supplementary"`, and the Git for Windows entry gains
   `"role": "reference"` and is otherwise unchanged. The file's `comment` describes the
   field; an entry with no `role` is a reference build, as `UpstreamCurlBuildPins` reads
   it. `release` stays `8.21.0`.
2. **This carries out ADR-0003 decision 5 with an 8.22.0 build rather than 8.21.0.** The
   curl.se's Windows page offers only its current release (its older download folders
   answered 403 when checked on 2026-09-28), and building an 8.21.0 ourselves or taking a
   third party's is a different decision. Stewart chose option A: the latest build for what the reference
   lacks, 8.21.0 for everything else. ADR-0003 is not edited; its reason for 8.21.0 still
   holds.
3. **A supplementary build is used only for the protocols and features its ADR names,
   never to second-guess the reference build.** For this build that is HTTP/2
   (`--http2`, `--http2-prior-knowledge`) and HTTP/3 (`--http3`, `--http3-only`) against
   Surl's HTTP servers. Every other exchange - HTTP/1.1 included - is measured with the
   reference build. Where the two builds would answer the same case differently, the
   reference build's answer stands and the supplementary build is not consulted for it.
   `UpstreamCurlLocator.Locate` returns a supplementary build only when asked for by role.
4. **It does not cover SMB.** The SMB server stays unvalidated, as ADR-0003 decision 5
   says, until a build with `smb` and `smbs` is pinned; that is filed as its own task.
5. **The binary lives outside the repository**, at
   `C:\UpstreamCurl\curl-8.22.0_2-win64-mingw\bin\curl.exe` - the archive unpacked as it
   ships into `C:\UpstreamCurl`. A machine-wide path rather than a per-user one, because
   `defaultPath` is read literally, with no environment variable expanded, and must be
   the same for every lane and every user on the machine. The archive's own folder name
   keeps the build number in the path, so a later build unpacks beside it rather than
   over it. The binary is never committed.

## Consequences

Good:

- The HTTP/2 and HTTP/3 servers can be validated against upstream curl on Windows now.
- `Record-CurlExchange.ps1 -Curl C:\UpstreamCurl\curl-8.22.0_2-win64-mingw\bin\curl.exe`
  runs the build with no script change: `Assert-PinnedUpstreamCurl` accepts any pinned
  SHA-256.

Costs and caveats:

- HTTP/2 and HTTP/3 are measured against 8.22.0, not 8.21.0, so the last phase compares
  the port's HTTP/2 and HTTP/3 against a release one newer than the one it targets. Where
  the two releases differ there, the difference is upstream's, and a port disagreement
  found in that phase is checked against the 8.22.0 changelog before it is filed.
- A machine without `C:\UpstreamCurl\...` has no supplementary build; conformance tests
  that need it report Inconclusive, as they do for any absent pinned build.
- SMB still needs a build. Pinning one is another download for Stewart to approve.
