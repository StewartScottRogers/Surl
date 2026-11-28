---
id: BL-2011
title: Keep another client's binding from feeding an AMQP 1.0 subscription queue
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
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

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
