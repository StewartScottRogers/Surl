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
completed:
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

- [ ] Tests in `Surl.Protocol.Http.UnitTests` prove an accepted and a refused HTTP login each
      write the exact note, with the method name and the user as sent, and `bearer token`
      for Bearer.
- [ ] Tests in `Surl.Protocol.Mqtt.UnitTests` prove the same for an MQTT `CONNECT`.
- [ ] Tests prove no note holds the password, the token or the `Authorization` value.
- [ ] `dotnet build -warnaserror` is clean; the fast tests pass; 100% line and branch
      coverage kept; no method exceeds complexity 10.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
