---
id: BL-537
title: Negotiate XMPP STARTTLS and require it before authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-536]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-537 — Negotiate XMPP STARTTLS and require it before authentication

## Goal

On `xmpp` the XMPP server offers `<starttls><required/></starttls>`. It answers `<starttls/>` with `<proceed/>`
and upgrades the connection, then restarts the stream. Until TLS is negotiated it refuses anything else as RFC 6120
section 5 says, unless BL-535's ADR's explicit option allows plain text.

## Context

- Decision: BL-535's ADR, decision 2.
- RFC 6120 section 5.3 and 5.4 (the STARTTLS flow, `<proceed/>` and `<failure/>`, the stream restart without
  closing, and features not re-offering STARTTLS), and section 9.1 (the client-to-server examples of TLS).
- The TLS upgrade comes from the listener seam, as the mail servers' STARTTLS does (ADR-0010; `IsTlsUpgradeAvailable`,
  ADR-0069). The server never constructs an `SslStream`.

## Acceptance criteria

- [x] Named tests, with the in-memory TLS upgrade the mail servers' tests use, show:
      - features offering required STARTTLS;
      - `<starttls/>` answered `<proceed/>` and then the upgrade;
      - the restarted stream's features offering SASL and not STARTTLS;
      - a SASL `<auth/>` before TLS refused as section 5 says;
      - `<starttls/>` without a configured certificate answered `<failure/>` and closed;
      - plain text allowed only under the ADR's option.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Delivered in `XmppServerConnection` (a loop over streams: `<starttls/>`, `<proceed/>`, the upgrade, a new
  `XmppStreamReader`, the restarted header) and `XmppStreamText`; tests in `XmppServerStartTlsTests` (16) beside the
  updated `XmppServerTests`. 162 XMPP tests pass; the library keeps 100% line and branch coverage, highest cyclomatic
  complexity 10 (`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Xmpp.UnitLibrary`).
- Choices within ADR-0114 decision 2 (no new ADR: none departs from it):
  - `XmppServerOptions` gains `IsTlsUpgradeAvailable` (the mail servers' flag name, ADR-0069),
    `AllowPlaintextAuthentication` (`--allow-plaintext-auth`) and `AuthenticationPolicy` (`ISaslAuthenticationPolicy`,
    `AnonymousAuthenticationPolicy` by default, as the other servers' policy-less constructors). BL-540 sets them.
  - Features advertise the policy's mechanisms for the TLS state, ordered and filtered to decision 3's four, so the
    restarted stream offers SASL now; BL-538 runs the exchange. Until then an `<auth/>` that TLS does not refuse
    stays `policy-violation`.
  - The head timeout runs from `<proceed/>` to the new header with the handshake inside it (decision 7). A
    handshake that fails or is cut off leaves no TLS to write a stream error in, so it closes with no bytes and no
    half-close (5.4.3.2).
  - Bytes pipelined after `<starttls/>` are dropped by starting a new XML reader, as the mail servers discard theirs
    (ADR-0010).
  - `xmpps` is not added to `Schemes`: BL-540 wraps the server in `ImplicitTlsSchemeServer`. A connection that is TLS
    from its first byte already starts at the post-TLS features and refuses `<starttls/>` (tested).
- `Documentation/Conformance/Extended/Xmpp.md` was not edited: `Documentation/Conformance` is in BL-396's `touches`
  (Doing). Filed BL-862 to trace the rows to the tests named here.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. xmpp requires STARTTLS by default: <starttls/> gets <proceed/>, the upgrade and a restarted stream offering SASL; <auth/> before TLS is encryption-required; --allow-plaintext-auth makes it voluntary
