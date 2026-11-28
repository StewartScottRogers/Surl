---
id: BL-2040
title: Decide how a non-durable shared AMQP 1.0 subscription is held
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0146-how-the-amqp-1-0-container-serves-transactions-durable-subscriptions-and-source-filters.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2040 — Decide how a non-durable shared AMQP 1.0 subscription is held

## Goal

ADR-0146 states how the links of a `shared` source with `durable` `none` or `expiry-policy` `link-detach` compete
for each message.

## Context

Found while working BL-1990 (2026-11-27) by reading the code; nothing was run.

- ADR-0146 decision 3.5: a source whose `capabilities` include `shared` names a subscription several links consume
  at once, competing as queue consumers do, keyed by the link name up to the first `|` (with the `container-id`,
  or without it under `global`), and the container echoes the capabilities it applied. It is not limited to
  durable sources; decision 3.7 speaks of "a durable or shared subscription". A JMS
  `createSharedConsumer` is non-durable.
- The code makes a shared subscription only for a kept one: `Amqp1KeptSubscription.IsAskedFor` needs `durable`
  above 0 and a policy other than `link-detach`. Otherwise `Amqp1ContainerConnection.Links.cs`, `Subscribe`, makes
  an ungrouped live subscription, so every link gets every message and no capability is echoed.
- Choose between at least: the bus's fan-out group (`BusConnection.Subscribe(..., group:)`; note that group names
  and filters are one namespace with the other protocols' groups, and round robin ignores link credit), and an
  auto-delete, non-durable, non-exclusive bus queue like a kept subscription's. Decide also: the name, so that a
  non-durable attach never replaces or deletes a durable subscription of the same name; links asking different
  filters; who may (decision 3.7); no-local (BL-2037); and the echoed fields.
- BL-2041 builds it, in the AMQP 1.0 library only. If the answer needs a `Surl.MessageBus` change, file that task
  and add it to BL-2041's `depends-on`. Amend only by adding a dated amendment.

## Acceptance criteria

- [x] ADR-0146 has a dated amendment, marked "Decided by Claude under Stewart's delegation", answering each point in Context, with the alternative rejected and why.
- [x] The body of BL-2041 agrees with the amendment (edited if it does not), and any bus task it needs is filed and listed in its `depends-on`.
- [x] The diff to ADR-0146 only adds lines.

## Notes

- Decision (ADR-0146 Amendment 4): a non-durable shared subscription is an auto-delete, non-exclusive bus queue
  with a description, named `surl-subscription-nondurable-<SHA-256 of decision 3.5's key>`, consumed through
  `ConsumeSubscriptionQueue`. Why over the bus's fan-out group: waiting and released messages return to the other
  links, and group names are shared with MQTT 5 and NATS. No `Surl.MessageBus` change, so no bus task is filed.
- Read: Jakarta Messaging 3.1 sections 8.3.2 to 8.3.4, fetched from jakarta.ee on 2026-11-27 through a web-fetch
  agent. The bus and container behaviour cited was read in the code; nothing was run.
- Written in the run itself rather than delegated to `align-and-document`, as BL-2037 was: the decision needed the
  code already read. No `.cs` or project file changed, so no build or test run.
- BL-2041's body edited to match, with a fourth test criterion.
- Filed BL-2048 (Low): queue consumers are chosen by unacknowledged room, not link credit.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. ADR-0146 Amendment 4 decides a non-durable shared subscription is an auto-delete bus queue its links compete on, and BL-2041 matches it
