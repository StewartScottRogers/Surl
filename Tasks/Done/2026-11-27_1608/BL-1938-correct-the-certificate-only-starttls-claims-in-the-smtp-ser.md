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
completed: 2026-11-27
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

- [x] The `isTlsUpgradeAvailable` param of `SmtpProtocolServer` and the `SmtpSession` comment name both a certificate and TLS-SRP.
- [x] `CLAUDE.md` says `Surl.Composition` sets `isTlsUpgradeAvailable` with a certificate or under TLS-SRP, and that `454` answers `STARTTLS` only when no upgrade is available.
- [x] No comment or `CLAUDE.md` line in the library says `STARTTLS` depends on a certificate alone.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

- Made in the lane's own session rather than delegated to `align-and-document`: four lines in three files.
- Checked against `ServerTlsComposition.IsTlsUpgradeAvailable` and `ProtocolServerRegistrations` before writing.
- The library's remaining `Surl.Console` composer claims and broken ADR links are left to BL-1948, already filed.
- Verify: build 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests 30366 passed, 3 skipped, 0 failed
  (83 assemblies). No integration tests run: comments and notes only.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. SMTP comments and CLAUDE.md say STARTTLS is available with a certificate or under TLS-SRP
