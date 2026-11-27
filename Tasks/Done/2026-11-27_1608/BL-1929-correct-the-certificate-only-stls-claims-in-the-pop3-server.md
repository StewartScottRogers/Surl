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
completed: 2026-11-27
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

- [x] The `isTlsUpgradeAvailable` param of `Pop3ProtocolServer` names both a certificate and TLS-SRP.
- [x] `CLAUDE.md` says `Surl.Composition` sets `isTlsUpgradeAvailable` with a certificate or under TLS-SRP, and that `-ERR STLS not available` answers only when no upgrade is available.
- [x] No comment or `CLAUDE.md` line in the library says `STLS` depends on a certificate alone.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

- Changed: the `isTlsUpgradeAvailable` XML doc in `Pop3ProtocolServer.cs` and two passages in the library's `CLAUDE.md`.
  Checked against `ServerTlsComposition.IsTlsUpgradeAvailable` and `Pop3Session.AnswerStlsAsync`. No new decision, so no ADR.
- 2026-11-27 verify: build with `-warnaserror` 0 warnings, format check clean, fast tests 30366 passed, 3 skipped,
  0 failed. `Integration` tests not run: comment and notes text only.
- Follow-ups filed: BL-1946 (this library's `Surl.Console` ownership claims and broken ADR links) and BL-1947 to
  BL-1950 (the same defects in sibling libraries).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The POP3 library's comments and CLAUDE.md say STLS is available with a certificate or under TLS-SRP
