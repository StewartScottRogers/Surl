---
id: BL-2019
title: Ask HonoursTokenIssuedTo when an OCI bearer token issued to an account is used
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2018]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2019 — Ask HonoursTokenIssuedTo when an OCI bearer token issued to an account is used

## Goal

The OCI registry asks `IAuthenticationPolicy.HonoursTokenIssuedTo` whether a bearer token issued to an account is
still honoured.

## Context

ADR-0167 Amendment 3 (BL-2003), point 6. BL-2018 adds the member, whose default is `NamesAccount`.
`OciAccessCheck.FindLiveToken` (`Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary`) calls
`NamesAccount` today, so a token `OciTokenEndpoint` issued to an LDAP directory user is refused. BL-2004 makes
`Surl.Authentication` answer `true` for such a user; this task only changes what the registry asks, and its tests use
`UnitTestAuthenticationPolicy`. An anonymous token is still not checked. Judged by the distribution specification, no
upstream curl measurement.

Stand-in: no new store, event or fault. A test takes access away with `remove_account`, reset or restore of the
accounts, or a sessions reset, which revokes every token.

BL-2007 edits the XML comments of `OciAccessCheck.cs` and may mention `NamesAccount`; leave them naming the new
member.

## Acceptance criteria

- [x] A test whose policy honours the token's name (`HonoursTokenIssuedTo` true) while `NamesAccount` is false serves the `/v2/` request; it was seen failing before the change (said in Notes) and passes after.
- [x] A test whose policy answers `HonoursTokenIssuedTo` false while `NamesAccount` is true answers `401` with `error="invalid_token"`.
- [x] `OciAccessCheck` calls `HonoursTokenIssuedTo` for every token issued to an account and no longer calls `NamesAccount`; the existing removed-account and anonymous-token tests pass with their expected values unchanged.
- [x] The XML and code comments of the library and its `CLAUDE.md` ("Reset and accounts") name `HonoursTokenIssuedTo` and say a directory user's token may be honoured (ADR-0167 Amendment 3).
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and the library stays at 100% line and branch coverage.

## Notes

- Plan: one call changes in `OciAccessCheck.FindLiveToken`; no new type, exit code, wire byte or ADR (ADR-0167
  Amendment 3 already decides it). `UnitTestAuthenticationPolicy.HonoursTokens` (`null` answers as `NamesAccount`)
  drives the two new tests in `OciResetTests`.
- Seen failing before the change: `CheckAsync_TokenThePolicyHonoursForANameThatIsNoAccount_ServesV2` got `401`, and
  `CheckAsync_TokenOfANamedAccountThePolicyDoesNotHonour_IsAnswered401WithTheInvalidTokenChallenge` got `200`; the
  other 391 tests of the project passed. After the change all 393 pass.
- Gates: build with `-warnaserror` 0 warnings; `dotnet format --verify-no-changes` clean; fast tests 0 failed;
  `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.OciRegistry.UnitLibrary` 0 failing members; code review
  found nothing in the two OCI projects.
- Pipeline stages not delegated: conformance (upstream curl has no `oci` scheme and no answer's bytes change) and
  align-and-document (the comments and `CLAUDE.md` changed with the code, the reviewer checked them against
  ADR-0167 Amendment 3, and a search found no other statement about this check to correct).
- The duplicated plain-text row in `OciAccessCheck`'s remarks is left to BL-2007.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The OCI registry asks IAuthenticationPolicy.HonoursTokenIssuedTo for every account's bearer token, so a policy can honour a directory user's token.
