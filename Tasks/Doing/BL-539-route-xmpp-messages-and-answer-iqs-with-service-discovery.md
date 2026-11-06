---
id: BL-539
title: Route XMPP messages and answer IQs with service discovery
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-538]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-539 — Route XMPP messages and answer IQs with service discovery

## Goal

The XMPP server routes `<message/>` stanzas between its connected users as BL-535's ADR decides. It answers
disco#info and disco#items (XEP-0030) for itself and for bare JIDs, and answers ping if served. It answers every
other IQ get or set with `service-unavailable`, and each malformed stanza with the stanza error RFC 6120 names.

## Context

- Decision: BL-535's ADR, decisions 5 and 6.
- RFC 6120 section 8 (stanza attributes; section 8.2.3 IQ semantics: exactly one child, and a `result` or `error`
  reply; section 8.3 stanza errors: `service-unavailable`, `bad-request`, `item-not-found`, `feature-not-implemented`)
  and section 10 (server rules for processing stanzas). RFC 6121 section 5 (message types and delivery to a
  full JID or a bare JID). XEP-0030 sections 3 and 4 (the disco#info identity and features, and disco#items),
  with its examples as test bytes.
- Sessions are shared by the server instance for routing, not a static.

## Acceptance criteria

- [ ] Named tests show:
      - a `chat` message from one session delivered to another user's session;
      - a message to a user with no session handled as the ADR says;
      - disco#info to the server answered with the ADR's identity and features, in XEP-0030 example 2's shape;
      - disco#items answered;
      - an unknown IQ namespace gets `service-unavailable`;
      - an IQ with two children gets `bad-request`;
      - an IQ `result` from the client never answered;
      - a `from` that is not the session's JID handled as section 8.1.2.1 says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
