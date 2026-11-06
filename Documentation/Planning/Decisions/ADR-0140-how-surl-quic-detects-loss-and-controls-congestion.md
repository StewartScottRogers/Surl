# ADR-0140 — How Surl.Quic detects loss and controls congestion

- **Status:** Accepted
- **Date:** 2026-11-06 (the decisions were taken 2026-11-04 in BL-379; this records them)
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04, in BL-379;
  recorded by BL-687, since BL-470 held `Documentation/Planning/Decisions` while BL-379 ran.
- **Builds on:** [ADR-0082](ADR-0082-how-surl-serves-http-3-over-quic.md) decision 4 ("Loss detection and
  congestion control": RFC 9002 in full, NewReno, an initial window of 10 x 1200 bytes, no pacing beyond the
  window) and its amendment 2 (the closing and draining states, BL-608).
- **Acted on by:** BL-379.

## Context

ADR-0082 decision 4 says `Surl.Quic` runs RFC 9002's loss detection and NewReno, limited by the congestion
window and with no pacing. RFC 9002 leaves choices to the implementation (its SHOULDs and MAYs), and RFC 9000
section 13.3 says what is sent again but not how a server built from separate packet number spaces, streams and
flow-control credit does it. BL-379 built `QuicRttEstimator` (section 5), `QuicLossDetection` (section 6,
Appendix A), `QuicCongestionController` (section 7, Appendix B), `QuicSentPacket` and
`QuicServerConnection.Recovery.cs`, and took the decisions below. The library's `CLAUDE.md` ("Loss recovery")
describes the result as built.

Upstream curl is the client here and has no say in how a server recovers its own packets; every choice is
judged by RFC 9000 and RFC 9002, and the tests (`QuicLossDetectionTests`, `QuicCongestionControllerTests`,
`QuicRttEstimatorTests`, `QuicServerConnectionTests.Recovery.cs`, `QuicStreamTests.Loss.cs`,
`QuicStreamTests.LossyExchange.cs`) replay values those RFCs give.

## Decision

1. **Every packet sent is recorded, ACK-only ones too.** As RFC 9002 Appendix A.5 does, each sealed packet
   becomes a `QuicSentPacket` in its space, so an acknowledgment of any packet number is understood. A packet is
   ack-eliciting when it carries a frame other than `ACK`, `PADDING` and `CONNECTION_CLOSE`, and in flight when
   it is ack-eliciting or padded (section 2). Losing a packet that carried nothing to send again sends nothing;
   one that is not in flight is outside the congestion window (`BytesInFlight` never counts it) and arms no
   probe timeout. *Why:* Appendix A's bookkeeping, unchanged, is the simplest way to stay faithful to it.

2. **A probe is one ack-eliciting packet.** When the probe timeout fires, the server sends one packet in the
   space `GetPtoTimeAndSpace` names (Appendix A.8): the space's waiting new data if it has any, else the
   retransmittable frames of its oldest ack-eliciting packet still unacknowledged, else a `PING`. The probe
   ignores the congestion window (`CongestionBudget` is one datagram while a probe is pending, section 7.5)
   but never the anti-amplification limit: no probe timeout is armed while the limit holds (RFC 9000 section
   8.1, RFC 9002 section 6.2.2.1). *Why:* section 6.2.4 SHOULDs new data and allows one or two probe packets;
   one is enough for a server whose client acknowledges promptly, and the backoff covers the rest.
   *Departs from* section 6.2.4's "two packets" suggestion only in sending the one it permits.

3. **What a lost frame becomes** (RFC 9000 section 13.3), in `Retransmittable` and `QuicStreams.Refresh`:
   - `CRYPTO` and `HANDSHAKE_DONE` are sent again as they were.
   - `MAX_DATA`, `MAX_STREAMS` and `MAX_STREAM_DATA` are sent with the current value, not the lost one;
     `MAX_STREAM_DATA` is dropped once the stream has received everything (`IsReceiveDone`).
   - `STREAM` is sent again as it was, except on a stream whose sending part was reset: then only the part
     below the reset's Reliable Size goes again (BL-748 added `RESET_STREAM_AT`, ADR-0117), so after a plain
     `RESET_STREAM`, Reliable Size 0, nothing goes.
   - `DATA_BLOCKED`, `STREAM_DATA_BLOCKED`, `STREAMS_BLOCKED`, `RESET_STREAM`, `RESET_STREAM_AT`,
     `STOP_SENDING`, `NEW_CONNECTION_ID`, `RETIRE_CONNECTION_ID` and every other frame not named here are sent
     again as they were.
   - `ACK`, `PADDING`, `PING`, `PATH_RESPONSE`, `CONNECTION_CLOSE` and `DATAGRAM` (RFC 9221 section 5) are
     never sent again.
   Retransmitted frames go ahead of new ones in their space. *Why:* section 13.3 asks for the current value of
   the flow-control frames and allows the rest to be resent; resending a blocked frame unchanged is the
   simplest, and the client reads blocked frames as information only. *Departs from* section 13.3's
   SHOULD-level "send a new blocked frame only if still blocked" by resending the lost one as it was.

4. **A retransmitted `CRYPTO` or `STREAM` frame too long for the room left is split** when its first part
   keeps at least 64 bytes of data (`MinSplitDataLength`); otherwise it waits for the next datagram. *Why:* a
   sensible default that fills datagrams without sending tiny fragments; RFC 9000 sections 19.6 and 19.8 let
   data be sent again at any offsets.

5. **Persistent congestion is judged on one space's losses at a time.** `IsPersistentCongestion` looks, among
   the packets one loss event declared lost in one space and sent after the first RTT sample, for a run of
   consecutive packet numbers whose ack-eliciting packets span more than
   `(PTO + max_ack_delay) x 3` (`kPersistentCongestionThreshold`). It then collapses the window to the
   2400-byte minimum and ends the recovery period. *Departs from* RFC 9002 section 7.6.2, which judges losses
   across spaces. *Why:* a server's Initial, Handshake and Application Data packets barely overlap in time, so
   one space's losses decide the same thing in practice, and the check stays one loop.

6. **The congestion window grows on every acknowledgment of an in-flight packet sent outside recovery**: by its
   size in slow start, by `1200 x size / window` in congestion avoidance (section 7.3). There is no
   application-limited check. *Departs from* section 7.8's SHOULD NOT increase while application-limited.
   *Why:* simplest first; the HTTP/3 server is usually limited by the window it serves files through, and an
   over-grown window only matters to a path that is congested, which a stand-in on loopback is not.

7. **An ack-eliciting Initial waits for room for 1200 bytes.** A datagram carrying an ack-eliciting Initial
   packet is padded to 1200 bytes (RFC 9000 section 14.1), so `CRYPTO` and other ack-eliciting Initial frames
   are sent only when the congestion window (or a pending probe) and the anti-amplification limit let a
   1200-byte datagram go. *Why:* sending a shorter Initial would break section 14.1; holding it is the only
   choice that keeps both rules.

8. **The client's ACK Delay is scaled, capped and bounded.** An Application Data `ACK`'s ACK Delay is
   multiplied by `2^ack_delay_exponent` (the client's, 3 by default, RFC 9000 sections 18.2 and 19.3) and
   capped at the client's `max_ack_delay` (25 ms by default) before it reaches `QuicRttEstimator` (RFC 9002
   section 5.3, which caps it once the handshake is confirmed; capping on decode also keeps the shift from
   overflowing). Initial and Handshake ACKs count as delay 0 (RFC 9002 section 5.3 lets an endpoint ignore their delay). The probe timeout's
   backoff stops doubling at `2^16` (`MaxBackoffExponent`). *Why:* RFC 9002 sets no ceiling on `pto_count`;
   the idle timeout ends a connection long before 2^16 PTOs, and the ceiling keeps the arithmetic bounded.

9. **`HandleTimeout` acts on one due timer, in a fixed order.** Once `NextDeadline` has passed it ends the
   closing or draining period if one runs (ADR-0082 amendment 2, BL-608, added since); else it discards the
   connection when the idle timeout is due (RFC 9000 section 10.1); else it closes the connection with
   `NO_ERROR` when the handshake timeout is due; else it acts on the loss detection timer (RFC 9002 Appendix
   A.9). *Why:* the idle timeout ends the connection, so nothing below it matters once it is due, and a
   handshake that will be closed needs no probe.

Everything runs on the injected `TimeProvider`; nothing sleeps.

## Consequences

- RFC 9002 sections 5 and 6 are followed as written: `kPacketThreshold` 3, `kTimeThreshold` 9/8,
  `kGranularity` 1 ms, the probe timeout armed for Application Data only once the handshake is confirmed, and a
  discarded space's packets leaving flight without being counted lost (section 6.4).
- A 1 MiB stream each way completes over `UnitTestLossyChannel` dropping every 20th datagram each way
  (`QuicStreamTests.LossyExchange.cs`).
- Two departures (decisions 5 and 6) are SHOULD-level and could be revisited if a stand-in scenario ever runs
  over a congested path; neither changes a byte upstream curl reads.
- Pacing (section 7.7) stays out, as ADR-0082 decision 4 says.
