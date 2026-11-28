---
id: BL-2052
title: Say in the --topic-access AI help that a subscription queue also needs sub on its filters
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-2052 — Say in the --topic-access AI help that a subscription queue also needs sub on its filters

## Goal

`surl --aihelp` says that a subscription queue needs more than its `$queue/<name>` grant.

## Context

- Found by the code review of BL-2047 (2026-11-27).
- The `--topic-access` paragraph in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` says a queue is named
  `$queue/<name>`. Since ADR-0146 Amendments 2 and 5, a get, consume, delete or purge by name of a queue holding an
  AMQP 1.0 durable or shared subscription (`surl-subscription-...`) also needs `sub` covering every filter it is
  bound to.

## Acceptance criteria

- [x] The `--topic-access` paragraph in `AiHelpProse` states that a get, consume, delete or purge by name of a subscription queue also needs `sub` on every topic filter it is bound to.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- The paragraph also says that a get or consume still needs its `$queue/<name>` grant (a delete or purge asks for
  none), and that once any grant is given a queue bound to no filter is refused. Both read from
  `MessageBus.SubscriptionQueues.cs` and `TopicAccessPolicy.cs`.
- Pinned by `AiHelpTextTests.Answer_Messages_SaysASubscriptionQueueAlsoNeedsSubOnItsFilters`, seen failing before
  the change. Build clean, `dotnet format --verify-no-changes` clean on both files, 30656 fast tests passed.
- `/feature` stages: one sentence of help text, so the plan, test and wording were done in the session without
  `protocol-architect`, `test-writer`, `protocol-implementer` or `align-and-document`, to save tokens. `code-reviewer`
  reviewed it: no Must fix; its one Should fix (scope "bound to no filter" to a configured grant) is applied.
  No conformance audit: no option, exit code or wire byte changed.
- The manual omits the same rule: filed as BL-2062.

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. surl --aihelp messages says a get, consume, delete or purge by name of a subscription queue also needs sub on every filter it is bound to
