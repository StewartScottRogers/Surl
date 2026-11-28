---
id: BL-1996
title: Judge an LDAP directory user's OCI and MCP tokens as the amended ADR-0167 decides
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1995]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1996 — Judge an LDAP directory user's OCI and MCP tokens as the amended ADR-0167 decides

## Goal

`AuthenticationPolicy` answers the OCI and MCP per-use token check for an LDAP directory user as ADR-0167 Amendment 3
(BL-1995) decides.

## Context

Found while working BL-1981 (2026-11-27), read from source. With `--user-ldap` serving `oci` or `mcp`,
`AuthenticationPolicy.CheckPasswordLoginAsync` accepts a directory user and a token is issued to the name, but
`AuthenticationPolicy.NamesAccount` is `AccountBook.HasNamedAccount` alone, so `OciAccessCheck` and
`McpAuthorizationGrants` refuse that token as unknown.

Read Amendment 3 first: it fixes the behaviour, the member and the libraries. `touches` here covers a change inside
`Surl.Authentication`; BL-1995 files separate tasks for any other library. Tests drive the directory in memory through
`ILdapDirectoryConnector`, on the settings view the server's policy is composed with
(`AuthenticationSettings.ForScheme`). No socket, no `Integration` test, no upstream curl measurement (curl has neither
scheme).

Stand-in: no new store, event or fault. The password login keeps publishing `LoginJudgedEvent` and taking
`refuse-login`; a test takes a book account's access away with `remove_account`, reset or restore.

## Acceptance criteria

- [ ] A test in `Foundation/Surl.Authentication.UnitTests` logs a directory-only user in by `CheckPasswordLoginAsync` on the `oci` view and asserts the amendment's answer to the token check; it was run and seen failing before the production change (say so in Notes), and passes after.
- [ ] A test does the same for `mcp`, and one for the TLS twin `mcps`.
- [ ] Each case the amendment lists as refused has a passing test, a removed account-book account whose name the directory also holds among them.
- [ ] The XML comments of every member changed, and the library's `CLAUDE.md` on `NamesAccount` and the LDAP account source, state the amended rule.
- [ ] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean, the fast tests pass, and the library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
