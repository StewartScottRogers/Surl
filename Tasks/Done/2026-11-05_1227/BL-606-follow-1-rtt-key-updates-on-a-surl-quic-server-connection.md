---
id: BL-606
title: Follow 1-RTT key updates on a Surl.Quic server connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-377]
touches: [Surl.Quic.UnitLibrary, Surl.Quic.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-606 — Follow 1-RTT key updates on a Surl.Quic server connection

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

- [x] A named test: after the handshake with BL-377's recorded-Initial test client, the client sends a
      1-RTT packet in key phase 1 with keys from "quic ku"; the server hands its frames to the caller
      and its reply opens with the updated keys, key phase bit 1.
- [x] A named test: a key update before the server's own packet in the current phase was acknowledged
      ends with `CONNECTION_CLOSE` `KEY_UPDATE_ERROR` (RFC 9001 section 6.2), or the choice taken is
      recorded under Notes with the RFC's reason.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

- Delivered directly in the session (the change sits in one library and its tests; no plan stage needed beyond RFC 9001 section 6).
- New internal `QuicKeyPhase` (`Surl.Quic.UnitLibrary`) holds the key phase bit, the client's next receive keys made in advance (section 6.3: no timing signal on an update), and its previous receive keys. `QuicPacketProtection` gained `NextTrafficSecret` ("quic ku") and `ForNextKeyPhase` (new key and IV, same header-protection key, section 6.1), and an internal `Unprotect` overload that removes header protection with the current header-protection key, then opens the payload with the keys the key phase bit and the decoded packet number pick.
- Choice, KEY_UPDATE_ERROR: the server closes `KEY_UPDATE_ERROR` on any key update that arrives before it has sent, in the current phase, a 1-RTT packet carrying an `ACK` - the first update too. Why: RFC 9001 section 6.1 forbids a client to start an update until a packet it sent in the current phase is acknowledged, and section 6.2 lets the receiver treat an update that does not wait for that as `KEY_UPDATE_ERROR`; since every ACK the server sends in a phase covers a packet the client sent in it (Application Data ACKs only follow a 1-RTT packet received, and the update packet itself opens the new phase), "an ACK sent in this phase" is exactly the condition the client must have seen. The close goes out in the phase the server is still in.
- Choice, old keys: the previous receive keys are kept until the next update rather than discarded after three PTOs (section 6.5's SHOULD). Why: it keeps the connection free of another timer, a packet older than the current phase still needs its packet number below the phases first one to be tried with them, and they are replaced, never accumulated. A packet in the other phase that fails to open is dropped and changes nothing (section 6.3 allows no other reaction).
- The test client derives the next secrets with the BCL's `HKDF.Expand` from RFC 8446's label layout, not Surl's code; `NextTrafficSecret` is pinned to RFC 9001 Appendix A.5's `ku` value.
- Tests: `QuicServerConnectionTests.ReceiveDatagram_KeyUpdateAfterTheServerAcknowledgedThePhase_HandsOverItsFramesAndRepliesWithTheUpdatedKeys` (criterion 1), `ReceiveDatagram_KeyUpdateBeforeTheServerAcknowledgedAPacketInThePhase_EndsWithConnectionCloseKeyUpdateError` and `ReceiveDatagram_SecondKeyUpdateBeforeTheFirstPhaseWasAcknowledged_EndsWithConnectionCloseKeyUpdateError` (criterion 2), plus a second update back to phase 0, a late old-phase packet, and a forged other-phase packet. Surl.Quic.UnitTests 458 green; Surl.Quic.UnitLibrary 100% line and branch coverage; `dotnet build -warnaserror` clean; all fast tests green.
- No ADR: ADR-0082 decision 4 already decides that received key updates are followed and the server never starts one; the two choices above are recorded here, as the acceptance criterion asks, and in the library's CLAUDE.md.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Quic server connections follow the client's 1-RTT key updates and close KEY_UPDATE_ERROR on an update that does not wait for an ACK
