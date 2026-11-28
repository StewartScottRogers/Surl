---
id: BL-2152
title: Take --max-message 0 as no limit in the XMPP server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests]
requirement: NFR-015
created: 2026-11-28
completed:
---
# BL-2152 — Take --max-message 0 as no limit in the XMPP server

## Goal

With `--max-message 0` the `xmpp` and `xmpps` server holds no size bound on the stream header, a stanza, an attribute value or a text node (ADR-0006 section 1). This task changes nothing in `Documentation/Product/Requirements.md`; BL-2157 re-states NFR-014 to NFR-016.

## Context

Found while working BL-2120 (2026-11-28), in `Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary`: `XmppServerConnection` builds `XmppStreamReader` with `context.Limits.MaxMessageBytes`; `XmppInputStream` throws `policy-violation` when `elementBytes > maxElementBytes`, and `XmppStreamReader` refuses a value whose UTF-8 length is `> maxValueBytes`, so 0 refuses everything. ADR-0114's limits table names both bounds as `--max-message`. Apply the same guard to any other use of the limit in the project. Stand-in: a test that turns the limit off sends a large stanza through `surl`; nothing new to seed, reset or publish on the event stream.

## Acceptance criteria

- [ ] A test in `XmppStreamReaderTests` reads a stream header and a stanza longer than 1048576 bytes with a bound of 0.
- [ ] A test in `XmppStreamReaderTests` reads an attribute value and a text node longer than 1048576 bytes with a bound of 0.
- [ ] A test in `XmppInputStreamTests` shows a bound of 0 raises no `policy-violation`.
- [ ] The existing tests for a non-zero bound still pass, `dotnet build Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests -warnaserror` is clean, and the fast tests pass with no `TestCategory=Integration` needed.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Renumbered from BL-2130 to BL-2152; an archived task holds BL-2130.
