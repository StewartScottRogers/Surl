---
id: BL-1287
title: Let a test's own protocol server declare FitsErrorReply so error fault rules reach it
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1246]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: FR-062
created: 2026-11-16
completed: 2026-11-16
---
# BL-1287 — Let a test's own protocol server declare FitsErrorReply so error fault rules reach it

## Goal

A `SurlProtocolServer` can declare `FitsErrorReply`, and an `error` fault rule scoped to its scheme or listener is accepted when the reply fits and reaches the server through `ExchangeContext.Faults`.

## Context

ADR-0172 (decided in BL-1246), amending ADR-0124 decision 2. Files: `Application/Surl.Api/CustomProtocolServers.cs` (`ToReadable`, `Read`, `WithScopeOf`), `SurlProtocolServer`, `Application/Surl.Api/README.md` ("Your own protocol server"), `Application/Surl.Api/CLAUDE.md` ("A test's own protocol servers"). `Surl.Cli.UnitLibrary` and its reply table are not changed.

## Acceptance criteria

- [x] `SurlProtocolServer` has an optional `Func<string, bool>? FitsErrorReply`, null by default; existing registrations behave as before.
- [x] Without it, an `error` rule scoped to the custom scheme is still an `ArgumentException`, at start and at `server.Faults.Add`.
- [x] With it, a fitting `reply` is accepted at start and at `Add`; a non-fitting one, or a predicate that throws, is an `ArgumentException` naming the rule.
- [x] A `Surl.Api.UnitTests` test shows a custom server's `error` rule reaching the server through `ExchangeContext.Faults` with its `Reply`, once per `times`.
- [x] A `Surl.Api.UnitTests` test covers an `AddOverride` answer of kind `error` for a custom scheme, with and without a fitting predicate, as ADR-0172 decision 5 says.
- [x] The README and `CLAUDE.md` passages say an `error` rule is refused unless the server declares `FitsErrorReply`.

## Notes

- Stand-in reply for the reader is `500` (fits `http`'s 400-599 shape); the real reply is restored after reading. Override with a custom scheme goes through `CustomProtocolServers.Read` so the same predicate judges it (ADR-0172 decision 5).

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. FitsErrorReply added; error fault rules reach a custom server
