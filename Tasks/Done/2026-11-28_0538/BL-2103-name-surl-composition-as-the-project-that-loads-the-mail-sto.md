---
id: BL-2103
title: Name Surl.Composition as the project that loads the mail store and LDAP directory and registers the servers in Requirements.md
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2090]
touches: [Documentation/Product/Requirements.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2103 — Name Surl.Composition as the project that loads the mail store and LDAP directory and registers the servers in Requirements.md

## Goal

Every statement in `Documentation/Product/Requirements.md` about where a server is registered or a store is loaded names the project and type that do it in the code. No behaviour changes.

## Context

Found while working BL-2090 (checked by reading the code on 2026-11-28). All the code below is in `Application/Surl.Composition.UnitLibrary`:

- `ImplicitTlsSchemeServer` (`ImplicitTlsSchemeServer.cs`).
- The server registrations: `ProtocolServerRegistrations.Original` (`ProtocolServerRegistrations.cs`), with the implicit-TLS schemes added through `ProtocolServerRegistration.ImplicitTls`. It holds the `smtps`, `wss`, `ldaps`, `smb`/`smbs` and `rtsp` entries.
- `LoadMailStoreAsync` and `LoadLdapServerAsync`, both on `SurlServerComposition` (`SurlServerComposition.cs`), not on `CommandLineRunner`.

The stale statements, each in a row's "satisfied by" text:

| Row | Says now |
| --- | --- |
| FR-043 | registered for `smtps` through `ImplicitTlsSchemeServer` in `Surl.Console` |
| FR-047 | loaded by `CommandLineRunner.LoadMailStoreAsync` in `Surl.Console` |
| FR-048 | registered for `wss` through `ImplicitTlsSchemeServer` in `Surl.Console` |
| FR-049 | loaded by `CommandLineRunner.LoadLdapServerAsync` and registered for `ldaps` through `ImplicitTlsSchemeServer` in `Surl.Console` |
| FR-050 | `SmbProtocolServer` ... registered in `Surl.Console` |
| FR-051 | `RtspProtocolServer` ... registered in `Surl.Console` |

FR-044 and FR-045 name `ImplicitTlsSchemeServer` with no project and are not wrong. NFR-002, NFR-003 and NFR-006 name `Surl.Console` for coverage and publishing, which is correct. BL-2090 edits the FR-049 row, so start from its landed wording. BL-2101 corrects the same attribution in ADR-0050 and does not touch this file.

Stand-in: nothing to add; documentation only, with no store, event, seed, reset or fault changed.

## Acceptance criteria

- [x] FR-043, FR-048 and FR-049 say their implicit-TLS scheme is registered through `ImplicitTlsSchemeServer` in `Surl.Composition`.
- [x] FR-047 says the mail store is loaded by `SurlServerComposition.LoadMailStoreAsync` in `Surl.Composition`, and FR-049 says the LDAP server and its directory are loaded by `SurlServerComposition.LoadLdapServerAsync` there.
- [x] FR-050 and FR-051 say their servers are registered in `Surl.Composition`.
- [x] A search of `Documentation/Product/Requirements.md` for `CommandLineRunner.Load` finds nothing, and every remaining `Surl.Console` mention is true of the code (any further stale one found by that search is corrected too).
- [x] The diff changes no file but `Documentation/Product/Requirements.md` (and this task's own).

## Notes

- 2026-11-28: Changed the "satisfied by" text of FR-043 and FR-047 to FR-051 only. FR-050 and FR-051 also name `ProtocolServerRegistrations.Original`; each name checked against `Application/Surl.Composition.UnitLibrary`.
- 2026-11-28: A search for `CommandLineRunner` in Requirements.md finds nothing. The remaining `Surl.Console` mentions (NFR-002, NFR-003, NFR-006) name it correctly.
- 2026-11-28: `dotnet build` 0 warnings; fast tests passed.
- 2026-11-28: Other stale statements found and left out of scope are filed as BL-2109. ADRs that place composition in `Surl.Console` were seen in search hits but not checked one by one (BL-2101 covers ADR-0050).

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. Requirements.md names Surl.Composition and its types for server registration and mail store and LDAP loading
