---
id: BL-1954
title: Correct the certificate-only TLS upgrade claim in the ProtocolServerRegistrations remarks
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary/ProtocolServerRegistrations.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1954 — Correct the certificate-only TLS upgrade claim in the ProtocolServerRegistrations remarks

## Goal

The `<remarks>` of `ProtocolServerRegistrations` say the TLS upgrade is offered with a certificate or under TLS-SRP.

## Context

Found while working BL-1942 (2026-11-27), read from source. XML doc comments only; no code or behaviour change.

- `Application/Surl.Composition.UnitLibrary/ProtocolServerRegistrations.cs`, about lines 35 and 36, says SMTP and IMAP
  `STARTTLS`, POP3 `STLS`, FTP `AUTH TLS` and LDAP `StartTLS` are offered "only when a certificate is configured".
- The code passes the servers `ComposedProtocolServices.IsTlsUpgradeAvailable`, which is
  `ServerTlsComposition.IsTlsUpgradeAvailable`: a certificate (`--cert`, `--self-signed`) or TLS-SRP (ADR-0122 decision 5).

No store, event or fault is involved: comments only.

## Acceptance criteria

- [x] The remarks say the upgrades are offered when a certificate is configured or TLS-SRP is served.
- [x] The diff changes only `///` comment lines.
- [x] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean.

## Notes

- 2026-11-27: BL-1937 (commit `583ca328e`) had already made this correction. The remarks now read "only when a
  certificate is configured or TLS-SRP is served", with the `ServerTlsComposition.IsTlsUpgradeAvailable` reference.
  Its change to `ProtocolServerRegistrations.cs` is `///` lines only. This task edits no source file.
- Checked here rather than through `align-and-document`: there was nothing left to write, so a subagent would only
  have spent tokens.
- Verified 2026-11-27: the composition library build with `-warnaserror` and the solution `dotnet build` have 0
  warnings and 0 errors; the fast tests exit 0.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The ProtocolServerRegistrations remarks say the TLS upgrades are offered with a certificate or under TLS-SRP (already corrected by BL-1937, verified here)
