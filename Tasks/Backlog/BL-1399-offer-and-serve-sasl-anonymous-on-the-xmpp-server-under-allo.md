---
id: BL-1399
title: Offer and serve SASL ANONYMOUS on the XMPP server under --allow-anonymous
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests, Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-1399 — Offer and serve SASL ANONYMOUS on the XMPP server under --allow-anonymous

## Goal

Under `--allow-anonymous`, the XMPP server's `<mechanisms/>` ends with `ANONYMOUS` (RFC 4505), and an
`<auth mechanism='ANONYMOUS'/>` logs the stream in as `anon-<16 hex digits>@<stream domain>`, as ADR-0114
decision 3 says.

## Context

- Surly cookbook bug report 2026-11-19 09:30, finding 2 (first half): `surl --allow-anonymous xmpp://127.0.0.1:5222/`
  then an XMPP client that asks for ANONYMOUS gets no such mechanism; the stream features list only `PLAIN`, before
  and after STARTTLS. (The client was the Curl port: it located the defect, it is not the oracle; ADR-0114 and
  RFC 4505 are.)
- ADR-0114 decision 3 says: "Mechanisms offered, in this order: `SCRAM-SHA-256`, `SCRAM-SHA-1`, `PLAIN`, then
  `ANONYMOUS` (RFC 4505) only under `--allow-anonymous`", and "for `ANONYMOUS`, `anon-` and 16 random lower-case
  hex digits `@<stream domain>`, new per login". The report's claim is right.
- Cause: `Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary/XmppServerConnection.cs:259-260` offers
  `OfferableMechanisms` (line 20, which lists `ANONYMOUS`) filtered by
  `options.AuthenticationPolicy.GetSaslMechanisms(...)`, and the policy
  (`Foundation/Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs:359`, `SaslMechanism.InOfferOrder`) has no
  `ANONYMOUS` mechanism at all, so the filter always drops it. `XmppSaslLogin.StartAsync` would then hand
  `ANONYMOUS` to `policy.StartSaslExchange`, which refuses an unknown mechanism.
- Precedent to copy: the AMQP 0-9-1 broker offers and serves `ANONYMOUS` itself.
  `Amqp0BrokerOptions.OffersAnonymous` (`Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary/Amqp0BrokerOptions.cs:22`),
  appended in `Amqp0BrokerConnection.Login.cs:46-48` and accepted at line 83, wired in
  `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs:112`
  (`OffersAnonymous = services.AuthenticationPolicy.Settings.AllowAnonymous`).
- The work: add `XmppServerOptions.OffersAnonymous` (default `false`); append `ANONYMOUS` last to the offered list
  when it is set, in every phase that offers mechanisms (decision 2's table); answer `<auth mechanism='ANONYMOUS'>`
  with an empty initial response (`=`), no text, or a trace token (RFC 4505 section 2: up to 255 UTF-8 characters,
  not checked) with `<success/>` (empty) and `BareJid` from `XmppJids.BareJid(null, ...)`; while TLS is required
  (`IsTlsRequired`, line 82) the `encryption-required` refusal still comes first. Wire the option with one line
  in `ExtendedProtocolServers.cs:186-193`, as for AMQP.
- Stand-in: the anonymous name comes from the instance's seedable `IdentifierXmppRandomSource` (BL-1178), so a
  test gets the same JID every run; the login is noted in the exchange log (`Logged in as anon-...`); no store,
  event or fault change.

## Acceptance criteria

- [ ] A failing test first: an `XmppServerConnection` (or `XmppServer`) test, with `OffersAnonymous = true` and no
      certificate, sees `<mechanisms>` end with `<mechanism>ANONYMOUS</mechanism>`; with `OffersAnonymous = false`
      it is absent.
- [ ] A test replaying `<auth xmlns='urn:ietf:params:xml:ns:xmpp-sasl' mechanism='ANONYMOUS'>=</auth>` (written
      from RFC 6120 section 6.4.2 and RFC 4505) gets `<success xmlns='urn:ietf:params:xml:ns:xmpp-sasl'/>`, and the
      bind that follows answers a JID `anon-<16 lower-case hex digits>@<domain>` drawn from the injected random
      source.
- [ ] A test shows `ANONYMOUS` without `OffersAnonymous` is `invalid-mechanism`, and `ANONYMOUS` before TLS while TLS
      is required is `encryption-required`.
- [ ] `ExtendedProtocolServers` sets `XmppServerOptions.OffersAnonymous` from
      `AuthenticationPolicy.Settings.AllowAnonymous` (one line).
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Extended.Protocol.Xmpp.UnitLibrary` stays at 100% line and branch coverage.

## Notes

- The second half of finding 2 (a `-u alice` login bound as `anon-...` under `--allow-anonymous`) is BL-1400.
- Update the library's `CLAUDE.md` SASL bullet to say who offers `ANONYMOUS`.

## Log

- 2026-11-19: Created.
