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
completed: 2026-11-27
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

- [x] No `ManualText` sentence says a TLS upgrade or a TLS scheme on TCP needs a certificate without excepting TLS-SRP.
- [x] The `webtransport` and DNS over QUIC sentences still say a certificate is needed under TLS-SRP too.
- [x] The tests in `ManualTextTests` pin the new wording.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Changed the sections for `amqps`, `amqp1s` and the `amqp1` TLS header, DNS over TLS and HTTPS, `ldaps` and
  StartTLS, `mcps`, `natss`, `oci`, the HTTPS proxy, `s3`, `smbs`, `stomps` and `wss`, plus the deployment checklist's
  item 3 and exit code 58. Wording follows `--aihelp`: "`--cert`, `--self-signed` or TLS-SRP". Checked against
  `ServerTlsComposition.IsSecuredByTlsSrp` and `IsTlsUpgradeAvailable`.
- DNS OPTIONS now also says TLS-SRP needs another listen URL that uses TLS, as a `dns` one does not count
  (`TlsSrpCombination.UsesTls`); without that, "or TLS-SRP" would suggest `tls`/`https` work alone.
- `webtransport` appears only in the TLS-SRP OPTIONS section, already right and unchanged, as is DNS over QUIC there;
  DNS OPTIONS now says `quic` needs `--cert` or `--self-signed` under TLS-SRP too.
- No ADR: wording of behaviour ADR-0122 already decides.
- BL-1935 overlaps: its `ManualText.cs` criterion is met by this task. Its `--aihelp` half remains.
- Integration tests not run: manual text only.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. --manual now says TCP TLS schemes and TLS upgrades work with --cert, --self-signed or TLS-SRP; QUIC listeners still need a certificate
