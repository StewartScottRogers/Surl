---
id: BL-814
title: Create Surl.Control with the loopback control endpoint's JSON requests and token check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests, Surl.slnx, Documentation/Planning/Decisions/ADR-0135-how-the-control-endpoint-writes-events-and-reads-requests.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-814 — Create Surl.Control with the loopback control endpoint's JSON requests and token check

## Goal

A new library `Surl.Control` answers the control endpoint's HTTP and JSON requests - inspect exchanges and
events, wait for one, add and remove fault rules, seed, reset, snapshot and restore - against an interface it
defines, checking a bearer token and a loopback peer on every request, so a non-.NET test suite can drive a
running `surl` with nothing but an HTTP client, and so it is unit tested without a network.

## Context

- Decision: BL-822's ADR decision 7 (request paths, JSON shapes, error answers, body bound, token rules). Planning
  paths: `GET /v1/exchanges` and `GET /v1/events` (query `scheme`, `listen_url`, `account`, `kind`, `after`),
  `GET /v1/events/wait` (with `timeout_ms`, bounded), `POST /v1/faults`, `GET /v1/faults`, `DELETE
  /v1/faults/{id}`, `POST /v1/mail`, `PUT /v1/files/{path}`, `POST /v1/messages`, `POST /v1/accounts`,
  `POST /v1/reset`, `POST /v1/snapshots`, `POST /v1/snapshots/{id}/restore`. Fault rules are given as the same
  `--fault` item text BL-785 reads, so there is one fault grammar.
- New projects: `Application/Surl.Control.UnitLibrary` (namespace `Surl.Control`) and
  `Application/Surl.Control.UnitTests`, added to `Surl.slnx` in the flat alphabetical run (the tests directly
  after the library), held to the four quality gates like every `*.UnitLibrary`. AOT is set by
  `Directory.Build.props`; JSON through `System.Text.Json` source generation (`JsonSerializerContext`), no
  reflection. No package.
- References: `Foundation/Surl.Protocol.Abstractions.UnitLibrary` (event records) and
  `Protocols/Http/Surl.HttpServing.UnitLibrary` (implement `IHttpRequestHandler`; `Http1ConnectionServer` reads
  heads, refuses malformed ones and writes answers - read its `CLAUDE.md`). It references no protocol server and
  not `Surl.Composition`: it defines the interface it drives (planning name `IControlTarget`: query and wait
  events, the fault plan, the store control), which `Surl.Composition` implements in BL-816 - so
  `ProtocolIsolationTests` stays green.
- Secure by default: every request without the right `Authorization: Bearer <token>` is refused (`401`) with the
  token compared by `CryptographicOperations.FixedTimeEquals`, after the same fixed delay as other refused logins
  on the injected `TimeProvider`; a non-loopback peer is refused (`403`) even with the token; request bodies are
  bounded; no answer echoes the token or a password; anything a request changes is published on the event stream.
- Stand-in: the loopback control endpoint; a subscriber of the event stream and a client of the fault plan and
  store control.

## Acceptance criteria

- [x] `Surl.slnx` lists both projects in order; `dotnet build Application/Surl.Control.UnitLibrary -warnaserror`
      is clean.
- [x] Named tests drive the handler through `InMemoryConnection` and `Http1ConnectionServer` with a fake
      `IControlTarget`: each path the ADR defines answers its JSON shape and calls the target as expected; a
      missing token, a wrong token and a non-loopback peer are refused as the ADR says; an oversized body and
      malformed JSON get the ADR's error; a wait past its bound is refused; no answer contains the token.
- [x] The fast tests are green and `Surl.Control.UnitLibrary` has 100% line and branch coverage, no method over
      complexity 10, and a CRAP score of at most 30.
- [x] `Application/Surl.Control.UnitLibrary/CLAUDE.md` states what the library holds and what it may reference.

## Notes

- Built: `ControlEndpoint` (`FeedEventsAsync` fills its own event log from `IControlTarget.ReadEventsAsync`;
  `ServeAsync(IConnection, ExchangeContext)` answers one connection through `Http1ConnectionServer` with the
  upload limit set to 16 MiB), `IControlTarget`, `IControlSnapshot`, `ControlFault`, `ControlMessage`,
  `ControlRefusalException`/`ControlRefusalKind` (400/404/409 from the target), `ControlToken` (SHA-256 then
  `FixedTimeEquals`), and internal routes, event log, JSON shapes (`ControlJsonContext`, source generation,
  snake_case) and the text-or-base64 rule.
- Decisions the ADR left open are recorded in ADR-0135 (decided by Claude under Stewart's delegation): `fault_kind`
  for the fault's kind, filters through the event's exchange, dropped counts including subscription gaps,
  `is_truncated` on a gap, a wait answers the first held match, peer before token, strict query keys and JSON
  members, JSON errors naming only a path, `Cache-Control: no-store`, no `Content-Length` on `204`.
- `touches` widened to the ADR file and `Documentation/Planning/Decisions/README.md`: the ADR is needed for the
  decisions above, and no task in Doing names either.
- `IControlTarget.ReadEventsAsync` replaces a dropped-count member: the endpoint counts subscription drops itself
  from gaps in `sequence`, so BL-816 only hands over a subscription with bytes, excluding the endpoint's own
  exchanges.
- The body bound uses the exchange's `MaxUploadBytes` (set to 16 MiB by `ServeAsync`) for chunked bodies, plus an
  up-front check of `Content-Length`, because the shared body discarder reads an announced body before it finds
  the length past its limit.
- The generated `ControlJsonContext` carries `[ExcludeFromCodeCoverage]` with its justification; with it the
  library measures 100% line and 100% branch coverage and no method above complexity 10 in Cobertura (the same
  figures `Measure-CodeQuality.ps1` reads), so CRAP is at most 10. 128 tests in `Surl.Control.UnitTests`.
- No upstream curl measurement: the control endpoint is Surl's own interface for test suites, not a protocol
  curl speaks (ADR-0124 decision 7).

## Log

- 2026-11-05: Created.
- 2026-11-05: depends-on narrowed from BL-805 to BL-823 at filing: Surl.Control drives its own interface, so it
  needs only the event contracts; BL-816 depends on BL-805 instead.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Control answers the loopback control endpoint's /v1/ requests against IControlTarget, checking a loopback peer and the bearer token on every request
