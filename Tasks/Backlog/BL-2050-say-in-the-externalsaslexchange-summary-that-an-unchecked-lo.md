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
completed:
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

- [ ] The `ExternalSaslExchange` summary states that an unchecked exchange is accepted without the account or authorization identity match.
- [ ] The diff changes only `///` lines in `ExternalSaslExchange.cs`.
- [ ] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
