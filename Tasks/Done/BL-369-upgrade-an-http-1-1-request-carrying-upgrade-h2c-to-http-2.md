---
id: BL-369
title: Upgrade an HTTP/1.1 request carrying Upgrade: h2c to HTTP/2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-368]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.HttpServing.UnitLibrary, Application/Surl.Cli.UnitLibrary]
requirement: none
created: 2026-11-04
completed: 2026-11-04
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

- [x] Replaying the recorded `curl --http2 http://...` request yields `101 Switching Protocols` with
      `Connection: Upgrade` and `Upgrade: h2c`. Then follow the server's `SETTINGS` and the response on
      stream 1, and the client's preface is accepted after the `101`.
- [x] A malformed `HTTP2-Settings` value, or an upgrade request the ADR does not accept, is answered
      over HTTP/1.1 as the ADR says, with a named test for each.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Protocol.Http` keeps
      100% line and branch coverage.

## Notes

- Touches grew by `Protocols/Http/Surl.HttpServing.UnitLibrary` (`HttpRequest.Http1Version`, set by
  `Http1RequestResponder`, so an HTTP/1.0 `Upgrade` is ignored per RFC 9110 section 7.8) and
  `Application/Surl.Cli.UnitLibrary` (`AiHelpProse`'s http paragraph said h2c was not answered yet).
  No task in Doing names either (BL-373: Quic; BL-390: Conformance).
- Decisions recorded in ADR-0091 (Decided by Claude under Stewart's delegation). Fixtures recorded
  from the pinned static curl 8.21.0 with `Record-CurlExchange.ps1`: `h2c-upgrade-get-file`,
  `h2c-upgrade-post-body`.
- `Content-Length: 0` counts as no body and still upgrades: sensible default, it announces nothing.
- `Surl.HttpServing.UnitLibrary`'s own tests leave it below 100% (body discarder, drainer, answer
  writer); those gaps predate this task and its lines are covered by `Surl.Protocol.Http.UnitTests`.
  `Surl.Protocol.Http.UnitLibrary` is at 100% line and branch.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. curl --http2 http:// is answered 101 and served as HTTP/2 stream 1 (ADR-0091); malformed or unaccepted upgrades answered HTTP/1.1
