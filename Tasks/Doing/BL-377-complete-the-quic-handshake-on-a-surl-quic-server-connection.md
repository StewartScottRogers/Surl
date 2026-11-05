---
id: BL-377
title: Complete the QUIC handshake on a Surl.Quic server connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-374, BL-376]
touches: [Surl.Quic.UnitLibrary, Surl.Quic.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-377 — Complete the QUIC handshake on a Surl.Quic server connection

## Goal

A `Surl.Quic` server connection completes the QUIC handshake with a client over a datagram exchange.
It processes Initial and Handshake packets, carries BL-376's TLS 1.3 handshake in `CRYPTO` frames,
exchanges transport parameters, sends `HANDSHAKE_DONE`, and then opens and seals 1-RTT packets. It
acknowledges every packet number space, and ends with `CONNECTION_CLOSE` on error, idle timeout or
shutdown.

## Context

- Decision: BL-372's ADR, decisions 4 (connection IDs, Retry, version negotiation, transport
  parameters) and 7 (limits and timeouts).
- RFC 9000 sections 5 (connection IDs), 7 (handshake), 8.1 (address validation and the anti-amplification
  limit of three times the bytes received), 10 (closing, idle timeout), 13.2 (generating ACKs), 14
  (datagram size, the 1,200-byte minimum for client Initials), and 18 (transport parameters). RFC
  9001 section 4 (encryption levels, key discard).
- Builds on BL-373's codec, BL-374's protection and BL-376's handshake. Time comes from an injected
  `TimeProvider`. The connection talks to a datagram abstraction that tests fake, and never to a
  socket (the seam is BL-380).
- Fixtures: BL-363's recorded client Initial datagrams from each pinned build.

## Acceptance criteria

- [ ] With BL-363's recorded client Initial from each pinned build as input, the connection answers
      with Initial and Handshake packets that a test-side QUIC client in the test project decrypts,
      and completes the handshake with that client.
- [ ] Named tests cover: a client Initial smaller than 1,200 bytes dropped; a server that sends no
      more than three times the bytes it received before address validation; an unsupported version
      answered with Version Negotiation; the idle timeout closing silently, measured with a fake
      `TimeProvider`; a protocol violation ending with `CONNECTION_CLOSE` and the RFC 9000 error code;
      and Initial and Handshake keys discarded when RFC 9001 section 4.9 says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
