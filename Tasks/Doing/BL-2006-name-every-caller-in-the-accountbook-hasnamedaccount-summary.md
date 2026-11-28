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
completed:
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

- [ ] The `HasNamedAccount` summary no longer reads as serving SASL `EXTERNAL` alone: it names or truthfully groups every caller (`EXTERNAL`, the Kerberos logins and `NamesAccount`).
- [ ] The diff changes only `///` lines in `AccountBook.cs`.
- [ ] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
