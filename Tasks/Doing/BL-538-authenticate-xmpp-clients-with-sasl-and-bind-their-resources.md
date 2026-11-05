---
id: BL-538
title: Authenticate XMPP clients with SASL and bind their resources
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-537, BL-416]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-538 — Authenticate XMPP clients with SASL and bind their resources

## Goal

The XMPP server offers the SASL mechanisms BL-535's ADR lists and runs each exchange with `<auth/>`,
`<challenge/>`, `<response/>`, `<success/>` and `<failure/>`. It restarts the stream after success, then binds a
resource and answers with the full JID.

## Context

- Decision: BL-535's ADR, decisions 3 and 4.
- RFC 6120 section 6.4 (the SASL flow, base64, `=` for an empty response, and the `<failure/>` conditions
  `not-authorized`, `invalid-mechanism`, `malformed-request`, `aborted` and `encryption-required`), and section 7
  (binding: the `urn:ietf:params:xml:ns:xmpp-bind` IQ and its errors `bad-request`, `conflict` and `not-allowed`).
- SASL exchanges come from `Surl.Authentication` (`PlainSaslExchange`, and SCRAM from BL-416), reached through
  the SASL contracts in `Surl.Protocol.Abstractions`.

## Acceptance criteria

- [ ] Named tests show:
      - SCRAM-SHA-256 and SCRAM-SHA-1 completed with the RFC 7677 and RFC 5802 example values and an account
        `user`/`pencil`;
      - PLAIN accepted over TLS;
      - a bad proof gets `not-authorized`;
      - an unoffered mechanism gets `invalid-mechanism`;
      - `<abort/>` gets `aborted`;
      - the attempt limit ending the stream;
      - bind with a resource answered with `user@domain/resource`;
      - bind without one answered with a generated resource;
      - a stanza before bind refused as section 7 says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
