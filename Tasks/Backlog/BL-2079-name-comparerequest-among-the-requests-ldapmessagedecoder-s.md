---
id: BL-2079
title: Name CompareRequest among the requests LdapMessageDecoder's remarks say it decodes
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary]
requirement: none
created: 2026-11-28
completed:
---
# BL-2079 — Name CompareRequest among the requests LdapMessageDecoder's remarks say it decodes

## Goal

The XML doc remarks of `LdapMessageDecoder` list every request it decodes, `CompareRequest` included.

## Context

- Found while working BL-2077 (2026-11-28).
- `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapMessageDecoder.cs`: the remarks list `BindRequest`, `UnbindRequest`, `SearchRequest`, `AbandonRequest` and `ExtendedRequest` and say every other operation is an `LdapUnrecognizedOperation`, but `ReadOtherOperation` also decodes `CompareRequest` (application tag 14).

## Acceptance criteria

- [ ] The remarks of `LdapMessageDecoder` name `CompareRequest` among the decoded requests.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-11-28: Created.
