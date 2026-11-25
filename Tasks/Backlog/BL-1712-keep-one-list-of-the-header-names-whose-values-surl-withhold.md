---
id: BL-1712
title: Keep one list of the header names whose values surl withholds from logs and SigV4 mismatches
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1701]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Protocols/Http/Surl.HttpMessage.UnitLibrary, Protocols/Http/Surl.HttpServing.UnitLibrary]
requirement: none
created: 2026-11-25
completed:
---
# BL-1712 — Keep one list of the header names whose values surl withholds from logs and SigV4 mismatches

## Goal

The header names ADR-0182 withholds are listed once, in `Surl.Protocol.Abstractions`, and the HTTP log redactors and
the SigV4 canonical request both read that list.

## Context

- Found in BL-1701's review. `AwsSigV4CanonicalRequest.WithheldHeaderNames`
  (`Foundation/Surl.Authentication.UnitLibrary`) copies `HttpRequestHeadLineRule.CredentialFieldNames`
  (`Protocols/Http/Surl.HttpMessage.UnitLibrary`) and `HttpExchangeRedaction.ApiKeyFieldNames`
  (`Protocols/Http/Surl.HttpServing.UnitLibrary`).
- A name added to the log redactor alone would still appear in clear in `AwsSigV4SignatureMismatch.CanonicalRequest`,
  and no test would fail.
- No behaviour change: the same seven names, matched without regard to case.

## Acceptance criteria

- [ ] One public list in `Foundation/Surl.Protocol.Abstractions.UnitLibrary` holds the seven names, and the three
      places above read it.
- [ ] A named test fails when a name in the list is not withheld from a SigV4 mismatch's canonical request.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-25: Created.
