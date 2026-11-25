---
id: BL-1722
title: Prove a held MQTT subscription with pinned upstream curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1721]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1722 — Prove a held MQTT subscription with pinned upstream curl

## Goal

`Integration` tests prove that pinned upstream curl 8.21.0 receives publishes made after it subscribed to `surl --mqtt-hold-subscription`.

## Context

- ADR-0014 Amendment 1 (BL-1715) says how a test seeds, observes and ends a held subscription; BL-1721 adds the option.
- Beside `Conformance/Surl.Conformance.UnitTests/UpstreamCurlSharesTopicsAcrossSurlListenersTests.cs`.
- A test learns the subscription is live from the `SUBACK` in the exchange's sent bytes, never from a sleep.

## Acceptance criteria

- [ ] An `Integration` test subscribes with `curl mqtt://`, publishes over `nats` after the `SUBACK`, and curl exits 0 with the topic and payload on stdout (count 1).
- [ ] An `Integration` test with count 0 and curl `--max-time` shows exit 28 with the live message on stdout.
- [ ] An `Integration` test shows a plain fetch of a retained message without the option still exits 0 with that message.
- [ ] The tests pass with the pinned build and skip as the neighbouring tests do where it is absent.

## Notes

## Log

- 2026-11-25: Created.
