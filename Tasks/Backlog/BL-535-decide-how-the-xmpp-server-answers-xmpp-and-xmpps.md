---
id: BL-535
title: Decide how the XMPP server answers xmpp and xmpps
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-535 — Decide how the XMPP server answers xmpp and xmpps

## Goal

An ADR decides how `surl xmpp://` and `xmpps://` answer as an XMPP server. It covers the domain served, the stream
features, mandatory STARTTLS, the SASL mechanisms, resource binding, stanza routing, the IQ namespaces answered
(service discovery included), stream and stanza errors, the XML restrictions and limits. A server-side
requirement matrix traces each decision.

## Context

- Oracle and rules: BL-543's ADR. Specifications:
  - RFC 6120: section 4 (streams), 5 (STARTTLS), 6 (SASL), 7 (resource binding), 8 (stanzas), 9 (examples) and
    11 (XML usage: no DTD, no comments, no processing instructions, no internal entities);
  - RFC 6121 section 5 (message exchange) and section 4 if presence is served;
  - XEP-0030 (service discovery), XEP-0368 (direct TLS for `xmpps`), and XEP-0199 (ping) if served;
  - RFC 4616 (PLAIN), RFC 4505 (ANONYMOUS), RFC 5802 (SCRAM-SHA-1) and RFC 7677 (SCRAM-SHA-256).
- Which stanzas a client sends (what to accept, not a verdict): the port's `Curl.Extended.Protocol.Xmpp.UnitLibrary`
  (the port's ADR-1068) and its matrix `Xmpp.md`. The port's `XmppReferenceServer` is copyable code.
- SCRAM: BL-416 adds it to `Surl.Authentication`. TLS upgrade: the listener seam, as the mail servers' STARTTLS
  is (ADR-0010, ADR-0069).
- One account set (Stewart, 2026-11-04; BL-622's ADR decision 4): SASL logins read the accounts of `--user`,
  `--user-file` and the layer-2 options (a `--mailbox` account restricted with `protocols=` included, BL-647)
  through `Surl.Authentication`'s SASL contracts, never a private roster of credentials. If decision 1 ties the
  XMPP domain to an option, consider BL-642's `--mail-domain` so one domain serves mail and chat.

Decide and record:

1. The domain: taken from the listen URL's host or an option, and `host-unknown` for another `to`.
2. Stream features in each phase; STARTTLS `<required/>` by default on `xmpp`, with plain text only under an
   explicit option. Direct TLS on `xmpps`.
3. SASL mechanisms offered: SCRAM-SHA-256, SCRAM-SHA-1 and PLAIN (PLAIN only over TLS), and ANONYMOUS only under
   `--allow-anonymous`. Also the `<failure/>` conditions.
4. Resource binding: the client's resource or a generated one, and conflicts.
5. Stanza handling: messages routed between connected sessions of the server's own users (offline storage or
   `service-unavailable`), the IQ namespaces answered (disco#info and disco#items for the server and for bare
   JIDs; ping), `service-unavailable` for others, and presence if served.
6. The stream errors and stanza errors sent for each failure.
7. Each ADR-0006 limit (stanza size, stream open time, SASL attempts).

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing the RFC or XEP section, and is
      listed in the folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/Xmpp.md` exists in the matrix format, each "covered by" naming
      BL-536, BL-537, BL-538 or BL-539, or "n/a: <reason>".

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: logins read the one account set.
