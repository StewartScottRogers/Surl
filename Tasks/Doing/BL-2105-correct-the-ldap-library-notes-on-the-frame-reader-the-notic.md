---
id: BL-2105
title: Correct the LDAP library notes on the frame reader, the Notice of Disconnection and fault rules
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary]
requirement: none
created: 2026-11-28
completed:
---
# BL-2105 — Correct the LDAP library notes on the frame reader, the Notice of Disconnection and fault rules

## Goal

The `CLAUDE.md` of `Surl.Protocol.Ldap.UnitLibrary` says truly what reads a frame, when a connection closes with no Notice of Disconnection, when a fault rule is asked and what the recorded tests cover.

## Context

Found while working BL-2100 (2026-11-28). All statements are in `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/CLAUDE.md`.

- The BER codec paragraph ends "All of it uses the BCL's `System.Formats.Asn1`", but `LdapMessageFrameReader.cs` uses none of it: it reads the tag and length octets itself.
- "It sends the Notice of Disconnection for what it cannot read (`LdapDiagnostics`) and for a limit": `LdapSession.AnswerNoFrameAsync` closes with no reply when the first message misses the head timeout, and when a security-layer buffer is too large or fails its check; an unknown operation that is not a write also gets the Notice, which the sentence omits.
- The chosen-error fault paragraph: `LdapSession.AnswerAsync` refuses a request with a critical control before any fault rule, and the root DSE's search is put to the fault plan with no bind check. The class remarks of `LdapProtocolServer` state both.
- "Its tests replay request bytes recorded from the pinned Windows build": `Surl.Protocol.Ldap.UnitTests/Fixtures/README.md` says abandon, unknown operations, malformed messages, compare, writes and some SASL, Sicily and `StartTLS` cases are built by hand.

## Acceptance criteria

- [ ] The BER codec paragraph does not say `LdapMessageFrameReader` uses `System.Formats.Asn1`.
- [ ] The Notice of Disconnection sentence names the cases that close with no reply and the unknown operation.
- [ ] The chosen-error fault paragraph says a critical control is refused first and the root DSE's search needs no bind check.
- [ ] The sentence about the tests says some cases are built by hand.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
