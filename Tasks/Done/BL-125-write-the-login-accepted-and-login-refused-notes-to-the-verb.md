---
id: BL-125
title: Write the Login accepted and Login refused notes to the verbose log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-114, BL-115]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests, Surl.Protocol.Mqtt.UnitLibrary, Surl.Protocol.Mqtt.UnitTests]
requirement: FR-027
created: 2026-09-29
completed: 2026-09-29
---
# BL-125 — Write the Login accepted and Login refused notes to the verbose log

## Goal

Every checked login writes ADR-0032 section 8's verbose-log note - `Login accepted: <method>
<user>` or `Login refused: <method> <user>` (`<user>` as sent, escaped by ADR-0007 section 8,
`bearer token` for Bearer) - and no note ever holds a password, a token or an `Authorization`
value.

## Context

ADR-0032 section 8's last bullet. BL-110 built `Surl.Authentication`'s policy, but the
section 6 contract (`IAuthenticationPolicy`, `HttpAuthenticationVerdict`,
`PasswordLoginVerdict`) carries neither the method nor the user name as sent, and the policy
has no `IExchangeLog`, so no task writes these notes yet. Either the verdicts gain what the
note needs (method, user as sent) and each server writes the note through its exchange log,
or the contract passes the exchange log to the policy; decide by ADR-0002 (no protocol server
references `Surl.Authentication`) and record the choice in an ADR if the contract changes.
Found while working BL-110 (2026-09-29).

## Acceptance criteria

- [x] Tests in `Surl.Protocol.Http.UnitTests` prove an accepted and a refused HTTP login each
      write the exact note, with the method name and the user as sent, and `bearer token`
      for Bearer.
- [x] Tests in `Surl.Protocol.Mqtt.UnitTests` prove the same for an MQTT `CONNECT`.
- [x] Tests prove no note holds the password, the token or the `Authorization` value.
- [x] `dotnet build -warnaserror` is clean; the fast tests pass; 100% line and branch
      coverage kept; no method exceeds complexity 10.

## Notes

- **Decision (contract):** the verdict carries what the note needs and each server writes
  the note through its own exchange log; the policy never gets an `IExchangeLog`. That keeps
  every note written by the server, as all others are, and lets the HTTP and MQTT tests prove
  the note with their own policy doubles, with no reference to `Surl.Authentication`
  (ADR-0002). New in Abstractions: `CheckedLogin(Method, User, IsAccepted)` with `Note` and
  `BearerTokenUser`; `HttpAuthenticationVerdict` gains an optional fourth parameter
  `CheckedLogin`; `PasswordLoginVerdict.AcceptedUnchecked` (last, so nothing renumbers).
- **Decision (method names):** HTTP uses the `Authorization` scheme spelling
  (`AuthenticationMethods.AuthorizationSchemeOf`: `Basic`, `Digest`, `Bearer`, `NTLM`,
  `Negotiate`, `AWS4-HMAC-SHA256`); a password login uses the listen URL's scheme, which
  `PasswordLogin.Scheme` already carries "for the log" (`Login accepted: mqtt tester`).
- **Decision (what is noted):** only logins whose credentials were checked: HTTP accepted and
  refused credentials; MQTT `Accepted` and `RefusedCredentials`. No note for no credentials,
  a plain-text secret refused unchecked, a login with no user name, a handshake's
  continuation step, or `--allow-anonymous` (hence `AcceptedUnchecked`: with a plain
  `Accepted`, MQTT would have noted logins nobody checked, unlike HTTP).
- **Decision (unreadable user):** when no user can be read (Basic not base64, no `:`, a
  user-id that is not UTF-8; a malformed Digest answer) the user is left out:
  `Login refused: Basic`. Digest's user is its `username` parameter as sent.
- MQTT's verdict switch now fails closed on a verdict value it does not know (CONNACK 5),
  pinned by a test; the jump table's out-of-range arm needed it for 100% branches.
- **ADR deferred to BL-129:** `Documentation/Planning/Decisions` is in BL-128's `touches`
  (lane 1, in Doing), so the ADR this Context asks for is filed as BL-129 (depends on this
  task) rather than written here, keeping this task inside its own `touches`.
- No upstream curl measurement: the notes go to surl's own stderr log, not the wire, so no
  byte curl sends or receives changed. Gates: `dotnet build -warnaserror` clean, fast tests
  green, Measure-CodeQuality 100/100 with 0 failing members for all four libraries.
  `dotnet format --verify-no-changes` still flags `Surl.Cli.UnitLibrary/SchemeDefaultPorts.cs`
  line endings, a file this task does not touch.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Checked HTTP and MQTT logins write 'Login accepted/refused: <method> <user>' to the verbose log, never a secret
