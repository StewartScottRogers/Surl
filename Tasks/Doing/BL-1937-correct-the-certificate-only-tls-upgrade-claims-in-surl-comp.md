---
id: BL-1937
title: Correct the certificate-only TLS upgrade claims in Surl.Composition's registration list and notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1924]
touches: [Application/Surl.Composition.UnitLibrary/ProtocolServerRegistrations.cs, Application/Surl.Composition.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1937 — Correct the certificate-only TLS upgrade claims in Surl.Composition's registration list and notes

## Goal

`ProtocolServerRegistrations`' remarks and `Surl.Composition`'s `CLAUDE.md` say that TLS-SRP, as well as a
certificate, makes the TLS upgrade available and satisfies the TCP listeners that otherwise need a certificate.

## Context

Found while working BL-1924 (2026-11-27), read from source. Comment and project-notes text only; no behaviour change and
no string literal changes. Line numbers are as of that date and approximate.

- `ServerTlsComposition.IsTlsUpgradeAvailable` is a certificate (`--cert`, `--self-signed`) or TLS-SRP (ADR-0122
  decision 5).
- `ServerTlsComposition.FindListenUrlWithoutCertificate` skips every listen URL `IsSecuredByTlsSrp` accepts: under
  TLS-SRP, each one except `webtransport`, `coaps` and DNS over QUIC (ADR-0122 Amendment 5). So `natss`, `gs` and `oci`
  listen URLs are not refused `(58)` under TLS-SRP.
- `Application/Surl.Composition.UnitLibrary/ProtocolServerRegistrations.cs`, remarks, about lines 35-36: "SMTP and IMAP
  offer STARTTLS, POP3 STLS, FTP AUTH TLS and LDAP StartTLS only when a certificate is configured."
- `Application/Surl.Composition.UnitLibrary/CLAUDE.md`: the `ExtendedProtocolServers.Compose` bullet says a `gs` listen
  URL (about line 85, a long line) and an `oci` one without `--cert` or `--self-signed` is `(58)`, and (about lines
  95-97) the same of `natss`. The start-order table (about line 231) says "the TLS-first listen URL without a
  certificate (58)". The DNS over HTTPS and DNS over QUIC bullets already state their TLS-SRP behaviour.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] The `ProtocolServerRegistrations` remarks say STARTTLS, STLS, AUTH TLS and StartTLS are offered only when a certificate is configured or TLS-SRP is served.
- [ ] The `gs`, `oci` and `natss` sentences of `Surl.Composition.UnitLibrary/CLAUDE.md` say the `(58)` refusal does not happen under TLS-SRP, keeping what they say about `--oci-plaintext`.
- [ ] The start-order table's `(58)` entry is true of `FindListenUrlWithoutCertificate`, TLS-SRP and the listen URLs that require a certificate without being TLS from the first byte included.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
