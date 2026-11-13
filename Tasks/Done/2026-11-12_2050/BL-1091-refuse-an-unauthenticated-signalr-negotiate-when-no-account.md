---
id: BL-1091
title: Refuse an unauthenticated SignalR negotiate when no account is configured
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1091 — Refuse an unauthenticated SignalR negotiate when no account is configured

## Goal

`surl signalr://` with no `--user` and no `--allow-anonymous` answers a negotiate with no credentials `401`, as
SignalR:Surl-5-1 requires.

## Context

- Found by BL-451: a live `POST /negotiate` with no login on a bare `surl signalr://127.0.0.1:0/` returns `200`.
- Cause: `SignalRRequestAnswerer` builds its `HttpAuthenticationRequest` with `IsWrite: false`, and
  `HttpAuthenticationSession` (`Foundation/Surl.Authentication.UnitLibrary`, line 180) lets a non-write request
  proceed anonymously when no account exists. Negotiate (`POST`) and the upgrade are logins, not reads, so
  secure-by-default needs them to count as writes.
- With an account configured the refusal already works; `CurlPortClientInvokesHubMethodsOnSurlOverSignalRTests`
  pins that case.

## Acceptance criteria

- [x] A unit test: negotiate with no credentials and no accounts is `401` unless `--allow-anonymous`.
- [x] A unit test: negotiate under `--allow-anonymous` still proceeds anonymously.
- [x] The Conformance test for the no-account case is added beside the with-account one and passes.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

Add `Conformance/Surl.Conformance.UnitTests` to `touches` when adding the live test.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Cause fixed in `SignalRRequestAnswerer`: every request is now judged `IsWrite: true` (negotiate, upgrade and the HTTP transports are all logins). Added `Conformance/Surl.Conformance.UnitTests` to `touches` for the live no-account test. The `--allow-anonymous` case is pinned by the existing `NegotiateAnonymouslyWhenAllowed` test.
- 2026-11-12: Doing -> Done. Negotiate and upgrade now count as logins; 401 with no account unless --allow-anonymous
