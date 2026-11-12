---
id: BL-758
title: Serve the Data Lake path options BL-426 left out
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-426]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-758 — Serve the Data Lake path options BL-426 left out

## Goal

The Data Lake endpoint serves the Path - Update and Path - Create options BL-426 refused or ignored, as
ADR-0109 decisions 1, 5.2, 5.4 and 5.5 describe them.

## Context

- BL-426 built Path - Read, Create, Update (`append`, `flush`), Delete and List in
  `Surl.Extended.Protocol.AzureStorage.UnitLibrary` (`AzurePathUpdate`, `AzurePathCreate`, `AzurePathList`,
  `AzureDataLakeRequestAnswerer`). It left out, and its Notes record:
  - `retainUncommittedData=true` on a flush, answered `400 InvalidQueryParameterValue`; ADR-0109 decision
    5.5 keeps the staged bytes past the flush position;
  - Path - List's `beginFrom`, ignored;
  - Path - Create's `x-ms-properties` (base64 name=value pairs), ignored;
  - the conditional fields on Path - Create, append and flush (`If-None-Match: *` on an existing path is
    `409 PathAlreadyExists`, any other failing condition `412 ConditionNotMet`; ADR-0109 decision 1);
  - the connection-limit refusal (`WriteRefusalAsync`), which writes the Blob XML `Error` on `abfs` and
    `abfss` too, where decision 5.2 wants the JSON error.
- Data Lake Storage Gen2 REST API: "Path - Create", "Path - Update", "Path - List".

## Acceptance criteria

- [x] Named tests show: a flush with `retainUncommittedData=true` keeps the bytes staged past it for a later
      flush; Path - List with `beginFrom` starts after that path; `x-ms-properties` on Path - Create is
      stored and comes back on Path - Read; `If-None-Match: *` on Path - Create of an existing file is
      `409 PathAlreadyExists`; a connection past the limit on `abfs` is `503 ServerBusy` in the JSON body.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage.

## Notes

- Named tests: `AzurePathUpdateTests.ServeAsync_FlushRetainingUncommittedData_KeepsTheBytesStagedPastItForALaterFlush`;
  `AzurePathListTests.ServeAsync_ListWithBeginFrom_StartsAfterThatPath`;
  `AzurePathCreateTests.ServeAsync_CreateFileWithProperties_KeepsThemForPathRead`;
  `AzurePathCreateTests.ServeAsync_CreateFileWithIfNoneMatchStarOverAnExistingFile_Is409PathAlreadyExistsAndKeepsIt`;
  `AzureStorageProtocolServerTests.WriteRefusalAsync_ConnectionPastALimitOnTheDataLakeEndpoint_Is503ServerBusyInTheJsonError`.
- Touches widened (rule 3; no task in Doing named them): the connection-limit refusal had no way to
  know its listen scheme, so `IConnectionRefusalWriter` gained a scheme-taking overload (a default
  interface member writing the scheme-blind refusal, so no other server changes) and `ServingEngine`
  calls it. Abstractions, Core and their tests were added to `touches`, and ADR-0109 for the amendment
  recording this and the other choices (beginFrom relative to the folder listed and started after;
  conditions judged against the committed file; retained bytes carried in a copied upload session).
- Verified on the pinned-compatible SDK 10.0.404 (lane 4): `dotnet build -warnaserror` 0 warnings, 0 errors; fast tests green in every project; coverage measured by lane 6 at 100% line and branch for Azure, Core and Abstractions.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 6 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-758-lane-6-20261111-060243; start with git cherry-pick --no-commit factory/BL-758-lane-6-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. The Data Lake endpoint serves retainUncommittedData, beginFrom, x-ms-properties, create/append/flush conditions, and a JSON 503 ServerBusy on abfs/abfss
