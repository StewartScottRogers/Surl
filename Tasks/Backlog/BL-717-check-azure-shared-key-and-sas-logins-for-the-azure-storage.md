---
id: BL-717
title: Check Azure Shared Key and SAS logins for the Azure Storage server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-424, BL-420]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-717 — Check Azure Shared Key and SAS logins for the Azure Storage server

## Goal

The Azure Storage server accepts `Authorization: SharedKey` and `SharedKeyLite` on the Blob and Data Lake
endpoints, and service and account shared access signatures in the query, each checked against the one account
set, and answers every failure with ADR-0109 decision 3's code.

## Context

- Decision: ADR-0109 decision 3 (3.2 Shared Key, 3.3 SAS, 3.4 the refusals). Rules: ADR-0083, ADR-0100 decision
  4.1 (name = storage account name, password = the account key's base64 text).
- Specifications: "Authorize with Shared Key" (the string to sign for the Blob and Data Lake services, the
  canonicalized headers and resource, `SharedKeyLite`), "Create a service SAS" and "Create an account SAS" (the
  string to sign per `sv`, `se`/`st`, `sp`, `spr`, `sip`).
- The methods live in `Surl.Authentication` behind `IHttpAuthenticationSession`, as BL-420's SigV4 does; the
  server reads the refusal reason BL-420 adds to `HttpAuthenticationVerdict` and never references
  `Surl.Authentication` or an account (ADR-0084 decision 4).
- Expected bytes come from the pages' worked examples, never from the Curl port (which does not sign Shared Key).

## Acceptance criteria

- [ ] Named tests show a Shared Key request verified with the page's string to sign, for a Blob and a Data Lake
      request, and a `SharedKeyLite` one.
- [ ] Named tests show a wrong signature, an unknown account and a date more than 15 minutes off each answered
      `403 AuthenticationFailed`, and a header that does not parse `400 InvalidAuthenticationInfo`.
- [ ] Named tests show a service SAS and an account SAS served, an expired one `403 AuthenticationFailed`, a
      permission the `sp` field lacks `403 AuthorizationPermissionMismatch`, and a user delegation SAS (`skoid`)
      `403 AuthenticationFailed`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line
      and branch coverage.

## Notes

- Filed by BL-423 (ADR-0109): BL-424 builds bearer and anonymous only; this adds the HMAC logins.

## Log

- 2026-11-05: Created.
