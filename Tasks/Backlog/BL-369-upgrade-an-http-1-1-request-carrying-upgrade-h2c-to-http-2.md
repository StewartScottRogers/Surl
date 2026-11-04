---
id: BL-369
title: Upgrade an HTTP/1.1 request carrying Upgrade: h2c to HTTP/2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-368]
touches: [Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-369 — Upgrade an HTTP/1.1 request carrying Upgrade: h2c to HTTP/2

## Goal

`surl http://` answers an HTTP/1.1 request that carries `Upgrade: h2c` and `HTTP2-Settings` (as
`curl --http2` sends on `http://`) with `101 Switching Protocols`, then serves that request's response
on HTTP/2 stream 1, as BL-364's ADR decides.

## Context

- Decision: BL-364's ADR, decision 3. It covers whether the upgrade is accepted, which requests
  qualify (for example, one with a body), and what happens to `Upgrade: h2c` on a TLS connection.
- RFC 9113 section 3.2 and RFC 7540 section 3.2 (the h2c Upgrade, which RFC 9113 deprecates but upstream
  curl 8.21.0 still sends: `docs/cmdline-opts/http2.md` at tag `curl-8_21_0` says "For HTTP, this means
  curl attempts to upgrade the request to HTTP/2 using the Upgrade: request header").
- Builds on BL-368's HTTP/2 connection. The `HTTP2-Settings` value is the base64url `SETTINGS` payload
  (RFC 7540 section 3.2.1) and is applied as the client's first `SETTINGS`.
- Fixtures: the `Upgrade` request BL-364 recorded from pinned upstream curl.

## Acceptance criteria

- [ ] Replaying the recorded `curl --http2 http://...` request yields `101 Switching Protocols` with
      `Connection: Upgrade` and `Upgrade: h2c`. Then follow the server's `SETTINGS` and the response on
      stream 1, and the client's preface is accepted after the `101`.
- [ ] A malformed `HTTP2-Settings` value, or an upgrade request the ADR does not accept, is answered
      over HTTP/1.1 as the ADR says, with a named test for each.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Protocol.Http` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
