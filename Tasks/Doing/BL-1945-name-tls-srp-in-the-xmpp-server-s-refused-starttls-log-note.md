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
completed:
---
# BL-1945 — Name TLS-SRP in the XMPP server's refused starttls log note

## Goal

The XMPP server's log note for a `<starttls/>` it refuses for want of a TLS upgrade names both a certificate and TLS-SRP.

## Context

Found while working BL-1931 (2026-11-27), read from source. `Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary/XmppServerConnection.cs` (about line 391) notes "no certificate is configured", but the condition is `XmppServerOptions.IsTlsUpgradeAvailable`: a certificate or TLS-SRP (ADR-0122 decision 5). `XmppServerStartTlsTests.cs` (about line 116) pins the string. Only the log note changes; no bytes sent to the client. Search the XMPP `CLAUDE.md` and any ADR quoting the note.

## Acceptance criteria

- [ ] The note says neither a certificate nor TLS-SRP is available, and the test in `XmppServerStartTlsTests` pins the new text.
- [ ] The bytes the server sends for the refused `<starttls/>` are unchanged (the existing assertions on them still pass).
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
