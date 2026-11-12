---
id: BL-758
title: Serve the Data Lake path options BL-426 left out
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-426, BL-1035]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-05
completed:
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

- [ ] Named tests show: a flush with `retainUncommittedData=true` keeps the bytes staged past it for a later
      flush; Path - List with `beginFrom` starts after that path; `x-ms-properties` on Path - Create is
      stored and comes back on Path - Read; `If-None-Match: *` on Path - Create of an existing file is
      `409 PathAlreadyExists`; a connection past the limit on `abfs` is `503 ServerBusy` in the JSON body.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage.

## Notes

- 2026-11-11 (lane 5): `dotnet` cannot build here: `global.json` pins SDK 10.0.401 with `rollForward: latestFeature`, and the machine has only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100, so every `dotnet` command answers "A compatible .NET SDK was not found". Returned to Backlog behind BL-1035 (Stewart reinstalls the SDK). Lane 6's earlier work is still on branch `factory/BL-758-lane-6-20261111-060243`; start from it.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 6 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-758-lane-6-20261111-060243; start with git cherry-pick --no-commit factory/BL-758-lane-6-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds
- 2026-11-11: Backlog -> Doing.
