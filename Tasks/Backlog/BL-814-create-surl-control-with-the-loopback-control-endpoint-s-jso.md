---
id: BL-814
title: Create Surl.Control with the loopback control endpoint's JSON requests and token check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Application/Surl.Control.UnitLibrary, Application/Surl.Control.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-05
completed:
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

- [ ] `Surl.slnx` lists both projects in order; `dotnet build Application/Surl.Control.UnitLibrary -warnaserror`
      is clean.
- [ ] Named tests drive the handler through `InMemoryConnection` and `Http1ConnectionServer` with a fake
      `IControlTarget`: each path the ADR defines answers its JSON shape and calls the target as expected; a
      missing token, a wrong token and a non-loopback peer are refused as the ADR says; an oversized body and
      malformed JSON get the ADR's error; a wait past its bound is refused; no answer contains the token.
- [ ] The fast tests are green and `Surl.Control.UnitLibrary` has 100% line and branch coverage, no method over
      complexity 10, and a CRAP score of at most 30.
- [ ] `Application/Surl.Control.UnitLibrary/CLAUDE.md` states what the library holds and what it may reference.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: depends-on narrowed from BL-805 to BL-823 at filing: Surl.Control drives its own interface, so it
  needs only the event contracts; BL-816 depends on BL-805 instead.
