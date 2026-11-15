---
id: BL-1178
title: Draw XMPP stream IDs, generated resources and anonymous names from the instance's seedable identifier source
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-064
created: 2026-11-14
completed:
---
# BL-1178 — Draw XMPP stream IDs, generated resources and anonymous names from the instance's seedable identifier source

## Goal

With `--random-seed` (or `SurlServerOptions.RandomSeed`), the XMPP server's stream IDs, generated resources and anonymous names repeat run after run for the same exchange order; unseeded, they stay cryptographically random as today.

## Context

- ADR-0124 decision 6 names "XMPP stream IDs" and "a generated resource" among the identifiers that may be seeded. The XMPP server draws them from `IXmppRandomSource` (`Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary/IXmppRandomSource.cs`, `NextHexDigits(int)`), default `CryptographicXmppRandomSource.Instance`, set through `XmppServerOptions.RandomSource` (ADR-0114 decision 0). `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` builds `new XmppServer(new XmppServerOptions { ... })` without a `RandomSource`, so a seeded instance never reaches it.
- Add an adapter over `IIdentifierRandom` in the XMPP library (as `IdentifierCoapRandomSource` does for CoAP in `Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary`), and set `RandomSource` from `services.IdentifiersForServer("xmpp")` in the one registration line (the per-exchange `context.Identifiers` is the alternative if the source must be per exchange; follow what ADR-0124 decision 6 says about deriving per exchange). Anonymous names grant nothing beyond what `--allow-anonymous` already grants; SASL nonces stay on their own secret sources.

## Acceptance criteria

- [ ] An adapter type in the XMPP library turns an `IIdentifierRandom` into `IXmppRandomSource`, producing lower-case hex of the asked length; named tests pin its output for a fixed `SeededIdentifierRandom`.
- [ ] The composition hands the XMPP server the instance's identifier source; a named composition test shows two instances with the same `RandomSeed` send the same stream `id` on their first `xmpp://` exchange, and two unseeded instances differ.
- [ ] No SASL nonce, challenge or token is drawn from the seeded source (a named test or a code reference in the adapter's doc comment).
- [ ] `dotnet build Surl.slnx -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
