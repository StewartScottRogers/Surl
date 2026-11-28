---
id: BL-1916
title: State in --aihelp that QUIC and DTLS listeners need a certificate under TLS-SRP
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1880]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1916 — State in --aihelp that QUIC and DTLS listeners need a certificate under TLS-SRP

## Goal

The `surl --aihelp tls` page says which listen URLs still need `--cert` or `--self-signed` under TLS-SRP.

## Context

Found in BL-1880. The TLS-SRP paragraph of the `tls` topic in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` says `--tlsauthtype SRP` makes every TLS listen URL serve the SRP suites with no certificate, so no `--cert` or `--self-signed` is needed. That is true of TCP listeners only. By ADR-0122 Amendment 5, a `webtransport` listen URL, a DNS over QUIC one (`--dns-transport quic`) and a `coaps` one with no account are refused `(58)` without a certificate, and with one they serve it.

## Acceptance criteria

- [x] The TLS-SRP paragraph in `AiHelpProse` limits "no certificate" to TCP listeners and names `webtransport`, `--dns-transport quic` and `coaps` with no account as needing `--cert` or `--self-signed` (58), citing no behaviour ADR-0122 Amendment 5 does not state.
- [x] `dotnet build -warnaserror` is clean and the fast tests of `Application/Surl.Cli.UnitTests` pass.

## Notes

- "No account" is written as "no `--user` or `--user-file` account", the two sources ADR-0122 Amendment 5 names for `coaps` pre-shared keys.
- Added `AiHelpTextTests.Answer_TlsTopic_SaysQuicAndDtlsListenersStillNeedACertificateUnderTlsSrp` to pin the two sentences.
- No ADR: this states an existing decision; no behaviour changed.
- Verification (2026-11-27): build 0 warnings, `dotnet format --verify-no-changes` clean, `Surl.Cli.UnitTests` 2007 of 2007. In the full fast run one test outside this task failed on a 30 s wait, `Surl.Quic.UnitTests` `ServeAsync_ClientSendsInTheClosingState_...`; that project passed 514 of 514 when run again. Filed as BL-1922.
- Filed BL-1921: the `TLS-SRP OPTIONS` section of `--manual` makes the same unlimited statement.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. surl --aihelp tls says TLS-SRP needs no certificate on TCP only, and that webtransport, DNS over QUIC and coaps with no account still need --cert or --self-signed (58)
