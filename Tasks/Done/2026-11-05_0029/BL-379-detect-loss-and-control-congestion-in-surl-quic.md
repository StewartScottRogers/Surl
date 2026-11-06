---
id: BL-379
title: Detect loss and control congestion in Surl.Quic
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-378]
touches: [Surl.Quic.UnitLibrary, Surl.Quic.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-379 — Detect loss and control congestion in Surl.Quic

## Goal

`Surl.Quic` detects lost packets and recovers from them (RFC 9002). It keeps RTT estimates, uses the
probe timeout, retransmits the lost frames that RFC 9000 section 13.3 says to retransmit, and paces
sending with the congestion controller that BL-372's ADR chose. It runs entirely on an injected
`TimeProvider`.

## Context

- Decision: BL-372's ADR, decision 4 (the congestion controller; RFC 9002's NewReno is the
  specification's example).
- RFC 9002 sections 5 (RTT estimation), 6 (loss detection: packet and time thresholds, PTO), and 7
  (congestion control: slow start, recovery, persistent congestion). Appendix A and Appendix B are
  pseudocode to follow. RFC 9000 section 13.3 lists what is retransmitted and how.
- Builds on BL-378's streams. Never `Thread.Sleep`: timers come from `TimeProvider` (root
  `CLAUDE.md`). `ManualTimeProvider` in the test projects sets a timer's due time before counting it
  (BL-355).

## Acceptance criteria

- [x] Named tests drive a channel that drops chosen packets: a dropped 1-RTT packet holding `STREAM`
      data is detected by the packet threshold and its data is retransmitted; a lost tail is detected
      by PTO; a dropped Handshake packet is recovered; and `ACK`-only packets are never retransmitted.
- [x] RTT tests reproduce RFC 9002 section 5.3's `smoothed_rtt` and `rttvar` updates for a scripted
      series of ACKs.
- [x] Congestion-window tests show slow start, the reduction on loss, and the collapse to the minimum
      window on persistent congestion, for scripted ACK and loss sequences.
- [x] The 1 MiB stream test from BL-378 completes over a channel that drops 5% of packets, chosen
      deterministically.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

Built: `QuicRttEstimator` (RFC 9002 section 5, tick arithmetic), `QuicCongestionController`
(NewReno, Appendix B), `QuicLossDetection` (Appendix A: packet and time thresholds, PTO with
backoff, space discard), `QuicSentPacket`, and `QuicServerConnection.Recovery.cs` (retransmission
by RFC 9000 section 13.3, probes, ACK Delay decoding). The congestion window now limits every
in-flight packet; ACK-only packets go regardless. Tests: `QuicRttEstimatorTests`,
`QuicCongestionControllerTests`, `QuicLossDetectionTests`, `QuicServerConnectionTests.Recovery.cs`,
`QuicStreamTests.Loss.cs`, `QuicStreamTests.LossyExchange.cs` (the 1 MiB each way over
`UnitTestLossyChannel`, every 20th datagram dropped each way). Surl.Quic: 379 tests, 100% line and
branch coverage.

Decisions (taken under Stewart's delegation; the ADR is BL-687, since BL-470 held
`Documentation/Planning/Decisions` while this ran - rule 3):
- Every packet sent is recorded, ACK-only ones too (Appendix A); losing an ACK-only packet sends
  nothing, and it is outside the congestion window and arms no PTO.
- A PTO probe is one ack-eliciting packet: the space's waiting new data if any (section 6.2.4's
  SHOULD), else the oldest in-flight packet's retransmittable frames, else `PING`; it ignores the
  congestion window but never the amplification limit.
- Lost `MAX_DATA`, `MAX_STREAMS` and `MAX_STREAM_DATA` are sent with the current value;
  `MAX_STREAM_DATA` dropped once the stream has received everything; `STREAM` dropped after a
  reset; blocked frames, `RESET_STREAM` and `STOP_SENDING` resent as they were (simplest; the
  client reads the blocked ones as information only).
- A retransmitted `CRYPTO`/`STREAM` frame too long for the room left is split when the first part
  keeps at least 64 bytes (a sensible default: no tiny fragments).
- Persistent congestion is judged on the losses of one space at a time, consecutive packet numbers
  (RFC 9002 section 7.6 says across spaces; the server's spaces barely overlap in time).
- The congestion window grows on every acknowledgment; there is no application-limited check
  (section 7.8 is a SHOULD NOT; simplest first).
- An ack-eliciting Initial waits for a datagram the window and amplification limit let reach 1200
  bytes (RFC 9000 section 14.1).
- The client's ACK Delay is scaled by its `ack_delay_exponent` and capped at its `max_ack_delay`
  before use; PTO backoff stops doubling at 2^16.
- `HandleTimeout` acts on whichever timer is due: idle first, then the handshake timeout, then loss
  detection.

Test changes: the test client now acknowledges (`AckFrame`, `AcknowledgeOneRtt`), since the
window stops a server that is never acknowledged. `ReceiveDatagram_BeforeAddressValidation_...`
now reaches the amplification limit through probe timeouts: curl's four 1200-byte Initials give a
14400-byte budget, above the 12000-byte initial window.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.Quic detects lost packets (RFC 9002 thresholds and PTO), resends per RFC 9000 13.3 and limits sending with NewReno; 1 MiB each way completes over a 5%-loss path
