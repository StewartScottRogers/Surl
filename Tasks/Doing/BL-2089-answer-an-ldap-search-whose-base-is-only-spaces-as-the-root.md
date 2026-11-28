---
id: BL-2089
title: Answer an LDAP search whose base is only spaces as the root DSE search it parses to
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2089 — Answer an LDAP search whose base is only spaces as the root DSE search it parses to

## Goal

A search whose base is only spaces is treated one way by both the session and the directory: either as the root DSE search or as a refused or ordinary search, decided and tested.

## Context

- Found while working BL-2078 (2026-11-28), by reading the code; no test or run has shown it.
- `LdapSession.AnswerSearchAsync` tests `BaseObject.Length == 0`, so a spaces-only base needs the bind check and hands the directory `UnusedRootDseFacts`.
- `LdapDirectory.Search` parses it to the root DN (`LdapDistinguishedNameParser.TryParse` skips spaces; `IsRoot`), so it answers the root DSE entry with no `supportedSASLMechanisms` and no `supportedExtension`, and the comment on `UnusedRootDseFacts` ("the directory reads it only for the root DSE") does not hold for it.
- Measure whether a pinned upstream curl build can send such a base (a URL path of `%20`) with `Record-CurlExchange.ps1` (ADR-0003). ADR-0072 decisions 1 and 3 say how the root DSE and the bind check behave; record the choice in an ADR if they do not settle it.

## Acceptance criteria

- [ ] A test in `Surl.Protocol.Ldap.UnitTests` sends a spaces-only base unbound and bound and pins the chosen result and the root DSE attributes, if any.
- [ ] The session and the directory agree on whether that base is the root DSE's, and the `UnusedRootDseFacts` comment is true.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
