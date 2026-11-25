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
completed: 2026-11-25
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

- [x] One public list in `Foundation/Surl.Protocol.Abstractions.UnitLibrary` holds the seven names, and the three
      places above read it.
- [x] A named test fails when a name in the list is not withheld from a SigV4 mismatch's canonical request.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- The list is `HttpWithheldHeaderNames` (`Surl.Protocol.Abstractions`): `Credentials` (2), `ApiKeysAndSessionTokens`
  (5) and `All`, built from the two. Two parts because the HTTP/1.x redactor keeps the scheme word before `****` for
  the credentials only.
- `HttpExchangeRedaction.ApiKeyFieldNames` stays public and returns `ApiKeysAndSessionTokens`, so callers are unchanged.
- The named test is `AwsSigV4AuthenticationMethodTests.Policy_ASignedHeaderTheExchangeLogWithholds_IsWithheldFromTheCanonicalRequest`;
  its rows now come from `HttpWithheldHeaderNames.All`, each as listed and in lower case.
- No ADR: no behaviour change and no new decision.
- The HTTP/2, HTTP/3 and SOCKS tunnel redactors name only `Authorization` and `Proxy-Authorization` themselves
  (outside this task). The HTTP/2 and HTTP/3 ones leave gRPC and WebTransport API keys in clear: filed BL-1713, BL-1714.
- `dotnet format --verify-no-changes` passes on the changed files; on the whole solution it reports line endings in
  `Surl.MailStore.UnitTests/MailboxStoreEventTests.cs`, whose working copy is LF and which this task did not touch.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The seven withheld header names are listed once, in HttpWithheldHeaderNames, and the HTTP/1.x log redactors and the SigV4 mismatch canonical request read that list
