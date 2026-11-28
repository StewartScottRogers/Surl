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
completed:
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

- [ ] The summary of `LdapUnrecognizedOperation` no longer lists compare as an operation Surl does not decode.
- [ ] The `MessageId` documentation of `LdapDecodeResult` no longer says a malformed message is answered by its ID.
- [ ] The comment above `ReadOtherOperation` is true of every request in that switch.
- [ ] The BER codec paragraph of `CLAUDE.md` names `LdapDecodeResult` as what `Decode` returns.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-11-28: Created.
