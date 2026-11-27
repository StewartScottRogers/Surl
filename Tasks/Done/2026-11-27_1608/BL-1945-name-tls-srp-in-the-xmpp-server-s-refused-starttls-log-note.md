---
id: BL-1945
title: Name TLS-SRP in the XMPP server's refused starttls log note
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1945 — Name TLS-SRP in the XMPP server's refused starttls log note

## Goal

The XMPP server's log note for a `<starttls/>` it refuses for want of a TLS upgrade names both a certificate and TLS-SRP.

## Context

Found while working BL-1931 (2026-11-27), read from source. `Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary/XmppServerConnection.cs` (about line 391) notes "no certificate is configured", but the condition is `XmppServerOptions.IsTlsUpgradeAvailable`: a certificate or TLS-SRP (ADR-0122 decision 5). `XmppServerStartTlsTests.cs` (about line 116) pins the string. Only the log note changes; no bytes sent to the client. Search the XMPP `CLAUDE.md` and any ADR quoting the note.

## Acceptance criteria

- [x] The note says neither a certificate nor TLS-SRP is available, and the test in `XmppServerStartTlsTests` pins the new text.
- [x] The bytes the server sends for the refused `<starttls/>` are unchanged (the existing assertions on them still pass).
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- New note text: "A <starttls/> arrived and neither a certificate nor TLS-SRP is available; answered the TLS failure and closed." The updated pin failed against the old text before the production string was changed.
- The XMPP `CLAUDE.md` already names both, and nothing under `Documentation/` quotes the note, so no document changed. No ADR: only a log note's wording changed.
- Verify: build 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests 30368 passed, 0 failed, 3 skipped. Integration tests not run: no network, file system or database code changed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The XMPP server's refused starttls log note names both a certificate and TLS-SRP.
