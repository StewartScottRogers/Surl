---
id: BL-1938
title: Correct the certificate-only STARTTLS claims in the SMTP server's comments and notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1924]
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1938 — Correct the certificate-only STARTTLS claims in the SMTP server's comments and notes

## Goal

The SMTP library's comments and `CLAUDE.md` say `STARTTLS` is available with a certificate or under TLS-SRP.

## Context

Found while working BL-1924 (2026-11-27), read from source. Comment and project-notes text only; no behaviour change and
no string literal changes. Line numbers are as of that date and approximate. `Surl.Composition`
(`ProtocolServerRegistrations`) passes `ServerTlsComposition.IsTlsUpgradeAvailable` as `isTlsUpgradeAvailable`: a
certificate (`--cert`, `--self-signed`) or TLS-SRP (ADR-0122 decision 5). The library does not reference
`Surl.Composition`, so name the type in `<c>`, not `<see cref>`.

In `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary`:

- `SmtpProtocolServer.cs`, the `isTlsUpgradeAvailable` param, about line 94.
- `SmtpSession.cs`, about line 189: "only on a plaintext connection of a server with a certificate".
- `CLAUDE.md`, about line 25: "`Surl.Console` sets when `--cert` or `--self-signed` is given" (the setter is
  `Surl.Composition`).
- `CLAUDE.md`, about line 58: "`454` without a certificate".

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] The `isTlsUpgradeAvailable` param of `SmtpProtocolServer` and the `SmtpSession` comment name both a certificate and TLS-SRP.
- [ ] `CLAUDE.md` says `Surl.Composition` sets `isTlsUpgradeAvailable` with a certificate or under TLS-SRP, and that `454` answers `STARTTLS` only when no upgrade is available.
- [ ] No comment or `CLAUDE.md` line in the library says `STARTTLS` depends on a certificate alone.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

## Log

- 2026-11-27: Created.
