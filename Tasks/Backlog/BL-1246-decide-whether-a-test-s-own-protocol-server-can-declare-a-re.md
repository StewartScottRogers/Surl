---
id: BL-1246
title: Decide whether a test's own protocol server can declare a reply shape for chosen-error fault rules
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-062
created: 2026-11-16
completed:
---
# BL-1246 — Decide whether a test's own protocol server can declare a reply shape for chosen-error fault rules

## Goal

An ADR, decided by Claude under Stewart's delegation, says whether a `SurlProtocolServer` a test registers through `Surl.Api` can take `error` fault rules, and if so how it declares its reply shape and receives the rule. The follow-up tasks are filed, or the ADR records why none is needed.

## Context

- Found by the second stand-in audit (2026-11-16). Root `CLAUDE.md`, "Open for extension": new protocols, faults and hooks plug into extension points. ADR-0124 decision 2 added a test's own protocol server (`SurlServerOptions.ProtocolServers`, `SurlProtocolServer(Scheme, Create)`, BL-813), and decision 4 checks every `error` rule's `reply` against its scheme's shape.
- The reply shapes are one table in `Application/Surl.Cli.UnitLibrary/FaultReplyShapes.cs` (`FitsByScheme`), which a built-in server joins with a row. A custom scheme has no row, so `Application/Surl.Api/CustomProtocolServers.cs` refuses an `error` rule scoped to it (`Application/Surl.Api/CLAUDE.md`, "A test's own protocol servers": "an `error` rule scoped so is refused, having no reply shape"). `delay`, `drop`, `refuse-login` and `out-of-space` already reach a custom server, through the engine, the policy and the stores; `ExchangeContext.Faults` reaches it too, so it could ask the plan itself.
- Questions: should `SurlProtocolServer` take an optional reply-shape predicate (a `Func<string, bool>`) that the `Surl.Api` reader uses for its scheme; does the control endpoint or `Surl.Mcp` need to know a custom scheme (they cannot register one); does a `SurlServer.Faults.AddOverride` answer of kind `error` work for it; or is the refusal right, and why. Secure by default: nothing widens unless the test asks for it.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/`, with the next free number, marked "Decided by Claude under Stewart's delegation", answers the questions and amends ADR-0124 decision 2.
- [ ] If the ADR opens it: one feature task per library it changes (expected `Surl.Api`, possibly `Surl.Cli.UnitLibrary`) is filed with `task-board.ps1 new`, depending on this task, with exact `touches`, its criteria naming a `Surl.Api.UnitTests` test where a custom server's `error` rule reaches the server through `ExchangeContext.Faults`. Each is listed in Notes.
- [ ] If the ADR keeps the refusal: `Application/Surl.Api/README.md`, section "Your own protocol server" or its equivalent, says so and why, in a docs task filed and listed in Notes.

## Notes

## Log

- 2026-11-16: Created.
