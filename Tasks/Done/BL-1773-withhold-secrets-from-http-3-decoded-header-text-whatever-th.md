---
id: BL-1773
title: Withhold secrets from HTTP/3 decoded header text whatever the field order and whatever bytes a value holds
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1762]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitTests, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Documentation/Planning/Decisions/ADR-0169-what-a-quic-or-dtls-exchange-publishes-as-its-bytes.md, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1773 — Withhold secrets from HTTP/3 decoded header text whatever the field order and whatever bytes a value holds

## Goal

No secret field value of an HTTP/3 request is logged in clear in the `Decoded` header text note, even for a malformed request.

## Context

- Found by the code review of BL-1762, read from the code, not recorded on the wire.
- `Http3RequestStreamServer` reports the decoded header list before `Http3RequestHeadMapping.Map` validates it.
- `Http3RequestBytesRedactor.Redact` treats a chunk as header text only when it starts with `:`, so a list whose first
  field is a regular one (`authorization`) is read as HTTP/3 frames and logged as received.
- `Http3DecodedHeaderText` writes a value as it is, so a value holding CR LF puts its rest on a line of its own, which is
  logged as received.
- ADR-0182 amendment 21 lists both as open. ADR-0169 decision 1 gives the text's form.

## Acceptance criteria

- [x] A fast test reports a header list whose first field is `authorization` and shows no byte of its value in the note.
- [x] A fast test gives `authorization` a value holding CR LF and shows no byte after the CR LF in the note.
- [x] An ADR-0182 amendment states the rule, and `dotnet build -warnaserror` and the fast tests are green.

## Notes

- Delivered (ADR-0182, amendment 22): `IDecodedTextReceivedBytesRedactor` lets the engine say a chunk is decoded text,
  so `Http3RequestBytesRedactor` no longer guesses from a leading `:`; `Http3DecodedHeaderText` writes CR and LF inside a
  name or value as `\r` and `\n`.
- Tests: `ServeAsync_Http3DecodedTextWhoseFirstFieldIsAuthorization_NotesNoByteOfItsValue` (Core),
  `RedactDecodedText_AHeaderListWhoseFirstFieldIsAuthorization_ShowsNoByteOfItsValue` and
  `RedactDecodedText_AnAuthorizationValueHoldingALineEnd_ShowsNoByteOfTheValue` (Http3). They failed before the engine
  and renderer changes (3 in Http3, 2 in Core) and pass after.
- Choice: escaping changes the published `Decoded` event text too, for malformed requests only. Alternatives are in the
  amendment.
- `touches` gained `Surl.Protocol.Http.UnitTests` and `Surl.Extended.Protocol.WebTransport.UnitTests`, whose tests passed
  decoded text to `Redact`, and ADR-0169, which now points to the amendment. No task in `Doing` named any of them.
- Pipeline: planned, tested and implemented in the lane's own session (a small change, to save tokens); `code-reviewer`
  reviewed it with no must-fix finding. No conformance stage: no byte Surl sends or accepts changes.
- Filed: BL-1776 (HTTP/3 pseudo-header values are not checked for NUL, CR, LF), BL-1777 (the same for HTTP/2), BL-1778
  (`dedupe` misses an ID an archived task holds).
- This ID is also held by the archived Redis `AUTH` task in `Tasks/Done/2026-11-25_2312`; ADR-0183 amendment 8 cites that
  one. This task was not renumbered, because the shift claimed it under this ID.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. HTTP/3 decoded header text notes withhold secret field values whatever the field order and whatever CR or LF a value holds
