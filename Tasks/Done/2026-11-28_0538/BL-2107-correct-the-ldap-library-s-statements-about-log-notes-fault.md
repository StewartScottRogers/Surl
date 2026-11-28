---
id: BL-2107
title: Correct the LDAP library's statements about log notes, fault bind checks and the announced security-layer length
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2107 — Correct the LDAP library's statements about log notes, fault bind checks and the announced security-layer length

## Goal

The LDAP library's `CLAUDE.md` and XML doc comments say truly which decisions are noted, which requests are checked for a bind before a fault rule and what `AnnouncedBytes` counts.

## Context

Found while working BL-2105 (2026-11-28). All files are in `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary`.

- `CLAUDE.md` says "It notes each decision (`LdapLogText`)": `LdapSession` writes no note itself for a critical-control refusal, an unsupported extended operation, abandon or unbind, and `AnswerCompareAsync` writes none for a compare no fault rule answers.
- `LdapSession.Faults.cs`, class summary: it says each search, compare and write is asked about "once the bind check has let it through". In `LdapSession.cs`, writes and the root DSE's search have no bind check.
- `LdapFrameReadResult.cs`, `AnnouncedBytes`: it says the count includes the tag and length. For a security-layer buffer, `LdapMessageFrameReader.ReadSecurityLayerBufferAsync` passes the protected length alone, without its 4 length bytes.

## Acceptance criteria

- [x] The `CLAUDE.md` sentence about log notes does not claim a note for a decision the code writes none for.
- [x] The class summary in `LdapSession.Faults.cs` says writes and the root DSE's search need no bind check.
- [x] The `AnnouncedBytes` and `TooLarge` doc comments in `LdapFrameReadResult.cs` say what is counted for a security-layer buffer.

## Notes

- `CLAUDE.md` now lists what is noted and what is not, in place of "It notes each decision".
- Two more statements of the same kind were corrected, inside the same library: the
  `LdapBindJudge` summary said it "notes the decision" (a simple bind accepted unchecked gets no
  note), and `LdapFrameReadOutcome.MessageTooLarge` named only a message past the message limit
  (a security-layer buffer past the layer's maximum, or the limit, ends there too).
- Documentation comments and `CLAUDE.md` only; no behaviour change. Build had no warnings, the
  fast tests passed in all 83 test projects, and `dotnet format --verify-no-changes` on the
  library exited 0.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The LDAP library's CLAUDE.md and doc comments now state which decisions are noted, which requests need a bind before a fault rule and what AnnouncedBytes counts.
