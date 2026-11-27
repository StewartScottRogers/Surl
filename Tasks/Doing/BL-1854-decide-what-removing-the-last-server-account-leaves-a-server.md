---
id: BL-1854
title: Decide what removing the last server account leaves a server asking for, and what the warning says
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1854 — Decide what removing the last server account leaves a server asking for, and what the warning says

## Goal

An ADR states what each server asks for after its last account is removed while it runs, and what the warning then
says.

## Context

- Found by BL-1839 by reading the source; no test was run. Confirm in the source before deciding.
- `ComposedControlTarget.RemoveAccountAsync`, `SurlServerAccounts.Remove` and `RemoveAccountTool` say the server
  "now needs no login".
- The policy is still an `AuthenticationPolicy` over the now empty book: `JudgePasswordLogin` refuses a login with no
  user name unless `--allow-anonymous` and checks every named one against the empty book, while
  `HttpAuthenticationSession.JudgeWithoutCredentials` lets a `GET` or `HEAD` without credentials proceed. So content
  an account protected becomes readable over HTTP, and the warning is false of other logins.
- ADR-0032 section 4 (a start with no account serves `GET` and `HEAD` anonymously) and ADR-0132 amendment 4 (the
  same question for proxy accounts: stay closed, through `RequiresLoginWithoutAccounts`) bear on it. With
  `--user-ldap` another line is written and the directory still checks logins.

## Acceptance criteria

- [ ] An amendment to ADR-0132, marked "Decided by Claude under Stewart's delegation", states what an HTTP `GET`, an
      HTTP write, and each other kind of login get after the last account is removed, and the warning's text on the
      control endpoint, `Surl.Api` and `surl-mcp`.
- [ ] The code and text tasks the decision needs are filed in `Tasks/Backlog`, or the amendment says none is needed.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
