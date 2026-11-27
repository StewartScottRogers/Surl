---
id: BL-1927
title: Say in --manual that STARTTLS-style upgrades and TLS schemes work under TLS-SRP with no certificate
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1923]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1927 — Say in --manual that STARTTLS-style upgrades and TLS schemes work under TLS-SRP with no certificate

## Goal

No `--manual` section says a TLS upgrade or a TLS scheme on TCP needs `--cert` or `--self-signed` without excepting
TLS-SRP.

## Context

Found while working BL-1923 (2026-11-27), read from source. Manual text and the tests that pin it; no behaviour change.

- Under TLS-SRP, `ServerTlsComposition.IsTlsUpgradeAvailable` is true with no certificate and
  `IsSecuredByTlsSrp` lifts the certificate requirement from every TCP TLS listener. Only `webtransport`, DNS over
  QUIC and a `coaps` listener with no account still need one (ADR-0122 Amendment 5).
- `Application/Surl.Cli.UnitLibrary/ManualText.cs` says "needs `--cert` or `--self-signed`" unqualified in about a
  dozen protocol sections (about lines 377 to 586), among them "StartTLS and ldaps need --cert or --self-signed".
  `grep` for `self-signed` to find each. The TLS-SRP OPTIONS section (about line 566) already states the rule.
- `ManualTextTests` pins the manual line for line, and every line stays within 79 columns.

No store, event or fault is involved: manual text only.

## Acceptance criteria

- [ ] No `ManualText` sentence says a TLS upgrade or a TLS scheme on TCP needs a certificate without excepting TLS-SRP.
- [ ] The `webtransport` and DNS over QUIC sentences still say a certificate is needed under TLS-SRP too.
- [ ] The tests in `ManualTextTests` pin the new wording.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
