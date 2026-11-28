---
id: BL-2011
title: Keep another client's binding from feeding an AMQP 1.0 subscription queue
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2011 — Keep another client's binding from feeding an AMQP 1.0 subscription queue

## Goal

A queue carrying a subscription description holds only messages its own topic filter copied in, whatever other
clients try to bind to it or unbind from it.

## Context

- `BusConnection.ConsumeSubscriptionQueue` (BL-1992) refuses a queue not bound to exactly the filter at consume
  time, and a described queue takes no direct send. `BindQueue` judges only `sub` on the filter being bound and
  `UnbindQueue` judges nothing, so an account with `sub` on another filter can bind it to a described queue, let
  messages queue up and unbind it; the subscriber then receives them. A client can likewise unbind a subscription's
  own filter, which stops it receiving until its next attach.
- AMQP 0-9-1 `queue.bind` and `queue.unbind` are the clients that can do this (the queue name is the hash
  `Amqp1KeptSubscription.QueueNameFor` gives). The AMQP 1.0 container itself binds right after declaring, with the
  one filter, and must keep working. Decide the rule (for example: a described queue takes one filter and no
  unbind) and record it in `Surl.MessageBus.UnitLibrary/CLAUDE.md`.

## Acceptance criteria

- [ ] `Surl.MessageBus.UnitTests`: a second, different filter bound to a described queue is refused and changes nothing, and no unbind removes a described queue's filter.
- [ ] `Surl.MessageBus.UnitTests`: declaring a described queue and binding its filter, once or again, still succeeds, and a queue with no description binds and unbinds as before.
- [ ] The known limit about an unbound filter's messages is gone from `Surl.MessageBus.UnitLibrary/CLAUDE.md` and the rule is stated there.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

Lane 2, 2026-11-27: no code written. Returned to Backlog because `touches` had to grow into a project
BL-2041 holds.

Why `touches` grew:

- `Surl.Extended.Protocol.Amqp1.UnitTests`:
  `ServeAsync_SubscriptionAnotherClientBoundFurther_IsRefusedUnauthorizedAndNothingIsSent`
  (`Amqp1ContainerConnectionTests.Subscriptions.cs`) has `bob` bind a second filter to a kept subscription
  and expects the next attach to be refused. The first acceptance criterion makes that bind fail, so the
  attach resumes the subscription and the test fails. Rewrite it to expect the refused bind, no
  `secrets` message queued and a resumed attach. The other binds there and in `...NoLocal.cs` are the
  declaring connection's first bind and keep passing under the rule below.
- Amqp1 library `CLAUDE.md`: "one another client bound further is `amqp:unauthorized-access`" stops
  being reachable through a client's bind.
- ADR-0146: record the rule as Amendment 6; Amendments 2 and 5 point at BL-2011 for it.

Design worked out from the code, to build next run (decide and record as Amendment 6):

- In `MessageBus.BindQueue` (`MessageBus.Queues.cs`), under the lock, on a queue with a description:
  every unbind is `AccessDenied`; binding a filter it already has is `Succeeded` and changes nothing; any
  other filter on a bound queue is `AccessDenied`. AMQP 0-9-1 already answers `AccessDenied` with 403.
- The first filter only from a connection that declared or redeclared the queue naming its description.
  Without this, another client can bind between the container's declare and bind. Keep every such
  connection, not just the last, so two shared links declaring at once both bind. A queue loaded with no
  binding is bound again after the container's redeclare.
- A queue with no description is unchanged. Restore and load set bindings directly and are unaffected.
- `MessageBusSubscriptionQueueTests` that build two-filter or unbound described queues through
  `BindQueue`/`UnbindQueue` (`binder`, `Rebind`) must change: unbound is a declare with no bind; two
  filters can only come from a `queues/index` file.
- In the library `CLAUDE.md`, remove both known limits: "a message another client's binding copied in,
  then unbound, stays" and "`UnbindQueue` asks for no grant, so a client ... can bind it, unbind".

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Backlog. touches now includes Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests (and the Amqp1 library CLAUDE.md), which BL-2041 in Doing holds; build after it, from the design in Notes
