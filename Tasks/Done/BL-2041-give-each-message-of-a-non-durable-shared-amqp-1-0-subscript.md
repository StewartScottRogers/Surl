---
id: BL-2041
title: Give each message of a non-durable shared AMQP 1.0 subscription to one of its links
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2040]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md, Documentation/Planning/Decisions/ADR-0159-which-broker-held-state-a-reset-snapshot-and-restore-of-the-message-bus-cover.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2041 — Give each message of a non-durable shared AMQP 1.0 subscription to one of its links

## Goal

Two links attached to one `shared` topic source with `durable` `none` or `expiry-policy` `link-detach` receive each
published message once between them (ADR-0146 Amendment 4, BL-2040).

## Context

- Found while working BL-1990 (2026-11-27) by reading the code; prove it with a failing test first.
- Read ADR-0146 Amendment 4 first and do exactly that. In short: a bus queue named
  `surl-subscription-nondurable-` and the hex SHA-256 of decision 3.5's key, flags auto-delete only, with a
  description recording policy `link-detach`, bound to the topic filter and consumed by each link through
  `ConsumeSubscriptionQueue`, not exclusively, filtered at delivery. Another address or other filters while a link
  consumes: `amqp:resource-locked`. The bus's auto-delete removes it with its last link, so no delete at a detach
  and no `SessionSubscriptions` entry; an attach refused after the declare calls
  `DeleteQueueIfDescribed(..., ifUnused: true)`. `UnknownQueue` from the bind or consume: declare, bind and consume
  once more (a test can delete the queue from an `ISurlEventPublisher` on its declare event). A lookup attach (no
  source) never finds it. The echo has no `durable`, `expiry-policy` `link-detach`, no `timeout`, and `shared`
  (with `global` when asked). No-local is left out of the echo and the description. No `Surl.MessageBus` change.
- Today `Amqp1KeptSubscription.IsAskedFor` leaves such a source out and
  `Amqp1ContainerConnection.Links.cs`, `Subscribe`, gives each link its own live subscription.
  `Amqp1Terminus` already reads `IsShared` and `IsGlobal`. `Amqp1KeptSubscription`'s name says kept; rename what no
  longer says what it does.
- Shared tests to build beside:
  `Amqp1ContainerConnectionTests.Subscriptions.cs`. Matrix row `Amqp1:Surl-Subscriptions-2` in
  `Documentation/Conformance/Extended/Amqp1.md`.
- Stand-in: the shared message bus, so a message another protocol publishes is shared out the same way. No new
  event or fault.

## Acceptance criteria

- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: two links on one non-durable shared source receive each message exactly once between them, and the echoed source carries `expiry-policy` `link-detach`, no `durable`, and the applied `shared` (and `global`) capability but no no-local entry; the test failed before the change (or Notes say why this is no defect).
- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: after one link detaches the other receives every later message and what the first left unsettled, and after both detach, and after a refused attach, nothing of the subscription remains on the bus.
- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: a non-durable shared attach leaves a durable subscription of the same link name and its messages in place, and an unshared non-durable source still gets every message on each link.
- [x] `Surl.Extended.Protocol.Amqp1.UnitTests`: while a link consumes, an attach under the name with other filters is refused with `amqp:resource-locked`, and a queue deleted between the declare and the consume is made again.
- [x] The library's `CLAUDE.md` states the rule, and row `Amqp1:Surl-Subscriptions-2` cites the new tests.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Built as ADR-0146 Amendment 4 says; no new decision, so no ADR. No `Surl.MessageBus` change.
- Failing test first (2026-11-27): `ServeAsync_TwoLinksOnANonDurableSharedSource_...` got 0 of 4 messages (each
  link was a live subscription applying no-local) and, with the no-local entry removed for one run, 8 of 4 (every
  link got every message).
- `Amqp1KeptSubscription` is now `Amqp1SubscriptionQueue` (kept, or non-durable shared; `IsKept` tells them apart).
  `IsAskedFor` lost its dynamic-node check: its one caller asks only for topic nodes.
- `touches` gained ADR-0159: its table named the renamed type. No task in `Doing` names that file.
- The retry on `UnknownQueue` and the delete after a refused attach apply to the non-durable queue only; kept
  subscriptions behave as before. No delete is asked when the outcome is `UnknownQueue`, so a queue another link
  has just declared is left alone.
- Stand-in: the queue is on the shared bus, so any protocol's publish is shared out; no new event or fault.
- No upstream curl run: curl cannot speak `amqp1` (ADR-0083); tests replay bytes written from the specification.
- Review (code-reviewer, 2026-11-27): no behaviour defect. Its doc and test points are fixed here; ADR-0107's two
  stale statements are BL-2049 (BL-2036 held the file). Its race (another container's declare landing between
  this one's declare and bind) leaves an extra binding that BL-2011 (a described queue takes one filter) refuses.
- Seen, not touched: this lane's worktree has a `sequencer` directory dated 2026-11-11 (a BL-426 cherry-pick), so
  `git status` says a cherry-pick is in progress.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Links on a non-durable shared AMQP 1.0 topic source now share one auto-delete subscription queue, each message going to one of them
