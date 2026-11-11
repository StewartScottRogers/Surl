---
id: BL-1049
title: Prove the two XMPP matrix rows no test proves: a stanza with no to, a message of unknown type
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-1049 â€” Prove the two XMPP matrix rows no test proves: a stanza with no to, a message of unknown type

## Goal

Two rows of `Xmpp.md` that name this task each have a named unit test: `Xmpp:RFC6120-10.3.3-1` (a stanza with no `to` is handled as addressed to the sender's bare JID) and `Xmpp:RFC6121-5.2.2-1` (a message of unknown `type` is treated as `normal`).

## Context

Found by BL-541: `XmppStanzaRouterTests` has no test for either row. Add them beside the existing router tests (ADR-0114) and replace this ID in the matrix with the test names.

## Acceptance criteria

- [x] A router test for a stanza with no `to` and one for a message of unknown `type` pass.
- [x] Both rows in `Documentation/Conformance/Extended/Xmpp.md` name their test instead of BL-1049.

## Notes

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Router tests prove no-to and unknown-type XMPP matrix rows
