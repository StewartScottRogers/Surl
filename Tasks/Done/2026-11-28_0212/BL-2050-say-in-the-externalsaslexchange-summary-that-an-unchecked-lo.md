---
id: BL-2050
title: Say in the ExternalSaslExchange summary that an unchecked login is accepted without the account match
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary/ExternalSaslExchange.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-2050 — Say in the ExternalSaslExchange summary that an unchecked login is accepted without the account match

## Goal

The class summary of `ExternalSaslExchange` is true of a login under `--allow-anonymous`.

## Context

Found while working BL-2006 (2026-11-27), read from source. `///` comment text only; no code or behaviour change.

`Foundation/Surl.Authentication.UnitLibrary/ExternalSaslExchange.cs`: the summary says the login is accepted when an
account of the certificate's name exists and the authorization identity is empty or that name. `AnswerAsync` returns
`AcceptedUnchecked` when `Context.IsUnchecked`, matching neither. No store, event or fault changes: documentation only.

## Acceptance criteria

- [x] The `ExternalSaslExchange` summary states that an unchecked exchange is accepted without the account or authorization identity match.
- [x] The diff changes only `///` lines in `ExternalSaslExchange.cs`.
- [x] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean.

## Notes

- Edited in the session rather than through `align-and-document`: one comment in one file. "Accepted when" now
  reads "a checked login is accepted when", and a sentence covers `--allow-anonymous`
  (`SaslExchangeContext.IsUnchecked` is `AuthenticationSettings.AllowAnonymous`). The client certificate is still
  required, checked or not (`AuthenticationPolicy`).
- Verified 2026-11-28: library and solution builds clean with `-warnaserror`, `dotnet format --verify-no-changes`
  clean, fast tests 30641 passed, 0 failed, 3 skipped.

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The ExternalSaslExchange summary now says an --allow-anonymous login is accepted without the account or authorization identity match.
