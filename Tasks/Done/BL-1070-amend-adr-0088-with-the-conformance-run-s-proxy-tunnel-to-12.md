---
id: BL-1070
title: Amend ADR-0088 with the conformance run's proxy tunnel to 127.0.0.1
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1070 — Amend ADR-0088 with the conformance run's proxy tunnel to 127.0.0.1

## Goal

`ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md` records, as an amendment marked "Decided by
Claude under Stewart's delegation", how the upstream test-case run reaches the origin through the proxy.

## Context

BL-556 composed the proxy server halves of the upstream test-case run. ADR-0088 decision 8 said the proxy reaches the
HTTP half through an in-memory connector. What BL-556 built instead: `HttpProxyUpstreamTestCaseServer` (in
`Surl.Protocol.HttpProxy`) takes an `IOriginConnector`, and `UpstreamTestCaseServerHalves` hands it
`Surl.Networking`'s `SocketOriginConnector` with `AllowedOriginRule.LoopbackOnly`. The server connects every tunnel to
`127.0.0.1` at the `CONNECT`'s port whatever host the request names, because upstream's `sws` does that (test 80 sends
`CONNECT test.80:%HTTPPORT`; test 1631 sends `CONNECT` to the FTP control port and then to the EPSV data port). An
in-memory connector could reach only the HTTP half. The tunnel opens only when the case's `connect` part is a `2xx`
response, and the proxy's `<proxy>` record stops, per connection, when its tunnel opens. The HTTPS proxy is the `https`
scheme with `ListenUrl.IsProxy`; the plaintext one the `http-proxy` scheme with `IsImplicitTls = false`, since `http` is
the origin's. This task was filed because BL-671 (in Doing) touches `Documentation/Planning/Decisions` when BL-556 ran.

## Acceptance criteria

- [x] ADR-0088 has an amendment stating the above, marked "Decided by Claude under Stewart's delegation".
- [x] The amendment says it supersedes decision 8's in-memory connector for the conformance run only.
- [x] `Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary/CLAUDE.md` is untouched by this task (BL-556 updated it).

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. ADR-0088 Amendment 10 records the conformance run's loopback proxy tunnel
