---
id: BL-424
title: Create the Azure Storage server and answer Get Blob and Get Blob Properties
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-423, BL-404, BL-407]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-424 — Create the Azure Storage server and answer Get Blob and Get Blob Properties

## Goal

`Surl.Extended.Protocol.AzureStorage.UnitLibrary` exists. It answers Get Blob and Get Blob Properties from the
content store, checks the required `x-ms-*` headers and the bearer login, and refuses anything else with the
Blob service's XML error and `x-ms-error-code`.

## Context

- Decision: BL-423's ADR, decisions 1 to 5. Rules: BL-543's ADR.
- New projects in `Protocols/FileTransfer/`, listed in `Surl.slnx` under `/Protocols/FileTransfer/`. Namespace
  `Surl.Extended.Protocol.AzureStorage`. References: `Surl.Protocol.Abstractions`, `Surl.Content`, and
  BL-407's library.
- The bearer check reaches the accounts through `Surl.Protocol.Abstractions` contracts, never `Surl.Authentication`
  directly.
- Expected bytes come from the REST API pages' response examples: `ETag`, `Last-Modified`, `x-ms-blob-type`,
  `x-ms-request-id`, `x-ms-version`, and the error codes `BlobNotFound`, `ContainerNotFound`,
  `MissingRequiredHeader`, `AuthenticationFailed`, `InvalidAuthenticationInfo`.

## Acceptance criteria

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - Get Blob returns the bytes and headers;
      - Get Blob Properties returns the headers only;
      - a missing blob gets `404 BlobNotFound`;
      - a missing container gets `404 ContainerNotFound`;
      - a missing `x-ms-version` is refused as the ADR says;
      - no bearer token with anonymous off gets the ADR's error;
      - a wrong token gets `AuthenticationFailed` or the ADR's code.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
