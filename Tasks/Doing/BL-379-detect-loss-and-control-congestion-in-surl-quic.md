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
completed:
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

- [ ] Named tests drive a channel that drops chosen packets: a dropped 1-RTT packet holding `STREAM`
      data is detected by the packet threshold and its data is retransmitted; a lost tail is detected
      by PTO; a dropped Handshake packet is recovered; and `ACK`-only packets are never retransmitted.
- [ ] RTT tests reproduce RFC 9002 section 5.3's `smoothed_rtt` and `rttvar` updates for a scripted
      series of ACKs.
- [ ] Congestion-window tests show slow start, the reduction on loss, and the collapse to the minimum
      window on persistent congestion, for scripted ACK and loss sequences.
- [ ] The 1 MiB stream test from BL-378 completes over a channel that drops 5% of packets, chosen
      deterministically.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
