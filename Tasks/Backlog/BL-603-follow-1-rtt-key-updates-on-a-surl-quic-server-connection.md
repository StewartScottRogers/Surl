---
id: BL-603
title: Follow 1-RTT key updates on a Surl.Quic server connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-377]
touches: [Surl.Quic.UnitLibrary, Surl.Quic.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-603 — Follow 1-RTT key updates on a Surl.Quic server connection

## Goal

A `QuicServerConnection` follows a client's key update (RFC 9001 section 6): a 1-RTT packet with the
other key phase opens with the next keys, and the server's later packets use them.

## Context

- ADR-0082 decision 4: "Received key updates are followed; the server never starts one."
- BL-377 built the connection and left this out: today a 1-RTT packet with the key phase bit set does
  not open, so it is dropped. `QuicPacketProtection` derives keys from a traffic secret; the next
  secret is HKDF-Expand-Label(secret, "quic ku", "", hash length), and the header protection key
  does not change (RFC 9001 section 6.1).
- Start at `Protocols/Http/Surl.Quic.UnitLibrary/QuicServerConnection.Receiving.cs`.

## Acceptance criteria

- [ ] A named test: after the handshake with BL-377's recorded-Initial test client, the client sends a
      1-RTT packet in key phase 1 with keys from "quic ku"; the server hands its frames to the caller
      and its reply opens with the updated keys, key phase bit 1.
- [ ] A named test: a key update before the server's own packet in the current phase was acknowledged
      ends with `CONNECTION_CLOSE` `KEY_UPDATE_ERROR` (RFC 9001 section 6.2), or the choice taken is
      recorded under Notes with the RFC's reason.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
