---
id: BL-1943
title: Name Surl.Composition, not Surl.Console, as the composer in the IMAP server's comments and notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1928]
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1943 — Name Surl.Composition, not Surl.Console, as the composer in the IMAP server's comments and notes

## Goal

The IMAP library's comments and `CLAUDE.md` name the project that really composes the server.

## Context

Found while working BL-1928 (2026-11-27). Comment and project-notes text only; no behaviour change.
`ImplicitTlsSchemeServer` and the `ImapProtocolServer` construction are in `Application/Surl.Composition.UnitLibrary`
(`ImplicitTlsSchemeServer.cs`, `ProtocolServerRegistrations.cs`), but the library still credits `Surl.Console`:
`CLAUDE.md` about lines 17 and 25, and `ImapProtocolServer.cs` about lines 41 and 80. The library does not reference
`Surl.Composition`, so name types in `<c>`, not `<see cref>`.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [x] `CLAUDE.md` and the XML doc comments of `ImapProtocolServer` name `Surl.Composition` as what wraps the server for `imaps` and passes its constructor arguments.
- [x] No line in the library says `Surl.Console` constructs, wraps or passes arguments to a type that `Surl.Composition` does.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

- Checked against `ProtocolServerRegistrations.Original`: `imap` is built with `AuthenticationPolicyFor("imap")` as both
  policy arguments, and `imaps` is `ProtocolServerRegistration.ImplicitTls("imaps", "imap")`. Four sentences changed, two
  in `CLAUDE.md` and two in `ImapProtocolServer.cs`; no `Surl.Console` mention is left in the library.
- Edited in the session rather than through `align-and-document`: four sentences, already read against the code.
- BL-1947 (Backlog) covers the same four sentences and also the four ADR links in this `CLAUDE.md`, which start
  `../Documentation/` and resolve to nothing. The links are outside this task's criteria and were left for BL-1947, whose
  other criteria the new wording already meets.
- `verify`: build 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests 30366 passed and 3 skipped in 83
  assemblies. Integration tests not run: comments and notes only.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The IMAP library's notes and doc comments name Surl.Composition as what wraps the server for imaps and passes its constructor arguments
