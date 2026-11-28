---
id: BL-2070
title: Say in the CommandStageFaults summary when a hold ends early and who ends a dropped exchange
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary/CommandStageFaults.cs]
requirement: none
created: 2026-11-28
completed:
---
# BL-2070 — Say in the CommandStageFaults summary when a hold ends early and who ends a dropped exchange

## Goal

The class summary of `CommandStageFaults` states truly, for each overload, when a `delay` hold ends and who ends a
dropped exchange.

## Context

Found while working BL-2053 (2026-11-28), read from
`Foundation/Surl.Protocol.Abstractions.UnitLibrary/CommandStageFaults.cs`. Doc comments only; no behaviour change.
References: ADR-0141 and its amendments 1, 2 and 3. The library's `CLAUDE.md` already describes the overload without
a connection.

The class summary says a `delay` rule holds the answer for the rule's `ms`. It omits that
`FindAndApplyAsync(context, connection, point, target)` and `FindAndApplyWithClientOutcomeAsync` watch the
connection and end the hold early when the client closes or resets it. It also says a `drop` rule aborts the
connection, which is true of those two only: `FindAndApplyAsync(context, point, target)` notes and publishes the
drop, hands the rule back and leaves ending the exchange to the caller. It watches no connection, so its hold lasts
until the delay passes or the exchange is cancelled.

It adds no store, event or fault, so the stand-in questions do not apply.

## Acceptance criteria

- [ ] The class summary says the overloads that take an `IConnection` end a `delay` hold early when the client closes or resets the connection.
- [ ] The class summary says those overloads abort the connection on a `drop` rule, and that the overload without one hands the rule back for the caller to end the exchange.
- [ ] The diff to `CommandStageFaults.cs` changes comment lines only.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
