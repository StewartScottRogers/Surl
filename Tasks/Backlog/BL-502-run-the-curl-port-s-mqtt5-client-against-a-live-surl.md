---
id: BL-502
title: Run the Curl port's mqtt5 client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-975, BL-406, BL-976, BL-1096]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Mqtt5.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-502 — Run the Curl port's mqtt5 client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl mqtt5://`, `mqtt5s://` and `mqtt5+ws://`:
a QoS 1 publish, a subscribe that receives a retained message, a QoS 2 round trip, and a refused bad password.
Each assertion comes from MQTT 5.0, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help mqtt5` and its cases in `Curl.Conformance.Extended.UnitTests/Mqtt5/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.
- The port's subscribe reads until the server sends DISCONNECT, and the broker keeps subscribers by default
  (ADR-0093 decision 0), so the subscribe case starts `surl` with `--mqtt5-disconnect-after-retained`
  (decision 9).

## Acceptance criteria

- [x] The test class exists with the exchanges above over each transport, each passing on Windows with the pinned
      client present.
- [ ] `Documentation/Conformance/Extended/Mqtt5.md`: every row is covered by a named test or marked `n/a` with a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- 2026-11-12: The subscribe case needs `--mqtt5-disconnect-after-retained` (ADR-0093 decision 9), which BL-976 adds and is still in Backlog; the port reads until DISCONNECT, so without the flag the case hangs. Added BL-976 to depends-on.
- 2026-11-12: Wrote `CurlPortClientExchangesMessagesWithSurlOverMqtt5Tests` (12 cases over mqtt5, mqtt5s, mqtt5+ws, all passing with the pinned client): publish delivered to a raw subscriber, retained QoS 1 and QoS 2 messages read by the port's subscribe with `--mqtt5-disconnect-after-retained`, refused bad password. The pinned port publishes and subscribes at QoS 0 only, so the QoS 1/2 flows are driven by a raw MQTT 5 client in the test (acks asserted from the spec) and the port reads the retained result.
- 2026-11-12: Port defect (reported, not fixed): the pinned port exits 8 for CONNACK 0x86 where its own newer ADR-1251 maps it to 67; the test asserts surl's reason code 0x86 in the port's stderr and a non-zero exit.
- 2026-11-12: Mapped all 251 rows of Mqtt5.md to named tests (228 tests, 6 n/a); 17 rows have no unit test and are marked `UNCOVERED`, filed as BL-1096 (which also settles row MQTT5-2.2.2.2-1, 0x82 vs 0x81). Criterion 2 waits on it. The test class and the Mqtt5.md edit are uncommitted in the lane worktree.

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-501 replaced by BL-975 (BL-501 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Backlog. Waits on BL-976: the subscribe case needs --mqtt5-disconnect-after-retained, not yet implemented
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Backlog. Waits on BL-1096: 17 Mqtt5.md rows have no unit test yet; integration test class written (12 cases pass) but left uncommitted
