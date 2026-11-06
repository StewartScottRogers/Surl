---
id: BL-875
title: Take over conflicting XMPP resources, cap resources per JID, refuse foreign authzids and bound the login time
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-538, BL-539]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-875 — Take over conflicting XMPP resources, cap resources per JID, refuse foreign authzids and bound the login time

## Goal

The XMPP server applies the parts of ADR-0114 decisions 3, 4 and 7 that BL-538 left out. A resource already bound
for the same bare JID moves to the new stream, and the older stream gets the stream error `conflict`. An eleventh
resource for one bare JID is `resource-constraint`. An authzid other than empty or the account's own bare JID is
`invalid-authzid`. A stream with no bound resource within twice `--head-timeout` of accept ends with
`policy-violation`.

## Context

- Decision: ADR-0114 decision 3 (authzid), decision 4 (conflict, at most 10 resources per bare JID) and decision 7
  (the login row of the limits table).
- BL-538 built SASL (`XmppSaslLogin`) and binding (`XmppResourceBinding`) for one stream only. A conflict and the
  resource cap need the bound streams that `XmppServer` holds, shared by one server instance and never in a static.
  BL-539 builds that session registry for routing, so this task starts after it.
- authzid: `PlainSaslExchange` in `Surl.Authentication` refuses an authzid other than the authcid as a bad
  credential, and SCRAM refuses an `a=` other than the user with `e=other-error`, so both reach the server as
  `RefusedCredentials` (`not-authorized`). To answer `invalid-authzid` (RFC 6120 6.5.6), the server reads the authzid
  itself: for PLAIN the bytes before the first NUL, for SCRAM the gs2 header's `a=`. It answers `invalid-authzid`
  when the authzid is neither empty, the authcid, nor the bare JID ADR-0114 decision 3 gives the account. The
  exchange is still run, so the refusal costs what a wrong password costs. A SCRAM message cannot be rewritten:
  its `c=` repeats the gs2 header.
- `Documentation/Conformance/Extended/Xmpp.md` rows `Xmpp:RFC6120-7.7.2.2-1`, `Xmpp:RFC6120-7.6.2.1-1`,
  `Xmpp:RFC6120-6.5.6-1` and `Xmpp:Surl-Limits-3` name this task.

## Acceptance criteria

- [ ] Named tests in `Surl.Extended.Protocol.Xmpp.UnitTests` show:
      - a second stream binding `user@domain/balcony` gets the bind result, and the first stream gets
        `<stream:error><conflict xmlns='urn:ietf:params:xml:ns:xmpp-streams'/></stream:error></stream:stream>`
        and is closed;
      - the eleventh bound resource for one bare JID gets an IQ error `resource-constraint` of type `wait`;
      - a PLAIN authzid `other@domain` and a SCRAM `a=other` each get `<failure><invalid-authzid/></failure>`, while
        an authzid equal to the bare JID logs in;
      - a stream that has not bound a resource when twice the head timeout has passed since accept gets
        `policy-violation`, measured with the injected `TimeProvider`.
- [ ] The four rows named above in `Documentation/Conformance/Extended/Xmpp.md` name this task's tests.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Filed by BL-538, whose acceptance criteria did not include these four rules even though the conformance trace
listed them under it.

## Log

- 2026-11-05: Created.
