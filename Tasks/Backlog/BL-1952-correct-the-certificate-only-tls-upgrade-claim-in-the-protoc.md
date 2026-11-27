---
id: BL-1952
title: Correct the certificate-only TLS upgrade claim in the ProtocolServerRegistrations remarks
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary/ProtocolServerRegistrations.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-1952 — Correct the certificate-only TLS upgrade claim in the ProtocolServerRegistrations remarks

## Goal

The `<remarks>` of `ProtocolServerRegistrations` say the TLS upgrade is offered with a certificate or under TLS-SRP.

## Context

Found while working BL-1942 (2026-11-27), read from source. XML doc comments only; no code or behaviour change.

- `Application/Surl.Composition.UnitLibrary/ProtocolServerRegistrations.cs`, about lines 35 and 36, says SMTP and IMAP
  `STARTTLS`, POP3 `STLS`, FTP `AUTH TLS` and LDAP `StartTLS` are offered "only when a certificate is configured".
- The code passes the servers `ComposedProtocolServices.IsTlsUpgradeAvailable`, which is
  `ServerTlsComposition.IsTlsUpgradeAvailable`: a certificate (`--cert`, `--self-signed`) or TLS-SRP (ADR-0122 decision 5).

No store, event or fault is involved: comments only.

## Acceptance criteria

- [ ] The remarks say the upgrades are offered when a certificate is configured or TLS-SRP is served.
- [ ] The diff changes only `///` comment lines.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
