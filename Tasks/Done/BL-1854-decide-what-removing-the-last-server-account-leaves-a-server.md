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
completed: 2026-11-27
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

- [x] An amendment to ADR-0132, marked "Decided by Claude under Stewart's delegation", states what an HTTP `GET`, an
      HTTP write, and each other kind of login get after the last account is removed, and the warning's text on the
      control endpoint, `Surl.Api` and `surl-mcp`.
- [x] The code and text tasks the decision needs are filed in `Tasks/Backlog`, or the amendment says none is needed.

## Notes

Decision (ADR-0132 amendment 5): a scheme that has had an account to log in to, configured or added, keeps asking for
a login when none is left; a reset or restore returns that with the accounts; a start with no account is unchanged.
Warning: `surl: warning: no local account left; the server still asks for a login wherever it did` (control endpoint,
`Surl.Api`) and `server <id> has no local account left; it still asks for a login wherever it did` (`surl-mcp`).

Why: removing an account is how a test takes access away, so it must give nobody access (secure by default). Closing
only the schemes that had an account at start would still open reads after removing an added account.

Confirmed by reading the source on 2026-11-27 (nothing run):

- The three removal warnings, `AuthenticationPolicy.JudgePasswordLogin`, `JudgeWithoutCredentials`, the other login
  checks of `AuthenticationPolicy`, and each server's call into it (HTTP and HTTP/3, WebSocket, RTSP, `gs`, S3, Azure
  Storage, OCI, MCP, gRPC, SignalR, WebTransport, DNS over HTTPS, CoAP and `coaps`).
- Reads also open over `ws`, `wss`, `rtsp`, `gs`, DNS over HTTPS, `coap` and `coaps`, not over HTTP alone, and over a
  scheme whose last account is removed while accounts restricted to other schemes remain, with no warning.
- SSH authorized keys still log in (decision 9). A `--user-ldap` directory then judges the removed name.
- `surl-mcp` writes "now needs no login" with `--user-ldap` too, and "logins are now checked against the directory
  only" is not so on a scheme the directory does not serve.
- ADR-0097 amendment 3 had rejected staying private once an account existed; amendment 5 supersedes it and answers its
  two reasons.
- No host resets the accounts alone, so a `--directory` server stays closed until restarted.
- In composition only the `coaps` certificate requirement depends on whether accounts were given at start.

Filed: BL-1855 (Authentication), BL-1856 (CoAP rename), BL-1857 (control endpoint), BL-1858 (`Surl.Api`), BL-1859
(`surl-mcp`), BL-1860 (help text), BL-1861 (decision pointers), BL-1862 (product documents and comments).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. ADR-0132 amendment 5 decides a scheme that has had an account keeps asking for a login after the last one is removed, with the new warning text; BL-1855 to BL-1862 filed
