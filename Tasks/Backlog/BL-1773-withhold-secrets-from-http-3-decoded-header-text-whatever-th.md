---
id: BL-1773
title: Withhold secrets from HTTP/3 decoded header text whatever the field order and whatever bytes a value holds
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1762]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
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

- [ ] A fast test reports a header list whose first field is `authorization` and shows no byte of its value in the note.
- [ ] A fast test gives `authorization` a value holding CR LF and shows no byte after the CR LF in the note.
- [ ] An ADR-0182 amendment states the rule, and `dotnet build -warnaserror` and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
