---
id: BL-2018
title: Add IAuthenticationPolicy.HonoursTokenIssuedTo for the OCI and MCP per-use token check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2003]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-2018 — Add IAuthenticationPolicy.HonoursTokenIssuedTo for the OCI and MCP per-use token check

## Goal

`IAuthenticationPolicy` has the member that says whether a token issued to an account name is still honoured.

## Context

ADR-0167 Amendment 3 (BL-2003), point 6, in
`Documentation/Planning/Decisions/ADR-0167-what-a-reset-snapshot-and-restore-do-with-in-progress-object-uploads-and-the-oci-and-mcp-servers-tokens.md`.
Today `OciAccessCheck` and `McpAuthorizationGrants` ask `NamesAccount` when a token is used, so a token issued to an
LDAP directory user is refused. The new member, `bool HonoursTokenIssuedTo(string accountName)`, is a default
interface member of `Foundation/Surl.Protocol.Abstractions.UnitLibrary/IAuthenticationPolicy.cs` returning
`NamesAccount(accountName)`, so every policy and test double behaves as before. It judges no login: it publishes no
event and asks no fault rule. `Surl.Authentication` overrides it (BL-2004); the OCI and MCP servers call it (BL-2019,
BL-2020). No upstream curl measurement (curl has neither scheme).

BL-2005 edits the library's `CLAUDE.md` on `NamesAccount`; start from what it wrote.

## Acceptance criteria

- [ ] `IAuthenticationPolicy.HonoursTokenIssuedTo(string accountName)` is a default interface member that returns `NamesAccount(accountName)` and throws `ArgumentNullException` for `null`.
- [ ] Tests in `Foundation/Surl.Protocol.Abstractions.UnitTests` show that the default answers `true` and `false` as a policy's own `NamesAccount` does, and throws for `null`.
- [ ] The member's XML comment says when a server asks it (a token it issued to an account is used), that the default is `NamesAccount`, and that a policy may also honour a directory user's token as ADR-0167 Amendment 3 decides.
- [ ] The `NamesAccount` XML comment and the library's `CLAUDE.md` give the token check as `HonoursTokenIssuedTo`'s question, and the Azure Storage path-style check as `NamesAccount`'s.
- [ ] `dotnet build -warnaserror` is clean, the fast tests pass, and the library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
