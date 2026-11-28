---
id: BL-1992
title: Let the Subscribe right on the topic filter alone admit an AMQP 1.0 durable or shared subscription
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1992 — Let the Subscribe right on the topic filter alone admit an AMQP 1.0 durable or shared subscription

## Goal

Under `--topic-access`, an AMQP 1.0 durable or shared subscription attach is served to an account granted
Subscribe on its topic filter, with no `$queue/...` grant, and gives no account any other queue or topic.

## Context

- ADR-0146 decision 3.7: a durable or shared subscription needs the `Subscribe` right on its topic filter, judged
  at every attach. The code asks more: `Amqp1ContainerConnection.Subscriptions.cs` (`MakeQueueAndConsume`) calls
  `BusConnection.Consume`, and `MessageBus.Consume` (`MessageBus.Queues.cs`) also judges `sub` on
  `$queue/<name>` (ADR-0102 decision 3.4), here `$queue/surl-subscription-<hex>`. A canonical filter has no
  prefix wildcard, so the only grants that cover it are the exact hashed name or one such as `$queue/+`, which
  opens every queue. No new ADR is needed: the fix makes the code do what decision 3.7 says.
- Recorded as "Known gap" in `Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/CLAUDE.md` and in the
  Notes of BL-960 (Done). `ServeAsync_DurableAttachWithTheTopicButNotTheQueueGranted_IsRefusedUnauthorized`
  (`Amqp1ContainerConnectionTests.Subscriptions.cs`) and row `Amqp1:Surl-Subscriptions-3` of
  `Documentation/Conformance/Extended/Amqp1.md` pin today's behaviour.
- The bus change is a separate `BusConnection` operation for consuming a subscription queue, for example
  `ConsumeSubscriptionQueue`, given the queue name, the subscription description and the topic filter. Under one
  hold of the bus's lock it checks that the queue carries that description (ADR-0146 decision 3.2) and is bound
  to exactly that filter, and it judges `sub` on the filter instead of on `$queue/<name>`. Anything else is
  refused. `Consume` and `GetFromQueue` do not change, so AMQP 0-9-1, STOMP and an AMQP 1.0 `/queue/` link still
  need the `$queue` grant.
- A client must not reach another client's subscription queue. `ClearStaleQueue` replaces a detached queue whose
  description differs, and `DeleteQueue` judges no grant, while `Judge` checks only the new attach's filter. So
  an account allowed topic Y but not X, using the container-id and link name of a subscription on X, deletes that
  queue today, and would also consume its replacement once this gap is closed. It must instead be refused before
  anything changes. `Amqp1KeptSubscription.FromDescription` gives the existing queue's filter.
- A subscription's identity stays the container-id and link name of ADR-0146 decisions 3.1 and 3.5; the account
  is not added to it.
- Stand-in: the shared store is the message bus. A test configures grants through
  `MessageBusConfiguration.AccessGrants` (`--topic-access`). Seeding, reset, snapshot, store change events and the
  out-of-space fault of a subscription queue are unchanged, so nothing new is published or injected.
- Filed while working BL-1959 (2026-11-27).

## Acceptance criteria

- [ ] `Surl.MessageBus.UnitTests`: the new operation consumes a described queue bound to exactly the filter for an account granted only `sub` on that filter, and refuses, adding no consumer, an account without that right, a queue with no or another description, and a queue bound to any other set of filters; `Consume` and `GetFromQueue` on the same queue still answer `AccessDenied` without the `$queue` grant.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: with only the topic granted, durable, `session-end` and shared attaches are served and receive a published message, and a durable one resumes after a detach; these replace `ServeAsync_DurableAttachWithTheTopicButNotTheQueueGranted_IsRefusedUnauthorized`.
- [ ] `Surl.Extended.Protocol.Amqp1.UnitTests`: an account without `sub` on an existing subscription's filter, attaching under its container-id and link name with another filter it is granted, the same filter, or no source, is refused `amqp:unauthorized-access`, and the queue, its binding and its messages are unchanged.
- [ ] The existing tests that an ungranted `/queue/<q>` link is refused `amqp:unauthorized-access` and a `/queue/surl-subscription-...` address `amqp:not-found` pass unchanged.
- [ ] `Amqp1.md` row `Amqp1:Surl-Subscriptions-3` names the new tests and not the removed one, the "Known gap" bullet is gone from the Amqp1 library's `CLAUDE.md`, and both libraries' `CLAUDE.md` state the access rule as built.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green with no test needing `TestCategory=Integration`, and both libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
