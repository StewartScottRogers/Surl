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
completed:
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

- [ ] Tests in `Surl.Protocol.Ws.UnitTests` replay the recorded upgrade request and answer it with the
      ADR's `101` head byte for byte, the accept key matching the recorded key; each refusal case of
      the ADR (wrong method, version, missing key or fields, unknown path, a request head past
      `--max-request-head`, the head timeout) answers as the ADR says; an `Authorization` field is
      judged by a fake `IHttpAuthenticationSession` and a `Challenge` verdict answers `401` with its
      fields; no test opens a socket.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ws.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
