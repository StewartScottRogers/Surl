# ADR-0177 — `--topic` and `--topic-access` keep the canonical filter rule: `#` may stand at any level

- **Status:** Accepted
- **Date:** 2026-11-18
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-18, in
  BL-1379. No package, no threshold change, no download.
- **Amends:** [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decision 3.2 (confirmed, not changed: a canonical filter allows `#` at any level) and
  [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  (its refusal list, `(2) option --topic: topic <filter> is not a valid topic filter`, is unchanged).

## Context

Finding 3 of the Surly cookbook bug report of 2026-11-18: `surl --user ann:secret --topic "shop/#/orders"
mqtt://127.0.0.1:0/` starts and listens, while `surl --aihelp messages` says a filter that is not a valid
topic filter exits 2, and MQTT 3.1.1 section 4.7.1.2 allows `#` only as the last level.

The acceptance is deliberate. ADR-0100 decision 3.2 defines the canonical filter as the bus's, and the bus
allows `#` anywhere because AMQP 0-9-1 binding keys do (AMQP 0-9-1 section 3.1.3.3: in a topic exchange
binding key `#` matches zero or more words, at any position, `*` exactly one). `CanonicalTopicName.IsValidFilter`
accepts it and `MessageBusComposition.ComposeTopics` and `ComposeGrants` refuse only what it refuses. An MQTT
client can never subscribe with such a filter (`MqttTopicNames` refuses it, per 4.7.1.2), but an AMQP 0-9-1 client
binding `shop.#.orders` maps to exactly `shop/#/orders`.

`--topic` and `--topic-access` are Surl's own options (ADR-0102 decision 3). Neither the MQTT specification nor
upstream curl (whose URL path is the filter verbatim and never carries a mid-level `#`) decides their grammar
(ADR-0003: nothing to measure here).

## Decision

1. **Keep the canonical rule.** `--topic` and `--topic-access` accept a filter with `#` at any level, such as
   `shop/#/orders` or `#/b`. They are written in canonical names for every bus protocol, so refusing mid-level
   `#` would make an AMQP binding-key declaration or grant inexpressible, and a test that stands Surl in for
   both an AMQP and an MQTT client could not name its filter once.
2. **Refusals do not change.** A filter is still refused at start, exit 2, with ADR-0102's
   `(2) option --topic: topic <filter> is not a valid topic filter` and
   `(2) option --topic-access: topic <filter> is not a valid topic filter`: empty, over 65535 bytes, a U+0000,
   or a level that mixes `+` or `#` with other characters (`a/b#`, `a/+x`).
3. **What a filter with `#` before its last level does.** It is a legal declaration or grant. It matches
   per the bus's matcher; MQTT clients still cannot subscribe with it (their `SUBSCRIBE` refuses it, MQTT 3.1.1
   4.7.1.2), so for MQTT it only governs publishes whose topic the filter matches and the retained and QoS
   settings they get; AMQP 0-9-1 clients may bind it.
4. **The defect is the help text.** `surl --aihelp messages` says only that an invalid filter exits 2. BL-1381
   adds this sentence there, beside the `--topic` and `--topic-access` text:
   "A filter is MQTT's topic filter with one difference: `+` and `#` must each fill a whole level, and `#` may
   stand at any level here, not only the last as in an MQTT `SUBSCRIBE`, because an AMQP 0-9-1 binding key
   may place it anywhere; a filter that is empty, longer than 65535 bytes or breaks that rule exits 2."

## Consequences

- No code change, no new task; the test `CanonicalTopicNameTests.IsValidFilter_CanonicalFilter_IsTrue` keeps
  pinning `#/b`.
- BL-1381 adds the decision 4 sentence (and its `AiHelpProse` completeness tests).
