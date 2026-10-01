---
id: BL-335
title: Amend ADR-0072 with BL-308's LDAP server decisions
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-308]
touches: [Documentation/Planning/Decisions/ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md]
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-335 — Amend ADR-0072 with BL-308's LDAP server decisions

## Goal

ADR-0072 says what `LdapProtocolServer` (BL-308) does where the ADR left a detail open or
could not be followed as written.

## Context

- BL-308 could not edit `Documentation/Planning/Decisions` (BL-286 held it), so it recorded
  these decisions under its own Notes instead; the code and its XML docs are the source.
- ADR-0059 decision 5 gives a server no reason for a cancellation, so the idle timeout and the
  maximum duration cannot be told apart: both send the Notice of Disconnection `unavailable`
  with the one diagnostic `idle timeout or maximum duration`, not decision 6's two.
- Diagnostics decision 3 left open: an extended operation is `protocolError`
  `unsupported extended operation`; a critical control `unavailableCriticalExtension`
  `critical control not supported`; a Sicily or SASL bind `authMethodNotSupported`
  `only simple binds are answered`; a bind of version 1 or 4 and up `protocolError`
  `only LDAP versions 2 and 3 are answered`; each undecodable message's diagnostic is
  `LdapDiagnostics`'.
- A bind DN no account maps from is checked by the policy under the whole DN as the user name,
  so it fails (49) after the same delay as a wrong password.
- A compare whose rule answers Undefined is `compareFalse`; one of a DN that is not RFC 4514 is
  `invalidDNSyntax` (34).
- A filter nested deeper than `LdapProtocolServer.MaxFilterDepth` (64) is a malformed message.

## Acceptance criteria

- [ ] ADR-0072 decision 6's idle timeout and maximum duration rows name the one diagnostic
      `idle timeout or maximum duration`, citing ADR-0059 decision 5.
- [ ] ADR-0072 decision 3 lists the diagnostics, the unmappable bind DN, the compare answers
      and the filter depth limit above, each matching `Surl.Protocol.Ldap.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
