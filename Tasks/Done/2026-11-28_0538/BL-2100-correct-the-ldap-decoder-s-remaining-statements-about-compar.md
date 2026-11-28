---
id: BL-2100
title: Correct the LDAP decoder's remaining statements about compare, message IDs and decode results
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2100 — Correct the LDAP decoder's remaining statements about compare, message IDs and decode results

## Goal

The doc comments and `CLAUDE.md` of `Surl.Protocol.Ldap.UnitLibrary` say what the LDAP decoder and server do with a compare, a malformed message's ID and a decode result.

## Context

Found while working BL-2081 (2026-11-28). All paths are in `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary`.

- `LdapUnrecognizedOperation.cs`: the summary lists compare among the operations Surl does not decode, but `LdapMessageDecoder.ReadOtherOperation` decodes `CompareRequest` (application tag 14).
- `LdapDecodeResult.cs`: `MessageId` is documented as kept "so a malformed message can still be answered by its ID", but `LdapSession.AnswerAsync` answers a malformed message with the Notice of Disconnection (`messageID` 0, ADR-0072 decision 3).
- `LdapMessageDecoder.cs`: the comment above `ReadOtherOperation` says "The requests no curl build sends", but that switch decodes `ExtendedRequest`, and `CLAUDE.md` says the OpenLDAP-backed build is proved against `STARTTLS`, an extended request. Check what each pinned build sends (ADR-0072, ADR-0076) before rewording.
- `CLAUDE.md`, the BER codec paragraph: says the decoder decodes into "an `LdapMessage` or an `LdapDecodeOutcome` carrying the message ID"; `LdapMessageDecoder.Decode` returns an `LdapDecodeResult`, which carries the outcome, the message ID and the message.

## Acceptance criteria

- [x] The summary of `LdapUnrecognizedOperation` no longer lists compare as an operation Surl does not decode.
- [x] The `MessageId` documentation of `LdapDecodeResult` no longer says a malformed message is answered by its ID.
- [x] The comment above `ReadOtherOperation` is true of every request in that switch.
- [x] The BER codec paragraph of `CLAUDE.md` names `LdapDecodeResult` as what `Decode` returns.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- What the pinned builds send: the Windows `WinLDAP` build was measured sending bind, search and
  unbind, and no `abandonRequest` (ADR-0072; the tests' `Fixtures/README.md`). The OpenLDAP-backed
  build also sends an extended request, `STARTTLS` (ADR-0076), so "no curl build sends" was false.
- The new comment above `ReadOtherOperation` claims nothing about curl, only how the two switches
  divide the operations: whether the OpenLDAP-backed build ever sends `abandonRequest` is not
  measured by either ADR.
- Verified 2026-11-28: build with 0 warnings, format check clean, all 83 fast test assemblies
  passed (`Surl.Protocol.Ldap.UnitTests` 602 of 602). Comments and prose only, so no
  integration run.
- Other false statements seen in the library's `CLAUDE.md` are filed as BL-2105.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. LDAP decoder comments and CLAUDE.md now say truly how compare, a malformed message's ID and decode results are handled
