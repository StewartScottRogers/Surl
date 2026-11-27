---
id: BL-1929
title: Correct the certificate-only STLS claims in the POP3 server's comments and notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1924]
touches: [Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1929 — Correct the certificate-only STLS claims in the POP3 server's comments and notes

## Goal

The POP3 library's comments and `CLAUDE.md` say `STLS` is available with a certificate or under TLS-SRP.

## Context

Found while working BL-1924 (2026-11-27), read from source. Comment and project-notes text only; no behaviour change and
no string literal changes. Line numbers are as of that date and approximate. `Surl.Composition`
(`ProtocolServerRegistrations`) passes `ServerTlsComposition.IsTlsUpgradeAvailable` as `isTlsUpgradeAvailable`: a
certificate (`--cert`, `--self-signed`) or TLS-SRP (ADR-0122 decision 5). The library does not reference
`Surl.Composition`, so name the type in `<c>`, not `<see cref>`.

In `Protocols/Mail/Surl.Protocol.Pop3.UnitLibrary`:

- `Pop3ProtocolServer.cs`, the `isTlsUpgradeAvailable` param, about line 90.
- `CLAUDE.md`, about line 24: "`Surl.Console` sets when `--cert` or `--self-signed` is given" (the setter is
  `Surl.Composition`).
- `CLAUDE.md`, about line 54: "`-ERR STLS not available` without a certificate".

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] The `isTlsUpgradeAvailable` param of `Pop3ProtocolServer` names both a certificate and TLS-SRP.
- [ ] `CLAUDE.md` says `Surl.Composition` sets `isTlsUpgradeAvailable` with a certificate or under TLS-SRP, and that `-ERR STLS not available` answers only when no upgrade is available.
- [ ] No comment or `CLAUDE.md` line in the library says `STLS` depends on a certificate alone.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

## Log

- 2026-11-27: Created.
