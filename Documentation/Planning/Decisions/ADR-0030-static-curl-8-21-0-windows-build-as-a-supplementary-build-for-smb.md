# ADR-0030 — stunnel/static-curl's 8.21.0 Windows build as a supplementary build, for SMB only

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29.
  Stewart approved downloading a Windows upstream curl build with SMB on 2026-09-29
  (BL-076). Which build, where it lives and how it is marked he left to this ADR.

## Context

ADR-0003 decision 5 leaves the SMB server unvalidated until an upstream curl build with
`smb` and `smbs` is pinned. Neither Windows build pinned so far has them: Git for Windows'
8.21.0 reference build (ADR-0003 decision 4) and curl.se's 8.22.0 supplementary build
(ADR-0017, decision 4). The Linux and macOS reference builds, from stunnel/static-curl's
8.21.0 release (ADR-0016), do list `smb smbs`.

The same stunnel/static-curl release ships a Windows build. What was checked on 2026-09-29:

- `https://github.com/stunnel/static-curl/releases/tag/8.21.0` has
  `curl-windows-x86_64-8.21.0.tar.xz`, with the GitHub asset digest
  `sha256:291cc21a95df384d7d9739e47d75445608d4d51819cbf1c0f92ee4cf9a950d8b`.
- The archive downloaded with `gh release download` hashes to
  `291CC21A95DF384D7D9739E47D75445608D4D51819CBF1C0F92EE4CF9A950D8B`, matching it.
- Its `curl.exe` hashes to
  `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`, and its full
  `curl --version` output is:

  ```
  curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 OpenSSL/4.0.1 zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 c-ares/1.34.6 libidn2/2.3.8 libpsl/0.22.0 libssh2/1.11.1 nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0
  Release-Date: 2026-06-24
  Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns mqtt mqtts pop3 pop3s rtsp scp sftp smb smbs smtp smtps telnet tftp ws wss
  Features: alt-svc asyn-rr AsynchDNS brotli ECH HSTS HTTP2 HTTP3 HTTPS-proxy HTTPSRR IDN IPv6 Kerberos Largefile libz NativeCA NTLM PSL SPNEGO SSL SSLS-EXPORT SSPI threadsafe TLS-SRP Unicode UnixSockets zstd
  ```

- It is the reference release, 8.21.0, and has `smb`, `smbs` and `NTLM`, which curl's SMB
  needs. It has no `ldap` or `ldaps`, which the Git for Windows build has.

## Decision

1. **Pin the build as a supplementary build, for SMB only.** `UpstreamCurlBuilds.json`
   gains a `win-x64` entry with `"role": "supplementary"`, listed after the curl.se 8.22.0
   entry. Every other Windows exchange is still measured with the reference build, and
   HTTP/2 and HTTP/3 with the 8.22.0 build, as ADR-0017 decision 3 says; where builds
   would answer a case differently, the reference build's answer stands.
2. **Not a new reference build.** It lacks `ldap` and `ldaps`, so it cannot replace Git
   for Windows' build, and swapping the reference would re-measure every Windows
   exchange already pinned for no gain.
3. **Same source as the Linux and macOS pins.** SMB is measured against the same
   release, built by the same project, on all three platforms - so ADR-0003's
   like-with-like rule holds for SMB everywhere, which ADR-0017 could not give HTTP/2.
4. **The binary lives outside the repository**, at
   `C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe` - the archive unpacked as
   it ships into a folder named for the project, release and platform, beside ADR-0017's
   build. It is never committed.
5. **Finding it by protocol is its own task.** `UpstreamCurlLocator.Locate` returns the
   first verified build of a platform and role, which for `win-x64` supplementary is the
   8.22.0 build, which has no SMB. The SMB server's conformance tests need a way to ask
   for a pinned build whose `protocols` include the scheme they measure; BL-089 adds it.

## Consequences

Good:

- ADR-0003 decision 5 is carried out for SMB on Windows with the reference release.
- `Record-CurlExchange.ps1 -Curl C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`
  runs the build with no script change: `Assert-PinnedUpstreamCurl` accepts any pinned
  SHA-256.

Costs and caveats:

- A machine without `C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\` has no SMB build;
  conformance tests that need it report Inconclusive, as for any absent pinned build.
- Until BL-089 lands, a caller that locates the `win-x64` supplementary build by role gets
  the 8.22.0 build, not this one.
- A third-party build, even of unpatched upstream source, is trusted as far as
  stunnel/static-curl's build is; ADR-0016 already accepts that for Linux and macOS.
