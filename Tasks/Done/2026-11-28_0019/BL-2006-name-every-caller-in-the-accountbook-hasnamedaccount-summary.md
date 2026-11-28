---
id: BL-2006
title: Name every caller in the AccountBook.HasNamedAccount summary
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary/AccountBook.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2006 — Name every caller in the AccountBook.HasNamedAccount summary

## Goal

The summary of `AccountBook.HasNamedAccount` is true of every caller it has.

## Context

Found while working BL-1981 (2026-11-27), read from source. `///` comment text only; no code or behaviour change.

`Foundation/Surl.Authentication.UnitLibrary/AccountBook.cs`: the summary of `HasNamedAccount` gives SASL `EXTERNAL`
as its use. It is also called by `AuthenticationPolicy.NamesAccount` and `FindKerberosAccountName` (the SOCKS5 GSS-API
login), `GssapiSaslExchange`, `GssSpnegoSaslExchange` and `NegotiateKerberosLogin`, beside `ExternalSaslExchange`.
Search again before writing: BL-2004 may have changed the callers.

BL-1994 corrects other summaries in the same library and does not cover this one. No store, event or fault changes:
documentation only.

## Acceptance criteria

- [x] The `HasNamedAccount` summary no longer reads as serving SASL `EXTERNAL` alone: it names or truthfully groups every caller (`EXTERNAL`, the Kerberos logins and `NamesAccount`).
- [x] The diff changes only `///` lines in `AccountBook.cs`.
- [x] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean.

## Notes

- Callers searched again (2026-11-27), unchanged from Context: `ExternalSaslExchange`, `GssapiSaslExchange`,
  `GssSpnegoSaslExchange`, `NegotiateKerberosLogin`, `AuthenticationPolicy.FindKerberosAccountName` and
  `AuthenticationPolicy.NamesAccount` (also used by `HonoursTokenIssuedTo`).
- The summary says "checked logins" because those paths skip the account match under `--allow-anonymous`.
- Verified: full `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests green.
- Filed BL-2050: the `ExternalSaslExchange` class summary omits the unchecked acceptance.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The HasNamedAccount summary names every caller: SASL EXTERNAL, the Kerberos logins and NamesAccount.
