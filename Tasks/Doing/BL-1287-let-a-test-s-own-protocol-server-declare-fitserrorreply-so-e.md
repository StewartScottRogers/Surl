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
completed:
---
# BL-1287 — Let a test's own protocol server declare FitsErrorReply so error fault rules reach it

## Goal

A `SurlProtocolServer` can declare `FitsErrorReply`, and an `error` fault rule scoped to its scheme or listener is accepted when the reply fits and reaches the server through `ExchangeContext.Faults`.

## Context

ADR-0172 (decided in BL-1246), amending ADR-0124 decision 2. Files: `Application/Surl.Api/CustomProtocolServers.cs` (`ToReadable`, `Read`, `WithScopeOf`), `SurlProtocolServer`, `Application/Surl.Api/README.md` ("Your own protocol server"), `Application/Surl.Api/CLAUDE.md` ("A test's own protocol servers"). `Surl.Cli.UnitLibrary` and its reply table are not changed.

## Acceptance criteria

- [ ] `SurlProtocolServer` has an optional `Func<string, bool>? FitsErrorReply`, null by default; existing registrations behave as before.
- [ ] Without it, an `error` rule scoped to the custom scheme is still an `ArgumentException`, at start and at `server.Faults.Add`.
- [ ] With it, a fitting `reply` is accepted at start and at `Add`; a non-fitting one, or a predicate that throws, is an `ArgumentException` naming the rule.
- [ ] A `Surl.Api.UnitTests` test shows a custom server's `error` rule reaching the server through `ExchangeContext.Faults` with its `Reply`, once per `times`.
- [ ] A `Surl.Api.UnitTests` test covers an `AddOverride` answer of kind `error` for a custom scheme, with and without a fitting predicate, as ADR-0172 decision 5 says.
- [ ] The README and `CLAUDE.md` passages say an `error` rule is refused unless the server declares `FitsErrorReply`.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
