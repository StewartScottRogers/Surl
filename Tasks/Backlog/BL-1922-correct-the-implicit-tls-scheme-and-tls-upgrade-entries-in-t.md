---
id: BL-1922
title: Correct the implicit TLS scheme and TLS upgrade entries in the glossary
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1922 — Correct the implicit TLS scheme and TLS upgrade entries in the glossary

## Goal

The `implicit TLS scheme` and `TLS upgrade` entries of `Documentation/Wiki/Glossary.md` are true of the code.

## Context

Found while working BL-1881 (2026-11-27), read from source. Prose only.

- `implicit TLS scheme` (about line 57) lists ten schemes. `TlsSchemes.ImplicitTlsSchemes`
  (`Foundation/Surl.Protocol.Abstractions.UnitLibrary/TlsSchemes.cs`) also holds extended ones. Its sentence on second
  registrations leaves out the extended `stomps` and `xmpps` `ProtocolServerRegistration.ImplicitTls` entries in
  `ExtendedProtocolServers.cs`. Its "Name in code" column already says `ListenUrl.IsImplicitTls` only defaults to the
  set.
- `TLS upgrade` (about line 58) says the upgrade is offered only when a certificate is configured, and that
  `isTlsUpgradeAvailable` comes from `ServerTlsComposition.IsCertificateConfigured`. It comes from
  `ServerTlsComposition.IsTlsUpgradeAvailable` (a certificate, or TLS-SRP; ADR-0122 decision 5), set in
  `SurlServerComposition.Start.cs`. The entry also leaves out the extended `amqp1` (TLS protocol header) and `xmpp`
  (`STARTTLS`) upgrades listed in `ServerTlsComposition.UpgradableSchemes`.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] The `implicit TLS scheme` entry either names exactly the schemes in `TlsSchemes.ImplicitTlsSchemes` or points to that set without a partial list.
- [ ] Its sentence on second registrations is true of both `ProtocolServerRegistrations.Original` and `ExtendedProtocolServers.Registrations`.
- [ ] The `TLS upgrade` entry says the upgrade is offered with a certificate or under TLS-SRP, and names `ServerTlsComposition.IsTlsUpgradeAvailable` as the source of `isTlsUpgradeAvailable`.
- [ ] The `TLS upgrade` entry either covers every scheme in `ServerTlsComposition.UpgradableSchemes` or points to that set without a partial list.

## Notes

## Log

- 2026-11-27: Created.
