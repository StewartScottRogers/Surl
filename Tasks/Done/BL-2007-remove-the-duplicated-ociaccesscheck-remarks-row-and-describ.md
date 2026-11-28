---
id: BL-2007
title: Remove the duplicated OciAccessCheck remarks row and describe every use of its policy
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary/OciAccessCheck.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2007 — Remove the duplicated OciAccessCheck remarks row and describe every use of its policy

## Goal

The XML comments of `OciAccessCheck` list each refusal once and describe every use of its `policy` parameter.

## Context

Found while working BL-1981 (2026-11-27), read from source. `///` comment text only; no code or behaviour change.

`Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary/OciAccessCheck.cs`:

- The `<remarks>` table holds the row "A Bearer token over plain text without `--allow-plaintext-auth`: `403 DENIED`"
  twice, word for word.
- `<param name="policy">` says "Says whether anonymous logins are allowed." The class also asks it whether plain-text
  secrets are refused (`RefusesPlaintextAsync`, a probe `CheckPasswordLoginAsync`) and, through `NamesAccount`,
  whether a token's account still exists (`FindLiveToken`).

Describe what the code does today; BL-2003 decides about LDAP directory users separately. No store, event or fault
changes: documentation only.

## Acceptance criteria

- [x] The plain-text Bearer row appears once in the `<remarks>` table.
- [x] The `policy` parameter comment names the anonymous-login check, the plain-text check and the token-account check.
- [x] The diff changes only `///` lines in `OciAccessCheck.cs`.
- [x] `dotnet build Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary -warnaserror` is clean.

## Notes

- The token-account check is now `IAuthenticationPolicy.HonoursTokenIssuedTo` (commit `b8eebb908`), not the
  `NamesAccount` call the Context names, so the comment names the method the code calls today.
- The remaining Bearer row also needed correcting: `--allow-anonymous` allows a Bearer token over plain text too
  (`AuthenticationPolicy.JudgePasswordLogin` accepts unchecked before its plain-text test; ADR-0115 Amendment 2),
  and the refusal happens before the token lookup. The row now says both.
- Verified: `dotnet build` clean, fast tests green, `dotnet format --verify-no-changes` on the file clean.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. OciAccessCheck's XML comments list each refusal once and name every use of its policy
