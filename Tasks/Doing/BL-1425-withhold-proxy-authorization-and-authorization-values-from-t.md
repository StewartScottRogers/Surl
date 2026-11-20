---
id: BL-1425
title: Withhold Proxy-Authorization and Authorization values from the HTTP proxy's exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1419, BL-1421]
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1425 — Withhold Proxy-Authorization and Authorization values from the HTTP proxy's exchange log

## Goal

`surl -v -U alice:s3cret --proxy http://127.0.0.1:0/`, answering
`curl -x http://127.0.0.1:<port> -U alice:s3cret http://<origin>/`, logs the `Proxy-Authorization`
value (and any `Authorization` the client sends to the origin) in the redacted form BL-1417's ADR fixes.

## Context

- Bug report item: secrets in the `-v` log; the proxy carries two of them.
- `Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary`: `ProxyFieldRules.cs:20` names
  `Proxy-Authorization` as request-only, and `:121-122` maps it for the FTP-over-HTTP fetch, which reads
  `Authorization` (`FtpFetch.cs:74`). The proxy reads HTTP/1.x heads through `Surl.HttpMessage`, so
  BL-1421's redactor applies to the client side.
- What the proxy forwards to an origin goes over an origin connection; whether those bytes are logged,
  and so redacted, is the ADR's to say. `CONNECT` tunnels carry the client's own TLS after the `200`,
  which is not plaintext to surl.

## Acceptance criteria

- [ ] `HttpProxyProtocolServer` implements BL-1418's contract with BL-1421's redactor. A fast test in
      `Protocols/Http/Surl.Protocol.HttpProxy.UnitTests` shows a `GET http://origin/` with
      `Proxy-Authorization: Basic ...` logged in the ADR's form.
- [ ] A `CONNECT` with `Proxy-Authorization` is redacted, and the tunnelled bytes after `200` are
      logged as the ADR says (named test).
- [ ] An `Authorization` header forwarded to an origin is redacted wherever the ADR says origin-side
      bytes are logged (named test).
- [ ] `dotnet build Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary -warnaserror` is clean, the fast
      tests are green, and coverage stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
