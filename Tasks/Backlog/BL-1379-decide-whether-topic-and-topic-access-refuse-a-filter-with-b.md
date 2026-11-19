---
id: BL-1379
title: Decide whether --topic and --topic-access refuse a filter with # before its last level
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-18
completed:
---
# BL-1379 — Decide whether --topic and --topic-access refuse a filter with # before its last level

## Goal

A new ADR, marked "Decided by Claude under Stewart's delegation", decides whether `surl` refuses at start
(exit 2) a `--topic` or `--topic-access` filter with `#` before its last level, such as `shop/#/orders`, or keeps
accepting it as a canonical filter and says so in `--aihelp messages`.

## Context

- Finding 3 of the Surly cookbook bug report of 2026-11-18: `surl --user ann:secret --topic "shop/#/orders" mqtt://127.0.0.1:0/`
  starts and listens. The reporter expected exit 2, reading `surl --aihelp messages`
  (`Application/Surl.Cli.UnitLibrary/AiHelpProse.cs:247`: "Starts exit 2 for a filter that is not a valid topic
  filter"), with MQTT 3.1.1 section 4.7.1.2's rule that `#` is valid only as the last level.
- Today's acceptance is deliberate, not a missed check: ADR-0100 decision 3.2 ("A canonical filter ... MQTT allows
  `#` only as the last level; the bus allows it anywhere, because AMQP 0-9-1 binding keys do") and the Glossary's
  "canonical topic" row. `CanonicalTopicName.IsValidFilter`
  (`Protocols/Messaging/Surl.MessageBus.UnitLibrary/CanonicalTopicName.cs:42-43`, levels checked by
  `IsValidFilterLevel` at line 106) allows `#` at any level; `MessageBusComposition.ComposeTopics` and
  `ComposeGrants` (`Application/Surl.Composition.UnitLibrary/MessageBusComposition.cs:192` and `:266`) refuse only
  what it refuses, with ADR-0102's `(2) option --topic: topic <filter> is not a valid topic filter`.
  `CanonicalTopicNameTests.IsValidFilter_CanonicalFilter_IsTrue` pins `#/b` as valid.
- An MQTT client can never subscribe with such a filter (`MqttTopicNames.cs:90` refuses `#` before the last
  level), but an AMQP 0-9-1 client binding `shop.#.orders` maps to exactly `shop/#/orders`, so a `--topic ...;retain`
  or a `--topic-access` grant written that way is reachable from AMQP.
- Neither the MQTT 3.1.1 specification nor upstream curl decides `--topic`'s grammar: it is Surl's own option
  (ADR-0102 decision 3), and upstream curl never sends a filter with `#` mid-level since its URL path is the
  filter verbatim. The decision is between the specification-shaped MQTT rule and the bus's canonical rule.
- Stand-in: the option configures the one `MessageBus`; the ADR states what a test that declares or grants a
  filter for AMQP clients, MQTT clients or both may write.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` (next free number), marked "Decided by Claude under
      Stewart's delegation", decides whether `--topic` and `--topic-access` accept a filter with `#` before its
      last level, citing ADR-0100 decision 3.2, MQTT 3.1.1 section 4.7.1.2 and AMQP 0-9-1 section 3.1.3.3.
- [ ] If it refuses such filters, the ADR gives the exact `(2) option --topic: ...` and `(2) option --topic-access: ...`
      texts, and the lane files the `Surl.Composition` code task for it (depending on this task) and adds that
      task to BL-1381's `depends-on`.
- [ ] If it keeps them, the ADR gives the sentence BL-1381 adds to `--aihelp messages` (which filters are valid, and
      that `#` may stand at any level there, unlike in an MQTT `SUBSCRIBE`).
- [ ] ADR-0100 (and ADR-0102 if its refusal list changes) carries an amendment line pointing to the new ADR, and
      `Documentation/Planning/Decisions/README.md` lists it.

## Notes

- Recommendation to weigh (the ADR decides): keep the canonical rule. `--topic` and `--topic-access` are written in
  canonical names for every bus protocol, and refusing `#` mid-level would make an AMQP binding-key grant
  inexpressible. The defect is that `--aihelp messages` does not say what a valid filter is; BL-1381 fixes that.
- Decision record only: no code, no help text.

## Log

- 2026-11-18: Created.
