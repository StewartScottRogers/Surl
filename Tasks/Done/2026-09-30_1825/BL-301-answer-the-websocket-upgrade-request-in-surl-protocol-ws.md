---
id: BL-301
title: Answer the WebSocket upgrade request in Surl.Protocol.Ws
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-285, BL-293, BL-288]
touches: [Surl.Protocol.Ws.UnitLibrary, Surl.Protocol.Ws.UnitTests]
requirement: FR-048
created: 2026-09-30
completed: 2026-09-30
---
# BL-301 — Answer the WebSocket upgrade request in Surl.Protocol.Ws

## Goal

`WsProtocolServer` in `Surl.Protocol.Ws` implements `IConnectionProtocolServer` for `ws`, reads
upstream curl's upgrade request through `Surl.HttpMessage`, answers it with `101 Switching
Protocols` and the right `Sec-WebSocket-Accept`, or with the refusal BL-285's ADR decides for each
defect, and answers HTTP challenges through `IHttpAuthenticationSession`.

## Context

- Decisions: BL-285's ADR (which request is accepted, the `101` head, each refusal's status and
  fields, subprotocols and extensions, anonymous upgrades, the verbose notes); BL-281's ADR (the
  library the head is read and written with); ADR-0032 section 6 (challenges, one `WWW-Authenticate`
  field per value the session gives); ADR-0019 (`Server: surl`, the refusal deadline).
- Code: BL-293's types in `Surl.HttpMessage.UnitLibrary`; BL-288's accept-key computation; the
  csproj gains the `Surl.HttpMessage.UnitLibrary` reference. The HTTP server's handling of
  `IHttpAuthenticationSession` in `Surl.Protocol.Http.UnitLibrary/HttpRequestResponder.cs` is the
  pattern (not a reference: ADR-0002 decision 3).
- Limits: `--max-request-head` and the head timeout through the shared reader (ADR-0006), and the
  connection's other limits as BL-285's ADR says.
- After a `101`, this task ends the exchange with the ADR's close; BL-302 adds the message exchange.
- Fixtures: the upgrade requests of BL-285's cases recorded with `Record-CurlExchange.ps1` against the
  Windows reference build, committed as test data (upstream bytes only, ADR-0003).

## Acceptance criteria

- [x] Tests in `Surl.Protocol.Ws.UnitTests` replay the recorded upgrade request and answer it with the
      ADR's `101` head byte for byte, the accept key matching the recorded key; each refusal case of
      the ADR (wrong method, version, missing key or fields, unknown path, a request head past
      `--max-request-head`, the head timeout) answers as the ADR says; an `Authorization` field is
      judged by a fake `IHttpAuthenticationSession` and a `Challenge` verdict answers `401` with its
      fields; no test opens a socket.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ws.UnitLibrary`.

## Notes

- Built: `WsProtocolServer` (scheme `ws`; `wss` is BL-303's registration through
  `ImplicitTlsSchemeServer`), `WebSocketUpgradeResponder`, `WebSocketUpgradeChecks` (checks 4-9),
  `WebSocketUpgradeRefusal`, `WebSocketHttpStatuses` (101, 426) and `WebSocketLingeringClose`
  (decision 5). The csproj references `Surl.HttpMessage` and `Surl.Content`, both in ADR-0002's
  table as amended; `HttpStatus` lacks 101 and 426, so they are built here rather than widening
  `Surl.HttpMessage` outside this task's `touches`.
- Fixtures: `upgrade-101`, `basic-401`, `head-405`, `version-8-426`, recorded 2026-09-30 with
  `Record-CurlExchange.ps1 -Raw` against the pinned Windows build, each answered with the exact
  bytes surl sends, so curl completed the `101` (exit 0) and refused each refusal with 22 before a
  test pinned them (`Fixtures/README.md`). The other defects are the recorded request with one
  line edited.
- Defaults taken (ADR-0071 leaves them open):
  - After the `101`, until BL-302 builds the message exchange, the server sends the empty
    `CLOSE` (`88 00`), half-closes and lingers - the ADR's close. Measured: curl exits 0 and
    writes nothing.
  - The lingering close is bounded by one second only, as decision 5 says (no 1 MiB bound as
    the HTTP drainer has); it is used after refusals too.
  - A `401` keeps the connection unless the request is HTTP/1.0, lists `close` in `Connection`,
    or announces a body; then it is a refusal with `Connection: close`.
  - A login bound to the body (AWS SigV4) is checked against the SHA-256 of the empty body,
    since an upgrade `GET` has none; a request that announces one is refused 400 at check 7.
  - `WWW-Authenticate` values a `Proceed` verdict carries (Negotiate's mutual auth) are written
    on the `101` and on any later refusal, after the ADR's fields, as the HTTP server does.
  - The request path is the target without its query; any target that does not start with `/`
    is refused by the content store (`404`).
  - Check 10's `--ws-echo` exemption and the `503` connection-limit refusal writer are left to
    BL-303, which adds the option and the registration.
- `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ws.UnitLibrary`: 100% line, 100% branch,
  67 members, 0 failing, worst CRAP 10. 145 tests in `Surl.Protocol.Ws.UnitTests`.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. WsProtocolServer answers upstream curl's recorded upgrade with ADR-0071's 101, every decision-1 refusal, and 401/403 through IHttpAuthenticationSession
