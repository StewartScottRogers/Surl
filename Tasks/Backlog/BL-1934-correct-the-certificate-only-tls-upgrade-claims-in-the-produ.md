---
id: BL-1934
title: Correct the certificate-only TLS upgrade claims in the product documents, README and glossary
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1924]
touches: [Documentation/Product/Product-Overview.md, Documentation/Product/Requirements.md, Documentation/Wiki/Glossary.md, README.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1934 — Correct the certificate-only TLS upgrade claims in the product documents, README and glossary

## Goal

The product overview, requirements, README and glossary say a TLS upgrade is offered with a certificate or under
TLS-SRP.

## Context

Found while working BL-1924 (2026-11-27), read from source. Prose only; no behaviour change. Line numbers are as of that
date and approximate. `ServerTlsComposition.IsTlsUpgradeAvailable` is a certificate (`--cert`, `--self-signed`) or
TLS-SRP (ADR-0122 decision 5). Under TLS-SRP a TCP listen URL that is TLS from the first byte also needs no certificate
(ADR-0122 Amendment 5; `webtransport`, DNS over QUIC and `coaps` with no account still do), so correct that half too
where it shares a sentence or passage listed here. The glossary's `TLS upgrade` entry is already right (BL-1925).

- `Documentation/Product/Product-Overview.md`: about line 152 (explicit FTPS), 270 (`STARTTLS`, `STLS`), 403 (LDAP root
  DSE) and 420 (`StartTLS`).
- `Documentation/Product/Requirements.md`: the `AUTH` answered `534` "with no certificate" clause of FR-037 (about line
  74), the `STARTTLS`/`STLS` "with `--cert` or `--self-signed`" clauses of the SMTP, IMAP and POP3 rows (about lines
  80-82) and the `StartTLS` clause of the LDAP row (about line 86). State what is built; add no new requirement.
- `README.md`: about lines 212 (`AUTH TLS`, `ftps`), 293-296 (mail upgrades and `smtps`, `imaps`, `pop3s`) and 367
  (`StartTLS`, `ldaps`).
- `Documentation/Wiki/Glossary.md`: the `explicit FTPS` entry (about line 155) and the `throwaway certificate` entry
  (about line 285), which says that without `--cert` or `--self-signed` a TLS listen URL ends surl with 58 and an
  upgradable one offers no upgrade, and lists only `ftp`, `imap`, `pop3` and `smtp` as upgradable
  (`ServerTlsComposition.UpgradableSchemes` also holds `amqp1`, `ldap` and `xmpp`).

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] The four Product-Overview passages named in Context say the upgrade is offered with a certificate or under TLS-SRP.
- [ ] The FR-037 clause and the SMTP, IMAP, POP3 and LDAP upgrade clauses in `Requirements.md` name both, with each refusal answer tied to neither being present.
- [ ] The three README passages name TLS-SRP, and none says the upgrade or its TCP TLS twin needs `--cert` or `--self-signed` without that exception.
- [ ] The `explicit FTPS` glossary entry names both ways to get `234`, and gives `534` for neither.
- [ ] The `throwaway certificate` glossary entry is true under TLS-SRP and names every scheme in `ServerTlsComposition.UpgradableSchemes`.

## Notes

## Log

- 2026-11-27: Created.
