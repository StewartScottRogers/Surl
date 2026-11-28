---
id: BL-2106
title: Correct the LDAP library's statements about log notes, fault bind checks and the announced security-layer length
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary]
requirement: none
created: 2026-11-28
completed:
---
# BL-2106 — Correct the LDAP library's statements about log notes, fault bind checks and the announced security-layer length

## Goal

The LDAP library's `CLAUDE.md` and XML doc comments say truly which decisions are noted, which requests are checked for a bind before a fault rule and what `AnnouncedBytes` counts.

## Context

Found while working BL-2105 (2026-11-28). All files are in `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary`.

- `CLAUDE.md` says "It notes each decision (`LdapLogText`)": `LdapSession` writes no note itself for a critical-control refusal, an unsupported extended operation, abandon or unbind, and `AnswerCompareAsync` writes none for a compare no fault rule answers.
- `LdapSession.Faults.cs`, class summary: it says each search, compare and write is asked about "once the bind check has let it through". In `LdapSession.cs`, writes and the root DSE's search have no bind check.
- `LdapFrameReadResult.cs`, `AnnouncedBytes`: it says the count includes the tag and length. For a security-layer buffer, `LdapMessageFrameReader.ReadSecurityLayerBufferAsync` passes the protected length alone, without its 4 length bytes.

## Acceptance criteria

- [ ] The `CLAUDE.md` sentence about log notes does not claim a note for a decision the code writes none for.
- [ ] The class summary in `LdapSession.Faults.cs` says writes and the root DSE's search need no bind check.
- [ ] The `AnnouncedBytes` and `TooLarge` doc comments in `LdapFrameReadResult.cs` say what is counted for a security-layer buffer.

## Notes

## Log

- 2026-11-28: Created.
