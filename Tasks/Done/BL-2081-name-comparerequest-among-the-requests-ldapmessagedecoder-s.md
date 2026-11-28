---
id: BL-2081
title: Name CompareRequest among the requests LdapMessageDecoder's remarks say it decodes
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2081 — Name CompareRequest among the requests LdapMessageDecoder's remarks say it decodes

## Goal

The XML doc remarks of `LdapMessageDecoder` list every request it decodes, `CompareRequest` included.

## Context

- Found while working BL-2077 (2026-11-28).
- `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapMessageDecoder.cs`: the remarks list `BindRequest`, `UnbindRequest`, `SearchRequest`, `AbandonRequest` and `ExtendedRequest` and say every other operation is an `LdapUnrecognizedOperation`, but `ReadOtherOperation` also decodes `CompareRequest` (application tag 14).

## Acceptance criteria

- [x] The remarks of `LdapMessageDecoder` name `CompareRequest` among the decoded requests.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-11-28: The remarks now name `CompareRequest`. Two neighbouring sentences in the same remarks were
  also untrue and are corrected: `controls` are decoded for every operation, not only the listed
  requests; and `LdapSession` does not answer a malformed message by its `messageID` but with the
  Notice of Disconnection (`messageID` 0, `protocolError`) and closes (ADR-0072 decision 3).
  Documentation only, so no new ADR.
- 2026-11-28: Verified: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes`
  exit 0, fast tests exit 0. The remaining misaligned statements elsewhere in the library are BL-2100.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. LdapMessageDecoder's remarks name CompareRequest and say truly how controls and malformed messages are handled
