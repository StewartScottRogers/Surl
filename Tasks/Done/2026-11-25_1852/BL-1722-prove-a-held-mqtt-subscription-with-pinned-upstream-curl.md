---
id: BL-1722
title: Prove a held MQTT subscription with pinned upstream curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1721]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Planning/Decisions/ADR-0014-how-the-mqtt-server-answers.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1722 — Prove a held MQTT subscription with pinned upstream curl

## Goal

`Integration` tests prove that pinned upstream curl 8.21.0 receives publishes made after it subscribed to `surl --mqtt-hold-subscription`.

## Context

- ADR-0014 Amendment 1 (BL-1715) says how a test seeds, observes and ends a held subscription; BL-1721 adds the option.
- Beside `Conformance/Surl.Conformance.UnitTests/UpstreamCurlSharesTopicsAcrossSurlListenersTests.cs`.
- A test learns the subscription is live from the `SUBACK` in the exchange's sent bytes, never from a sleep.

## Acceptance criteria

- [x] An `Integration` test subscribes with `curl mqtt://`, publishes over `nats` after the `SUBACK`, and curl exits 0 with the topic and payload on stdout (count 1).
- [x] An `Integration` test with count 0 and curl `--max-time` shows exit 28 with the live message on stdout.
- [x] An `Integration` test shows a plain fetch of a retained message without the option still exits 0 with that message.
- [x] The tests pass with the pinned build and skip as the neighbouring tests do where it is absent.

## Notes

- Tests: `Conformance/Surl.Conformance.UnitTests/UpstreamCurlHoldsASubscriptionOnSurlOverMqttTests.cs` (3). Expected
  stdout and exit 0 come from the `subscribe-t` recording; exit 28 is ADR-0014 Amendment 1's measured row.
- The `SUBACK` wait uses the control endpoint's `GET /v1/events/wait?kind=exchange_bytes`, because `SurlServer` has no
  wait that matches exchange bytes (`Exchanges` could only be polled, which means sleeping or spinning).
- The count 0 test uses `--max-time 20`, so it lasts 20 s: room under load, below the runner's 30 s limit.
- "Skip where absent" is the shared `PinnedUpstreamCurl` path (`Assert.Inconclusive`); not exercised here, since the
  pinned build is installed.
- Test-only change: no production code, so the `/feature` plan and implement stages had nothing to do. Code review run;
  its three findings applied or, for the index row, added to BL-1741.
- Added ADR-0014 to `touches`: its "not yet built: BL-1722" line became false. No task in Doing named that file.
  `Decisions/README.md` is named by BL-1725, so its row is BL-1741.
- Verified 2026-11-25: build clean with `-warnaserror`, format clean, fast tests 29249 passed, 3 skipped, the new
  tests 3 passed. Of the other `Integration` tests only the MQTT-related ones were run (31 passed with the new ones).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. Pinned upstream curl 8.21.0 receives a publish made after its SUBACK on surl --mqtt-hold-subscription: exit 0 at count 1, exit 28 with the message at count 0, and a plain retained fetch still exits 0.
