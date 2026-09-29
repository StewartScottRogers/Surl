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
completed:
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

- [ ] A new ADR in `Documentation/Planning/Decisions` states each decision above and why,
      marked "Decided by Claude under Stewart's delegation", and is listed in that folder's
      `README.md`.
- [ ] ADR-0032 section 6's contract block shows `CheckedLogin`, the new verdict parameter and
      `AcceptedUnchecked`, and links the new ADR.
- [ ] `Documentation/Wiki/Glossary.md` defines "checked login" if it lists the other
      authentication terms.

## Notes

## Log

- 2026-09-29: Created.
