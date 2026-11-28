---
id: BL-2089
title: Answer an LDAP search whose base is only spaces as the root DSE search it parses to
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests, Documentation/Planning/Decisions/ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
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

- [x] A test in `Surl.Protocol.Ldap.UnitTests` sends a spaces-only base unbound and bound and pins the chosen result and the root DSE attributes, if any.
- [x] The session and the directory agree on whether that base is the root DSE's, and the `UnusedRootDseFacts` comment is true.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured 2026-11-28 with the pinned Windows curl 8.21.0 (`Record-CurlExchange.ps1 -Ldap`): a URL path of `%20` sends base `" "`, `%20%20%20` sends three spaces; `WinLDAP` trims neither.
- Chosen: a spaces-only base is the root DSE's. No bind, the connection's root DSE, success. Why: it parses to the empty DN (ADR-0072 decision 1 compares normal forms), and it shows nothing `""` does not already show an unbound peer. Recorded as ADR-0072 Amendment 3, decided by Claude under Stewart's delegation, since decisions 1 and 3 only name `""`.
- `LdapDistinguishedName.NamesTheRootDse` answers it without parsing, so an unbound peer's other bases are still not parsed before the bind check. `UnusedRootDseFacts` and its comment are unchanged: the comment is true now.
- Added the ADR-0072 file to `touches` for the amendment; no task in Doing names it.
- Tests: `ServeAsync_SearchOfABaseOfOnlySpaces_AnswersTheRootDseUnboundAndBound` (one and three spaces; failed before the fix), `ServeAsync_CurlsSearchOfThePathPercent20_AnswersTheRootDseWithNoBind` (recorded curl bytes), `NamesTheRootDse_IsTrueExactlyForTheTextsThatParseToTheEmptyDn`. Build clean, format check clean, fast tests all pass (LDAP 602).

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. An LDAP search whose base is only spaces is the root DSE search: no bind needed, answered with the connection's root DSE (ADR-0072 Amendment 3)
