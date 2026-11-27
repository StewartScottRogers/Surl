---
id: BL-1861
title: Point the decisions that open reads when no account is configured at ADR-0132 amendment 5
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1863]
touches: [Documentation/Planning/Decisions/ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md, Documentation/Planning/Decisions/ADR-0071-how-the-websocket-server-answers-upstream-curl.md, Documentation/Planning/Decisions/ADR-0074-how-the-rtsp-server-answers-upstream-curl.md, Documentation/Planning/Decisions/ADR-0092-how-the-dns-server-answers-dns-and-its-encrypted-transports.md, Documentation/Planning/Decisions/ADR-0097-how-the-coap-server-answers-coap-and-coaps.md, Documentation/Planning/Decisions/ADR-0149-how-an-ldap-directory-serves-as-an-optional-account-source.md, Documentation/Planning/Decisions/README.md, Documentation/Planning/Decisions/ADR-0034-curl-style-help-categories-and-the-manual.md, Documentation/Planning/Decisions/ADR-0180-an-http-server-judges-the-login-before-the-method.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1861 — Point the decisions that open reads when no account is configured at ADR-0132 amendment 5

## Goal

No decision reads as if removing the last account while a server runs opens its reads.

## Context

- ADR-0132 amendment 5.
- Passages: ADR-0032 section 4 ("With no account configured"), ADR-0071 "Anonymous upgrade is a read", ADR-0074
  "Reads need no login while no account is configured", ADR-0092 decision 5 "Logins", ADR-0097 decision 6 and its
  amendment 3 (the "Removal" and "Alternative rejected" bullets are superseded), and ADR-0149 amendment 1's `Surl.Api`
  bullet, which gives the old directory warning and a method name BL-1863 renames.

## Acceptance criteria

- [x] Each passage named in Context carries a short pointer to ADR-0132 amendment 5 saying what it changes; the
      original wording stays as the record.
- [x] ADR-0097 amendment 3's "Removal" and "Alternative rejected" bullets are marked superseded.
- [x] ADR-0149 says the warning it quotes is replaced by ADR-0132 amendment 5's line, and names
      `IsLoginRequiredToRead` as what the HTTP family asks now.
- [x] The index rows of the changed decisions in the decisions `README.md` point to amendment 5.

## Notes

- Added ADR-0180 and ADR-0034 to `touches` and gave each the same pointer: ADR-0180 decisions 2 and 3 ("No account
  ... GET and HEAD are served") and ADR-0034's manual row 8 ("anonymous HTTP reads only while no account is
  configured") also read as if removing the last account opened reads, which the Goal rules out. No task in Doing
  names either file.
- Left alone: ADR-0072, ADR-0116, ADR-0117 and ADR-0119 mention "no account" only to refuse anonymous access or to
  explain why a request is judged as a write; none says a removal opens anything.
- ADR-0097 also got an "Amended by" entry in its header, and a pointer in decision 6 itself.
- Checked against the source: `HttpAuthenticationSession` and `ExtendedProtocolServers` ask
  `AuthenticationSettings.IsLoginRequiredToRead` (BL-1863). The new warning text (BL-1866) and the
  `CoapAccessRules` rename (BL-1864) are written as ADR-0132's rule with the task that builds each.
- Documents only: no `.cs` or project file changed, so the `verify` skill was not needed (`docs` pipeline).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The decisions that opened reads with no account configured now point to ADR-0132 amendment 5, so none reads as if removing the last account opens reads
