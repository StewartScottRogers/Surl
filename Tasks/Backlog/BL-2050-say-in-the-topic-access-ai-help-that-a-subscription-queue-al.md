---
id: BL-2050
title: Say in the --topic-access AI help that a subscription queue also needs sub on its filters
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-2050 — Say in the --topic-access AI help that a subscription queue also needs sub on its filters

## Goal

`surl --aihelp` says that a subscription queue needs more than its `$queue/<name>` grant.

## Context

- Found by the code review of BL-2047 (2026-11-27).
- The `--topic-access` paragraph in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` says a queue is named
  `$queue/<name>`. Since ADR-0146 Amendments 2 and 5, a get, consume, delete or purge by name of a queue holding an
  AMQP 1.0 durable or shared subscription (`surl-subscription-...`) also needs `sub` covering every filter it is
  bound to.

## Acceptance criteria

- [ ] The `--topic-access` paragraph in `AiHelpProse` states that a get, consume, delete or purge by name of a subscription queue also needs `sub` on every topic filter it is bound to.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
