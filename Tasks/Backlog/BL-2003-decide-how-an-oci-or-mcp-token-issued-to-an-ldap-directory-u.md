---
id: BL-2003
title: Decide how an OCI or MCP token issued to an LDAP directory user is judged when used
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0167-what-a-reset-snapshot-and-restore-do-with-in-progress-object-uploads-and-the-oci-and-mcp-servers-tokens.md, Documentation/Planning/Decisions/ADR-0149-how-an-ldap-directory-serves-as-an-optional-account-source.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-2003 — Decide how an OCI or MCP token issued to an LDAP directory user is judged when used

## Goal

An amendment to ADR-0167 says whether, and for how long, a token the OCI registry or the MCP server issued to a user
known only to the `--user-ldap` directory is honoured.

## Context

Found while working BL-1981 (2026-11-27). Read from source, not run and not tested.

- `AuthenticationPolicy.CheckPasswordLoginAsync` (`Foundation/Surl.Authentication.UnitLibrary`) accepts a name the
  account book does not hold when `LdapAccountSource` serves the scheme and the bind succeeds (ADR-0149).
- `OciTokenEndpoint` then issues a token to that name, and `McpPasswordCheck` gives the MCP authorization server the
  name (`/authorize` and `client_credentials`).
- At each use `OciAccessCheck.FindLiveToken` and `McpAuthorizationGrants.AccountOf` and `RedeemRefreshToken` ask
  `IAuthenticationPolicy.NamesAccount`. `AuthenticationPolicy.NamesAccount` asks only the account book
  (`AccountBook.HasNamedAccount`), so the new token is refused: `401` with `error="invalid_token"` on the registry,
  `401` (or no new tokens on refresh) on the MCP server. A directory user can log in and then do nothing.
- ADR-0167 decision 4 honours a token "only while that account still exists in the account book at that moment". It was
  written for removed accounts and does not mention a directory. ADR-0149 decision 2 serves "each extended server's
  clear-password login", and decision 4 checks the directory on every login with no cache; it needs the password to
  bind, so a token check cannot repeat the login.
- `AzureStorageProtocolServer` also asks `NamesAccount`, to read a path-style target (ADR-0109 decision 2). A change to
  what the member answers reaches it too.

Design question delegated to Claude (root `CLAUDE.md`, "Decisions"). Decide by the standing rules: secure by default,
a complete stand-in, the simplest change. No upstream curl measurement applies: upstream curl has neither `oci` nor
`mcp`, and the Curl port is never evidence.

Stand-in: the stores are the shared account book and the operator's own directory. The password login already publishes
`LoginJudgedEvent` and takes `refuse-login`; `remove_account`, reset and restore are how a test takes access away.

## Acceptance criteria

- [ ] ADR-0167 has an Amendment 3, marked "Decided by Claude under Stewart's delegation" and naming BL-1995, listed in its header and marked at decision 4; ADR-0149 has a dated note linking to it.
- [ ] The amendment says, for the OCI bearer token and the MCP access and refresh tokens, whether a directory user's token is issued and honoured, until when, and what happens after the directory disables or deletes the user.
- [ ] It keeps or explicitly changes each of these refusals: an account-book account removed by `remove_account`, reset or restore (also when the directory holds the same name), a scheme `protocols=` leaves out, and a name that never logged in.
- [ ] It says whether the Azure Storage path-style check answers differently under `--user-ldap`.
- [ ] It names the member that answers the token check and every library that changes; for any library other than `Surl.Authentication`, one task per library is filed with `task-board.ps1 new`, and added to BL-2004's `depends-on` where BL-2004 needs it first.
- [ ] No file outside `Documentation/Planning/Decisions/` and `Tasks/` changes.

## Notes

## Log

- 2026-11-27: Created.
