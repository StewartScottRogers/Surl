---
id: BL-1914
title: State in --aihelp that QUIC and DTLS listeners need a certificate under TLS-SRP
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1880]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1914 — State in --aihelp that QUIC and DTLS listeners need a certificate under TLS-SRP

## Goal

The `surl --aihelp tls` page says which listen URLs still need `--cert` or `--self-signed` under TLS-SRP.

## Context

Found in BL-1880. The TLS-SRP paragraph of the `tls` topic in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` says `--tlsauthtype SRP` makes every TLS listen URL serve the SRP suites with no certificate, so no `--cert` or `--self-signed` is needed. That is true of TCP listeners only. By ADR-0122 Amendment 5, a `webtransport` listen URL, a DNS over QUIC one (`--dns-transport quic`) and a `coaps` one with no account are refused `(58)` without a certificate, and with one they serve it.

## Acceptance criteria

- [ ] The TLS-SRP paragraph in `AiHelpProse` limits "no certificate" to TCP listeners and names `webtransport`, `--dns-transport quic` and `coaps` with no account as needing `--cert` or `--self-signed` (58), citing no behaviour ADR-0122 Amendment 5 does not state.
- [ ] `dotnet build -warnaserror` is clean and the fast tests of `Application/Surl.Cli.UnitTests` pass.

## Notes

## Log

- 2026-11-27: Created.
