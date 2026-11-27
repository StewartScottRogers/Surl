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
completed: 2026-11-27
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

- [x] The `SurlSnapshot` summary is true of every store a snapshot covers, including what a restore does to
      the entries that copy no bytes.
- [x] The `StoreKind.Uploads` comment names only registrations made under `StoreKind.Uploads`.
- [x] `dotnet build` is clean.

## Notes

2026-11-27, verified against the code (`Composition` is `Application/Surl.Composition.UnitLibrary`):

- A snapshot covers every registration: `Composition/ComposedStores.cs:81-89` and `788-809`, the
  additional ones at `SurlServerComposition.Start.cs:531-577`, proxy accounts at
  `ComposedStores.AccountStores.cs:159-181`, TLS-SRP at `:136-156` (joined at `Start.cs:620-624`).
- No copy, and restore discards: uploads (`S3UploadStoreRegistration.cs:31-38`,
  `AzureStagedUploadStoreRegistration.cs:33-40`, `GcsStagedUploadStoreRegistration.cs:34-41`,
  `OciUploadStoreRegistration.cs:32-39`), OCI tokens (`OciTokenStoreRegistration.cs:25-27`), MCP
  (`McpSessionStoreRegistration.cs:26-28`). Azure also drops committed block lists
  (`AzureStorageProtocolServer.cs:115-117`).
- Live state dropped: Kafka consumer groups (`KafkaBrokerState.cs:31-48`); MQTT 5 Wills, queued
  deliveries and unacknowledged publishes (`Mqtt5BrokerState.cs:194-252`).
- Account stores report 0 bytes but are copied (`ComposedStores.cs:830-833`). The old "never a clear
  password" was false: a named account keeps the password's UTF-8 bytes
  (`Foundation/Surl.Authentication.UnitLibrary/AccountStore.cs:176-185`, `ChallengeResponseAccount.cs:10`,
  `AwsSigV4Account.cs:9`) and the snapshot holds those accounts (`AccountStore.cs:141`). TLS-SRP holds
  verifiers only (`Cryptography/Surl.TlsSrp.UnitLibrary/TlsSrpAccounts.cs:156-163`).
- `StoreKind.Uploads` registrations are the four upload ones above; OCI tokens and MCP register under
  `StoreKind.Sessions`. With a disk content store the upload entries refuse
  (`S3UploadStoreRegistration.cs:51-57`).
- Verified 2026-11-27: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean,
  fast tests pass. Integration tests not run: only doc comments changed.
- False statements found outside this task's `touches` are filed as BL-1846 (clear-password claim) and
  BL-1847 (`SurlServer` store docs, `StoreKind.Sessions`), BL-1848 (store registration combiner names);
  the Composition comments are already BL-1830.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The SurlSnapshot and StoreKind.Uploads doc comments now name what a snapshot copies and what registers under Uploads
