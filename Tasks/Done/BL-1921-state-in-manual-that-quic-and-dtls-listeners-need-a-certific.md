---
id: BL-1921
title: State in --manual that QUIC and DTLS listeners need a certificate under TLS-SRP
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1916]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1921 — State in --manual that QUIC and DTLS listeners need a certificate under TLS-SRP

## Goal

The TLS-SRP OPTIONS section of `surl --manual` says which listen URLs still need `--cert` or `--self-signed` under TLS-SRP.

## Context

Found in BL-1916, which corrected the same statement on the `surl --aihelp tls` page. The `TLS-SRP OPTIONS` section in `Application/Surl.Cli.UnitLibrary/ManualText.cs` says TLS-SRP serves every TLS listen URL and that no certificate is sent. By ADR-0122 Amendment 5 TLS-SRP secures TCP listeners only: a `webtransport` listen URL, a DNS over QUIC one (`--dns-transport quic`) and a `coaps` one with no `--user` or `--user-file` account are refused `(58)` without `--cert` or `--self-signed`, and with one they serve it. The section's lines are pinned in `Application/Surl.Cli.UnitTests/ManualTextTests.cs`. Documentation only: no store, event or fault changes.

## Acceptance criteria

- [x] The `TLS-SRP OPTIONS` section limits the certificate-free case to TCP listeners and names `webtransport`, `--dns-transport quic` and `coaps` with no account as needing `--cert` or `--self-signed` (58), citing no behaviour ADR-0122 Amendment 5 does not state.
- [x] `dotnet build -warnaserror` is clean and the fast tests of `Application/Surl.Cli.UnitTests` pass.

## Notes
- Changed the `TLS-SRP OPTIONS` lines in `ManualText.cs` and their pins in `ManualTextTests.cs`, using the wording BL-1916 gave the `surl --aihelp tls` page. "No certificate is sent there" now refers to TCP listeners only.
- Verified: build `-warnaserror` 0 warnings, format check passed, fast tests 30327 passed, 0 failed, 3 skipped (`Surl.Cli.UnitTests` 2007 passed). No Integration run: text only.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. surl --manual says which listen URLs still need --cert or --self-signed under TLS-SRP
