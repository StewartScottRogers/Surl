---
id: BL-890
title: Refuse OCI Bearer tokens over plain text without --allow-plaintext-auth
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-439]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-890 — Refuse OCI Bearer tokens over plain text without --allow-plaintext-auth

## Goal

On a plain-text `oci` connection (`--oci-plaintext`) a `/v2/` request carrying `Authorization: Bearer` is
`403 DENIED`, message `credentials over plain text are refused`, unless `--allow-plaintext-auth` is given.

## Context

- ADR-0115 decision 4.4's last row and decision 6; Amendment 1 point 7 left it here because `--oci-plaintext`
  arrives with BL-439.
- `OciAccessCheck` (BL-438) checks the token; `Basic` at `/token` is already refused over plain text through
  `IAuthenticationPolicy.CheckPasswordLoginAsync` (`RefusedPlaintext`). The server needs a way to learn
  `--allow-plaintext-auth` - decide it (a constructor option set by composition is the likely answer) and
  record it in an ADR amendment.

## Acceptance criteria

- [ ] A named test shows a Bearer token on a plain-text connection refused `403 DENIED` with the plain-text
      message, and the same request served under `--allow-plaintext-auth` and over TLS.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-05: Created.
