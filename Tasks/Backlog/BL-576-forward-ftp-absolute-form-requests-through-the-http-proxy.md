---
id: BL-576
title: Forward ftp:// absolute-form requests through the HTTP proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-552]
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-576 — Forward ftp:// absolute-form requests through the HTTP proxy

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

- [ ] Named tests in `Surl.Protocol.HttpProxy.UnitTests` replay the measured request against an
      in-memory FTP origin and pin the `200` with the file's bytes, a directory listing, and the
      `502` and `504` refusals.
- [ ] The interim `501` for `ftp://` is gone and ADR-0088's decision 3 is amended to say so.
- [ ] `dotnet build` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-550.

## Log

- 2026-11-04: Created.
