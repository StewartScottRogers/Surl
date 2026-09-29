---
id: BL-129
title: Record the CheckedLogin contract change in an ADR
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-125]
touches: [Documentation/Planning/Decisions, Documentation/Wiki/Glossary.md]
requirement: FR-027
created: 2026-09-29
completed: 2026-09-29
---
# BL-129 — Record the CheckedLogin contract change in an ADR

## Goal

An ADR, marked "Decided by Claude under Stewart's delegation", records BL-125's change to
ADR-0032 section 6's authentication contract, and ADR-0032 section 6 points to it.

## Context

BL-125 wrote ADR-0032 section 8's `Login accepted` / `Login refused` verbose-log notes. Its
Context asked for an ADR if the contract changed, and it did, but
`Documentation/Planning/Decisions` was held by BL-128 on another dark factory lane, so the
code landed first and the ADR was filed here. BL-125's task file (Notes) holds the decisions
to record:

- `CheckedLogin(string Method, string? User, bool IsAccepted)` added to
  `Surl.Protocol.Abstractions`, with `Note` building the text, and `BearerTokenUser`.
- `HttpAuthenticationVerdict` gains a fourth, optional `CheckedLogin? CheckedLogin`; the HTTP
  server writes its note. The policy does not get the `IExchangeLog`: the note is written by
  the server, as every other note is, and protocol tests can prove it without referencing
  `Surl.Authentication` (ADR-0002).
- `PasswordLoginVerdict.AcceptedUnchecked` (declared last, so no value is renumbered) is the
  `--allow-anonymous` answer; a password-login server notes only `Accepted` and
  `RefusedCredentials`, with the listen URL's scheme as the method (`Login accepted: mqtt tester`).
- Which cases write no note: no credentials, a plain-text secret refused unchecked, a login
  with no user name, a handshake's continuation step, and `--allow-anonymous`.
- A user that cannot be read (Basic not base64, no `:`, a user-id that is not UTF-8; a
  malformed Digest answer) is left out: `Login refused: Basic`.

## Acceptance criteria

- [x] A new ADR in `Documentation/Planning/Decisions` states each decision above and why,
      marked "Decided by Claude under Stewart's delegation", and is listed in that folder's
      `README.md`.
- [x] ADR-0032 section 6's contract block shows `CheckedLogin`, the new verdict parameter and
      `AcceptedUnchecked`, and links the new ADR.
- [x] `Documentation/Wiki/Glossary.md` defines "checked login" if it lists the other
      authentication terms.

## Notes

- ADR-0038 (`ADR-0038-checked-logins-carry-the-login-note-and-the-server-writes-it.md`)
  records BL-125's decisions, each checked against the code: `CheckedLogin`, the optional
  fourth `HttpAuthenticationVerdict` parameter, `PasswordLoginVerdict.AcceptedUnchecked`, the
  method names, which logins are noted and the unreadable user. Listed in the Decisions
  `README.md`, where ADR-0032's status now says section 6 is amended by ADR-0038.
- ADR-0032: header "Amended" line; section 6's contract block shows the three additions as
  declared in code, with a bullet linking ADR-0038; its `AnonymousAuthenticationPolicy`
  bullet now says `AcceptedUnchecked` (the code's answer), no longer `Accepted`; section 8's
  last bullet links ADR-0038.
- The glossary listed no authentication terms, but `CheckedLogin` is a Surl-specific term in
  code, so a new "Authentication" section defines "checked login".
- One detail beyond BL-125's Notes, recorded in ADR-0038 decision 5: an HTTP continuation
  step with no value to send is answered as a refusal, delayed, and noted `Login refused`.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0038 records the CheckedLogin contract change; ADR-0032 section 6 shows it and links ADR-0038; glossary defines checked login
