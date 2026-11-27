---
id: BL-1928
title: Correct the certificate-only STARTTLS claims in the IMAP server's comments and notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1924]
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1928 — Correct the certificate-only STARTTLS claims in the IMAP server's comments and notes

## Goal

The IMAP library's comments and `CLAUDE.md` say `STARTTLS` is available with a certificate or under TLS-SRP.

## Context

Found while working BL-1924 (2026-11-27), read from source. Comment and project-notes text only; no behaviour change and
no string literal changes. Line numbers are as of that date and approximate. `Surl.Composition`
(`ProtocolServerRegistrations`) passes `ServerTlsComposition.IsTlsUpgradeAvailable` as `isTlsUpgradeAvailable`: a
certificate (`--cert`, `--self-signed`) or TLS-SRP (ADR-0122 decision 5). The library does not reference
`Surl.Composition`, so name the type in `<c>`, not `<see cref>`.

In `Protocols/Mail/Surl.Protocol.Imap.UnitLibrary`:

- `ImapProtocolServer.cs`, the `isTlsUpgradeAvailable` param, about line 85.
- `ImapSession.Authentication.cs`, about line 22: "or when the server has no certificate".
- `CLAUDE.md`, about line 27: "`Surl.Console` sets when `--cert` or `--self-signed` is given" (the setter is
  `Surl.Composition`).
- `CLAUDE.md`, about line 83: the refusal "without a certificate".

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [x] The `isTlsUpgradeAvailable` param of `ImapProtocolServer` and the comment above `AnswerStartTlsAsync` name both a certificate and TLS-SRP.
- [x] `CLAUDE.md` says `Surl.Composition` sets `isTlsUpgradeAvailable` with a certificate or under TLS-SRP, and that `STARTTLS` is refused as unavailable only when no upgrade is available.
- [x] No comment or `CLAUDE.md` line in the library says `STARTTLS` depends on a certificate alone.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

- `align-and-document` edited `ImapProtocolServer.cs` (the param), `ImapSession.Authentication.cs` (the comment above
  `AnswerStartTlsAsync`) and `CLAUDE.md` (the setter and the refusal). The class remarks, `ImapCapabilities.cs` and
  `ImapSession.cs` made no certificate-only claim.
- Verified 2026-11-27: build 0 warnings, `dotnet format --verify-no-changes` clean, fast tests 30334 passed, 0 failed,
  3 skipped. No Integration run: only comments and notes changed.
- The same claims elsewhere already have their own tasks (BL-1927, BL-1929 to BL-1932, BL-1935 to BL-1938). Filed
  BL-1939 for the library's remaining `Surl.Console` attributions, which are outside this task's certificate wording.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. IMAP comments and CLAUDE.md say STARTTLS is available with a certificate or under TLS-SRP
