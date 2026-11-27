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
completed:
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

- [ ] `CLAUDE.md` and the XML doc comments of `ImapProtocolServer` name `Surl.Composition` as what wraps the server for `imaps` and passes its constructor arguments.
- [ ] No line in the library says `Surl.Console` constructs, wraps or passes arguments to a type that `Surl.Composition` does.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

## Log

- 2026-11-27: Created.
