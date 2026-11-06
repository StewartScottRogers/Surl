---
id: BL-539
title: Route XMPP messages and answer IQs with service discovery
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-538]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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

- [x] Named tests show:
      - a `chat` message from one session delivered to another user's session;
      - a message to a user with no session handled as the ADR says;
      - disco#info to the server answered with the ADR's identity and features, in XEP-0030 example 2's shape;
      - disco#items answered;
      - an unknown IQ namespace gets `service-unavailable`;
      - an IQ with two children gets `bad-request`;
      - an IQ `result` from the client never answered;
      - a `from` that is not the session's JID handled as section 8.1.2.1 says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Built: `XmppSessionRegistry` (one per `XmppServer` instance) of `XmppBoundSession`s, `XmppJid` (RFC 7622 parse),
  `XmppStanzaRouter` (decision 5's message, presence and IQ rules) and `XmppStanzaText` (results in XEP-0030 example
  2's shape, stanza errors, routed stanzas). `XmppResourceBinding` now answers only until bind; the second bind and
  legacy session moved to the router. `XmppServerConnection` chains its writes so routed stanzas never interleave
  with its own answers.
- Tests (`XmppStanzaRouterTests`), per criterion: `AnswerAsync_ChatToAnotherUsersFullJid_IsDeliveredToThatStreamWithFromStamped`;
  `AnswerAsync_ChatToAUserWithNoStream_IsServiceUnavailableWhetherOrNotTheAccountExists`;
  `AnswerAsync_DiscoInfoToTheServer_IsAnsweredInXep0030Example2sShape`; `AnswerAsync_DiscoItemsToTheServer_IsAnEmptyQuery`
  and `..._DiscoItemsToABareJidWithABoundStream_IsAnEmptyQuery`; `AnswerAsync_IqInAnUnknownNamespace_IsServiceUnavailableWithTheChildKept`;
  `AnswerAsync_IqGetWithoutExactlyOneChild_IsBadRequest`; `AnswerAsync_IqResultOrErrorFromTheClient_IsNeverAnswered`;
  `AnswerAsync_FromThatIsNotTheStreamsJid_EndsTheStreamInvalidFrom`. 304 tests in the project; library 100% line and branch.
- Choices: results and the session answer now carry `to` = the full JID (RFC 6120 8.1.1.1 allows it; XEP-0030
  example 2 has it), so BL-538's session and second-bind expectations were updated. Stanza errors and routed stanzas
  are serialized by `XElement` (double quotes, `xmlns='jabber:client'` on the stanza); handwritten answers use single
  quotes, as the rest of the server does. A message or presence error drops the original children; an IQ's keeps them
  (decision 5). JID localparts compare without case (RFC 7622 3.3); resources compare exactly.
- A routed write awaits the recipient's transport. The 16 MiB unsent-output limit (decision 7, row
  `Xmpp:Surl-Limits-4`) was not in this task's criteria; filed as BL-886 and the conformance row now names it. Added
  `Documentation/Conformance/Extended/Xmpp.md` to `touches` for that one row; no task in Doing touches it.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. XMPP routes messages between bound streams and answers IQs with disco#info, disco#items, ping and stanza errors
