---
id: BL-1051
title: Serve the signed TLS-SRP suites from surl when --cert or --self-signed is given
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-911]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-11
completed:
---
# BL-1051 — Serve the signed TLS-SRP suites from surl when --cert or --self-signed is given

## Goal

`surl --tlsauthtype SRP --tlsuser u --tlspassword p --cert server.pem https://...` (or `--self-signed`) starts, and its TLS-SRP listeners also offer the RSA- or DSS-signed SRP suites with that certificate, as ADR-0122 decision 2 says.

## Context

- ADR-0122 decision 2 and Amendment 2: BL-567 refuses `--cert` and `--self-signed` beside TLS-SRP
  (`TlsSrpCombination.FindRefusedBeside` in `Surl.Cli`) until BL-911 adds the signed suites to
  `Surl.TlsSrp`. This task lifts that refusal.
- `ServerTlsComposition.ComposeTlsSrp` (Composition) builds `ServerTlsSettings.ForTlsSrp`
  (Networking) with accounts only; it needs the loaded or throwaway certificate too, handed to
  whatever `TlsSrpServer` takes after BL-911. A `--cert` whose key is neither RSA nor DSA is
  `CertificateProblem` (58), decision 6.
- Update the `tls` `--aihelp` paragraph and `ManualText`'s TLS-SRP OPTIONS section in the same change.

## Acceptance criteria

- [ ] `Surl.Cli.UnitTests`: TLS-SRP with `--cert` and with `--self-signed` is served, and the
      refusal tests for them are gone.
- [ ] `Surl.Networking.UnitTests`: a TLS-SRP handshake with an RSA certificate in the settings
      chooses `C0 21` from pinned upstream curl's recorded ClientHello.
- [ ] `Surl.Composition.UnitTests`: a `--cert` with an ECDSA key under TLS-SRP is `CertificateProblem` (58).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and each touched library keeps 100% line and branch coverage.

## Notes

- Filed by BL-567 on 2026-11-11.

## Log

- 2026-11-11: Created.
- 2026-11-13: Backlog -> Doing.
