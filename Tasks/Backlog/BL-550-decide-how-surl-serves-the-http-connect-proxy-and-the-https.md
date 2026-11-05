---
id: BL-550
title: Decide how Surl serves the HTTP CONNECT proxy and the HTTPS proxy
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed:
---
# BL-550 — Decide how Surl serves the HTTP CONNECT proxy and the HTTPS proxy

## Goal

An ADR decides how Surl acts as the HTTP proxy and the HTTPS proxy that upstream curl's `-x`,
`--proxy`, `--proxytunnel`, `--proxy-user` and the `--proxy-*` TLS options talk to: forwarding
absolute-form requests, tunnelling `CONNECT`, `Proxy-Authorization`, TLS between curl and the
proxy, where the code lives and how it reaches the origin.

## Context

- Scope: product overview, "Also in scope", "Proxies" (HTTP `CONNECT` proxy and HTTPS proxy).
  ADR-0080 decision 2 marks `http-proxy` and `https-proxy` `server-not-built`.
- Upstream test cases that wait on this (tag `curl-8_21_0`, commit
  `68720b4837284335b2d63cb358f8f6ce65f5bc55`, classified with ADR-0080's rules by BL-399):
  - `http-proxy` (24): 80, 83, 95, 275, 445, 714, 715, 744, 1078, 1230, 1287, 1288, 1297, 1319,
    1320, 1321, 1428, 1849, 1904, 2050, 2055, 2107, 3028, 3106;
  - `https-proxy` (5): 1586, 1630, 1631, 1632, 3401;
  - 715 and 2055 also need `socks5` (BL-557's family).
- Oracle: RFC 9110 section 9.3.6 (`CONNECT`), sections 7.1 and 11.7 (proxy authentication,
  `Proxy-Authenticate`, `Proxy-Authorization`, 407), RFC 9112 section 3.2.2 (absolute-form) and
  3.2.3 (authority-form); RFC 7616 (Digest), MS-NLMP (NTLM); curl 8.21.0's manpage,
  https://curl.se/docs/manpage.html (`-x`, `--proxytunnel`, `--proxy-user`, `--proxy-anyauth`,
  `--proxy-basic`, `--proxy-digest`, `--proxy-ntlm`, `--proxy-cacert`, `--proxy-insecure`,
  `--proxy-http2`); FILEFORMAT.md at the tag for `%PROXYPORT`, `<connect>` and the proxy's
  `<servercmd>` lines. Measure each byte with `Record-CurlExchange.ps1` against the pinned 8.21.0
  builds; never the Curl port (ADR-0003).
- Code to build on: `Protocols/Http/Surl.HttpMessage.UnitLibrary` (ADR-0070's shared request reader
  and response writer), `Foundation/Surl.Authentication.UnitLibrary` (Basic, Digest and NTLM
  checks the HTTP server already uses), `Foundation/Surl.Protocol.Abstractions.UnitLibrary`
  (`IDataConnectionOpener.ConnectActiveAsync` is FTP's outbound connection today),
  `Foundation/Surl.Networking.UnitLibrary` (`ServerTlsHandshake`, `SocketDataConnectionOpener`).
  Protocol servers never construct a socket and never reference each other (root `CLAUDE.md`).

Decide and record:

1. The library: planning assumption `Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary` with its
   `.UnitTests` twin, referencing `Surl.HttpMessage` and `Surl.Authentication`; amend ADR-0002
   decision 3's reference table and ADR-0070's list of `Surl.HttpMessage` users.
2. How the proxy reaches the origin without constructing a socket. Planning assumption: a new
   outbound-connection contract in `Surl.Protocol.Abstractions` (planning name
   `IOriginConnector`, connecting to a host name or an address and port), implemented in
   `Surl.Networking` by BL-551, shared with the SOCKS server; or reuse of
   `IDataConnectionOpener` if the ADR shows it fits. Include which origins may be reached
   (planning assumption: loopback only unless an option allows more, secure by default) and the
   refusal for others (status code, and log entry).
3. Forwarding: absolute-form requests, the headers the proxy removes and adds (`Via`,
   `Proxy-Connection`, hop-by-hop headers), persistent connections on both sides, and the answer
   when the origin is unreachable (502) or times out (504).
4. `CONNECT`: authority-form parsing, the 200 reply's exact bytes as upstream curl reads them, the
   refusal for a port the proxy will not tunnel to, and closing both sides.
5. `Proxy-Authorization`: Basic, Digest and NTLM, the 407 challenge and how accounts are named on
   the command line (planning assumption: the same account options the HTTP server uses, with a
   proxy-specific name).
6. The HTTPS proxy: TLS between curl and the proxy over `Surl.Networking`'s server TLS, the
   certificate it presents, ALPN (`http/1.1`; whether `h2` for `--proxy-http2` waits on Phase 6's
   HTTP/2), and TLS inside a `CONNECT` tunnel passed through unchanged.
7. The command line (planning assumption: `surl --proxy http://127.0.0.1:3128` and `https://` for
   the HTTPS proxy, mirroring curl's `--proxy`, since a bare `http://` listen URL is already the
   HTTP server) and each ADR-0006 limit the proxy applies.
8. How the upstream test cases' server half plays `http-proxy` and `https-proxy`: which parts
   (`<connect>`, `<data>`) the proxy takes from a case and which the origin (BL-391's HTTP server
   half) answers.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked
      "Decided by Claude under Stewart's delegation", answering decisions 1 to 8, each citing its
      RFC section or the curl 8.21.0 measurement it rests on, and it is listed in the folder's
      `README.md`.
- [ ] The ADR names the 29 cases above by number and the tasks that act on it: BL-551, BL-552,
      BL-553, BL-554, BL-555 and BL-556.
- [ ] ADR-0002's reference table and ADR-0070 are amended (or the ADR states why not) for the new
      library's references.

## Notes

- Planning assumptions above are BL-399's defaults for an unattended plan; the ADR may change any
  of them, and the build tasks' `touches` follow the ADR (a task records the change in its Notes).

## Log

- 2026-11-04: Created.
