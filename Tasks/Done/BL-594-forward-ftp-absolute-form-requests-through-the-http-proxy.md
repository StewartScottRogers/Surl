---
id: BL-594
title: Forward ftp:// absolute-form requests through the HTTP proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-552]
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Documentation/Planning/Decisions/ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-04
completed: 2026-11-06
---
# BL-594 — Forward ftp:// absolute-form requests through the HTTP proxy

## Goal

The HTTP proxy answers an `ftp://` absolute-form `GET` (what curl sends for `curl -x http://proxy
ftp://host/path`) by fetching the file or listing over FTP from the origin and returning it as an
HTTP response, instead of ADR-0088's interim `501`.

## Context

- Decision: ADR-0088 decision 3 ("Schemes"). Measured with the pinned 8.21.0 build (BL-550):
  `GET ftp://origin.test/a.txt HTTP/1.1`, `Host: origin.test:21`, `Proxy-Connection: Keep-Alive`.
  Credentials in the URL arrive as `ftp://user:pass@host/` or as `Authorization: Basic`; measure
  which with `Record-CurlExchange.ps1` before pinning a byte.
- The proxy may not reference `Surl.Protocol.Ftp` (a server, and servers never reference each
  other): the FTP client half (USER, PASS, TYPE I, EPSV/PASV, RETR, LIST, QUIT) is hand-written in
  `Surl.Protocol.HttpProxy` over ADR-0088 decision 2's `IOriginConnector`, data connection included.
- Origin refusals as decision 2; FTP failures map to `502` (a refused login `502` too, never `401`,
  since the proxy is not the origin), a timeout `504`.

## Acceptance criteria

- [x] Named tests in `Surl.Protocol.HttpProxy.UnitTests` replay the measured request against an
      in-memory FTP origin and pin the `200` with the file's bytes, a directory listing, and the
      `502` and `504` refusals.
- [x] The interim `501` for `ftp://` is gone and ADR-0088's decision 3 is amended to say so.
- [x] `dotnet build` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-550.
- Touches widened (2026-11-06): the ADR-0088 file (the criterion amends it) and
  `Documentation/Planning/Decisions/README.md` (its row said "`ftp` `501` until BL-594"). No task
  in `Doing` named either.
- Measured with `Record-CurlExchange.ps1` on the Windows reference 8.21.0: both
  `ftp://user:pass@host/` and `-u` send the userinfo in the target **and** `Authorization: Basic`;
  no credentials send neither. Fixtures `ftp-forward-file`, `ftp-forward-listing`, `ftp-forward-502`.
- Decisions (ADR-0088 Amendment 4): userinfo first, then Basic, then `anonymous`/`ftp@example.com`;
  EPSV then PASV, data connection always to the origin's own host (never a `227` address, so a
  reply cannot redirect the proxy); whole path to `RETR`/`LIST`/`NLST` (no per-segment `CWD`),
  `;type=a|d` honoured; `200` chunked, or close-delimited when the request ends the connection;
  every FTP failure `502` (login refusal included), reply timeout `504`; only `GET` (others stay
  `501`), since that is all curl sends for a download through `-x`.
- Tests: `HttpProxyFtpTests`, plus `ProxyTargetTests` rows for ftp port 21 and userinfo.
  `Surl.Protocol.HttpProxy.UnitLibrary` line and branch coverage 100%; 248 tests in the project.

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. The HTTP proxy fetches ftp:// GETs over a hand-written FTP client and answers 200 with the file or listing, 502 on FTP failure, 504 on timeout
