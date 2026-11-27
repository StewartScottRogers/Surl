---
id: BL-1831
title: Correct the store lists in the SurlSnapshot and StoreKind.Uploads doc comments
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Api/SurlSnapshot.cs, Foundation/Surl.Protocol.Abstractions.UnitLibrary/StoreKind.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-1831 — Correct the store lists in the SurlSnapshot and StoreKind.Uploads doc comments

## Goal

The doc comments of `SurlSnapshot` and `StoreKind.Uploads` name what the code actually copies and registers.

## Context

Found while running BL-1822 (checked against the code on 2026-11-26).

- `Application/Surl.Api/SurlSnapshot.cs`: the class summary says a snapshot copies a server's "mail, files,
  messages and accounts". `Surl.Composition` registers more stores than those (the `*StoreRegistration.cs`
  files and `SurlServerComposition.Start.cs`).
- `Foundation/Surl.Protocol.Abstractions.UnitLibrary/StoreKind.cs`: the `Uploads` comment gives "MCP upload
  tokens" as an example. The `StoreKind.Uploads` registrations are S3, Azure, Google Cloud Storage and OCI;
  `McpSessionStoreRegistration.cs` and `OciTokenStoreRegistration.cs` register under `StoreKind.Sessions`.

No behaviour changes: doc comments only.

## Acceptance criteria

- [ ] The `SurlSnapshot` summary is true of every store a snapshot covers, including what a restore does to
      the entries that copy no bytes.
- [ ] The `StoreKind.Uploads` comment names only registrations made under `StoreKind.Uploads`.
- [ ] `dotnet build` is clean.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
