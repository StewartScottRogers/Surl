---
id: BL-2068
title: Say in the manual that a subscription queue also needs sub on its filters
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2068 — Say in the manual that a subscription queue also needs sub on its filters

## Goal

`surl --manual` says that a subscription queue needs more than its `$queue/<name>` grant.

## Context

- Found while working BL-2052, which added the rule to the `--topic-access` paragraph of `--aihelp messages`
  (`Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`).
- The `--topic-access` paragraph in `Application/Surl.Cli.UnitLibrary/ManualText.cs` (TOPICS AND QUEUES) does not
  say it. The rule is ADR-0146 Amendments 2 and 5, in
  `Protocols/Messaging/Surl.MessageBus.UnitLibrary/MessageBus.SubscriptionQueues.cs`.
- `ManualTextTests` pins the manual's lines, laid out to 79 columns.

## Acceptance criteria

- [ ] The `--topic-access` text in `ManualText` states that a get, consume, delete or purge by name of a subscription queue also needs `sub` on every topic filter it is bound to, and `ManualTextTests` pins the new lines.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
