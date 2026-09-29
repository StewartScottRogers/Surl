---
id: BL-053
title: Enforce the first-packet timeout, packet size and publish payload limit in Surl.Protocol.Mqtt
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-036, BL-046]
touches: [Surl.Protocol.Mqtt.UnitLibrary, Surl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-053 — Enforce the first-packet timeout, packet size and publish payload limit in Surl.Protocol.Mqtt

## Goal

The MQTT server (BL-036) closes with no bytes when the first packet is not complete
within the head timeout, and closes with no bytes on a packet over 1 MiB or a `PUBLISH`
payload over `MaxUploadBytes` - all from `ExchangeContext.Limits` - with what pinned
upstream curl 8.21.0 reports for each close recorded.

## Context

- Specification: `Documentation/Planning/Decisions/ADR-0006-hardening-for-internet-facing-use.md`,
  sections 1 (head timeout "or first packet (the binary-framed ones)", "Maximum framed
  message: MQTT packet", "Maximum upload: … an MQTT `PUBLISH` payload"), 3 (fixed error
  text) and 5 (the framed-protocol column and "MQTT closes without a reply": close with
  no bytes for a head timeout, a connection refusal, a framed message too long and an
  upload too large). Contract types come from BL-046.
- Head timeout: `HeadTimeout` on `ExchangeContext.TimeProvider`, from the start of
  `ServeAsync` until the first packet (`CONNECT`) is complete. Later packets are governed
  by `Surl.Core`'s idle timeout only.
- Packet size: `MaxMessageBytes` (default 1048576) counts the whole packet, fixed header
  included. Decide it from the fixed header and remaining length (MQTT 3.1.1 section
  2.2.3) before reading the body, so the server never reads past the limit. A `PUBLISH`
  whose payload length (remaining length minus variable header) exceeds
  `MaxUploadBytes` gets the same answer.
- No `IConnectionRefusalWriter`: a refusal is a bare close (ADR-0006 section 5).
- MQTT 3.1.1, the level upstream curl speaks, has no server-to-client `DISCONNECT` and
  no reason codes (those are MQTT 5.0), which is why ADR-0006 closes without a reply.
- Measurement (ADR-0003): record what the pinned build reports for each close - after
  an over-limit packet, after an over-limit `PUBLISH` (curl `-d @<file>` with a file
  over a lowered limit), and before `CONNACK` - with
  `Record-CurlExchange.ps1 -Raw` (BL-029); commit each under
  `Surl.Protocol.Mqtt.UnitTests/Fixtures/<case>/` as `EmbeddedResource` with the command
  line and build SHA-256 in its `README.md`, recording curl's exit code and stderr as the
  build reported them.

## Acceptance criteria

- [x] `HeadTimeoutTests.PartialConnect_AfterHeadTimeout_ClosesWithNoBytes` passes, and a
      test proves a connected client idle past `HeadTimeout` is not disconnected by it.
- [x] `PacketLimitTests.PacketOfExactlyTheLimit_IsAccepted` and
      `PacketLimitTests.PacketOneByteOverTheLimit_ClosesWithNoBytes` pass, the second
      proving no body byte was read.
- [x] `PublishPayloadLimitTests.PayloadOverMaxUploadBytes_ClosesWithNoBytes` passes, and
      a test proves `MaxMessageBytes = 0` and `MaxUploadBytes = 0` accept a 2 MiB
      `PUBLISH`.
- [x] Recordings for each case above are committed, with pinned upstream curl 8.21.0's
      exit code and stderr for each close.
- [x] `dotnet build Surl.Protocol.Mqtt.UnitLibrary -warnaserror` is clean, the fast tests
      are green with no `Integration` test in `Surl.Protocol.Mqtt.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Mqtt.UnitLibrary`.

## Notes

BL-036 (2026-09-28) already refuses a packet over `MaxMessageBytes` from its fixed header
before reading any body byte, and closes with no bytes: see `MqttPacketReader` and
`MqttProtocolServerTests.ServeAsync_PacketOfExactlyTheLimit_IsAccepted` /
`ServeAsync_PacketOverTheLimit_ClosesWithNoBytesBeforeReadingItsBody`. What is left here
is the head timeout, the `PUBLISH` payload limit, the recordings, and the test names the
criteria ask for.

Delivered 2026-09-29 (lane 1):
- Head timeout: `MqttProtocolServer.ReadFirstPacketAsync` reads the first packet under a
  token linked to `HeadTimeout` on `ExchangeContext.TimeProvider`; the timer is disposed as
  soon as the first packet is complete, so later packets are bounded only by the idle
  timeout (`ConnectedClientIdlePastTheHeadTimeout_IsNotDisconnected` checks no timer is
  left). A cancellation of the exchange's own token still propagates and is not taken for
  a head timeout. `Timeout.InfiniteTimeSpan` starts no timer.
- Payload limit (decision, within ADR-0006): `MqttPacketReader` reads a `PUBLISH` body's
  first two bytes (the topic name's length) on their own, computes the payload as remaining
  length less topic name and, above QoS 0, the two-byte packet identifier, and refuses it
  before reading more. Why: with `MaxMessageBytes = 0` and a large `MaxUploadBytes`, reading
  the whole body first would buffer an oversized payload before refusing it. A topic length
  that does not fit gives a negative payload and is left to the responder's "PUBLISH was
  malformed" close, and a body shorter than two bytes is not checked here for the same reason.
- `ServeAsync_LargeRemainingLengthNeverSent_ClosesMidPacket` now lifts `MaxUploadBytes` too,
  since its 256 MiB announced `PUBLISH` is now (correctly) refused by the payload limit; the
  two BL-036 packet-limit tests in `MqttProtocolServerTests` were replaced by
  `PacketLimitTests`, which also prove by read count that no body byte was read.
- Recordings (win-x64 reference build, SHA-256 0E7737...8778) under `Fixtures/`, documented
  in the shared `Fixtures/README.md` (the folder's existing convention, one README for all
  cases): `closed-before-connack` and `publish-closed-before-connack` exit 56 "curl: (56)
  Connection disconnected"; `packet-over-the-limit` (1 MiB `-d @file`) and
  `publish-payload-over-the-limit` (201-byte `-d @file`) exit 0 with empty stderr, because
  curl sends a QoS 0 `PUBLISH` and exits without waiting for a reply.
- Pipeline: delivered directly in the lane (plan, tests, implementation, verify) since the
  change is one library and its tests; `Measure-CodeQuality.ps1 -Library
  Surl.Protocol.Mqtt.UnitLibrary` reports 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. MQTT closes with no bytes on a first packet past the head timeout, a packet over MaxMessageBytes and a PUBLISH payload over MaxUploadBytes, with upstream curl's reports recorded
