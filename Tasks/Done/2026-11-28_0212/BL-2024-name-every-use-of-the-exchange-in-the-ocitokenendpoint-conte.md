---
id: BL-2024
title: Name every use of the exchange in the OciTokenEndpoint context parameter comment
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary/OciTokenEndpoint.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-2024 — Name every use of the exchange in the OciTokenEndpoint context parameter comment

## Goal

The `context` parameter comment of `OciTokenEndpoint` names every use the class makes of the exchange.

## Context

Found while working BL-2005 (2026-11-27), read from source. `///` comment text only; no code or behaviour change.

`Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary/OciTokenEndpoint.cs`: `<param name="context">`
says "The exchange: its random source." The class uses the exchange three ways:

- `context.Identifiers`, handed to `OciTokenBook.Issue` for the token's bytes;
- `context.TimeProvider`, for the `issued_at` of the token response (`TokenBody`);
- `LoginExchange.Of(context)`, which puts the exchange ID and listen URL on each `PasswordLogin` (`LogInAsync`).

Describe what the code does when this task runs, and search the class again first. BL-2007 corrects comments in
`OciAccessCheck.cs` and does not cover this file. No store, event or fault changes: documentation only.

## Acceptance criteria

- [x] The `context` parameter comment names the source of the token's bytes, the clock behind `issued_at` and the exchange identity carried on the login.
- [x] The diff changes only `///` lines in `OciTokenEndpoint.cs`.
- [x] `dotnet build Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary -warnaserror` is clean.

## Notes

- 2026-11-28: The class now uses the exchange two ways, not the three under Context. Since commit `edbff0065`
  the token's bytes come from `OciTokenBook`'s `RandomNumberGenerator`, not `context.Identifiers`, and the
  comment already named the clock and the login's exchange ID and listen URL. Added one sentence saying the
  token's bytes do not come from `context` and naming their source, so criterion 1 holds and stays true of
  the code.
- 2026-11-28: Verified: solution build with `-warnaserror` 0 warnings, `dotnet format --verify-no-changes`
  clean, fast tests 30641 passed, 0 failed, 3 skipped. Integration tests not run: comment text only.

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The OciTokenEndpoint context parameter comment names the clock and login exchange identity it supplies and where the token's bytes come from instead
