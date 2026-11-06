---
id: BL-716
title: Answer Azure List Containers and the Data Lake rename, properties and filesystem requests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-426]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests, Documentation/Planning/Decisions/ADR-0109-how-the-azure-storage-server-answers-az-abfs-and-abfss.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-716 — Answer Azure List Containers and the Data Lake rename, properties and filesystem requests

## Goal

The Azure Storage server answers List Containers and Get Container Properties on the Blob endpoint, and Path -
Get Properties (`HEAD`), Path - Create with `x-ms-rename-source`, Path - Update `action=setProperties`,
Filesystem - Get Properties and Filesystem - List on the Data Lake endpoint, as ADR-0109 decision 1 lists them.

## Context

- Decision: ADR-0109 decisions 1, 2 and 5. Rules: ADR-0100 decision 2.1 (containers and filesystems are the
  top-level folders, plus `--bucket`), ADR-0102 decision 2.3 (a share the caller may not read is missing).
- Specifications: "List Containers", "Get Container Properties", Data Lake "Path - Get Properties", "Path -
  Create" (rename), "Path - Update" (setProperties, `x-ms-properties`), "Filesystem - Get Properties",
  "Filesystem - List".
- Hadoop's ABFS driver renames and reads properties on every write; the port's client does neither.

## Acceptance criteria

- [x] Named tests show List Containers' `EnumerationResults` written from the page's example, listing only
      the containers the caller may read, with `prefix`, `maxresults` and `marker`.
- [x] Named tests show Path - Get Properties of a file and a directory (`x-ms-resource-type`), a rename of a
      file and of a directory through the content store, and a rename whose source is missing `404
      SourcePathNotFound`.
- [x] Named tests show `setProperties` stored in the object metadata index and returned in `x-ms-properties`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-423 (ADR-0109).
- Touches widened to ADR-0109 to record the choices as its decision 9 ("Decided by Claude under
  Stewart's delegation"); no task in Doing names that file.
- Plan as built: AzureTopLevelFolders names the containers/filesystems the caller may read (root
  folders, shares, --buckets; hidden shares and top-level files left out) for both
  AzureListContainers (XML, prefix/marker/maxresults via AzureListRequest) and
  AzureFilesystemList (JSON, prefix/maxResults/continuation). AzureFolderProperties
  answers Get Container Properties, Filesystem - Get Properties and a directory's Path - Get
  Properties. AzurePathRename renames through ContentStore.RenameEntry (or
  RenameEntryWithoutReplacing for If-None-Match: *) and re-stores a file's index entry under
  its new name. AzurePathSetProperties + AzurePropertiesField map x-ms-properties onto the
  file's user metadata (the same entries the Blob endpoint sends as x-ms-meta-*).
- Defaults taken: listings need --list-directories, as List Blobs does; a folder's ETag is
  "0x<ticks>", as Path - Create already gives a directory; setProperties on a directory is
  409 ResourceTypeMismatch because the index keys only files; a renamed directory's files fall back
  to the index defaults (the index keys by path). A HEAD naming a listing is judged as a SAS read
  (), not a listing (l).
- Tests: AzureListContainersTests, AzureFilesystemListTests, AzurePathRenameTests,
  AzurePathSetPropertiesTests (named in the criteria: ServeAsync_ListContainers_AnswersThePagesEnumerationResults,
  ServeAsync_ListContainersWithPrefixMaxResultsAndMarker_PagesThroughTheNamesThatMatch,
  ServeAsync_ListContainers_ListsOnlyTheContainersTheCallerMayRead,
  ServeAsync_GetPropertiesOfADirectory_AnswersItsResourceType,
  ServeAsync_GetPropertiesOfAFile_AnswersItsResourceTypeWithoutProperties,
  ServeAsync_RenameAFile_MovesItThroughTheContentStoreAndKeepsItsHeaderFields,
  ServeAsync_RenameADirectory_MovesItWithEverythingInIt,
  ServeAsync_RenameWhoseSourceIsMissing_Is404SourcePathNotFound,
  ServeAsync_SetProperties_StoresThemInTheIndexAndGetPropertiesReturnsThem). Azure tests 433 green;
  library coverage measured with the MSTest coverage collector: line 100%, branch 100%.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Azure List Containers, Get Container Properties, Filesystem - List/Get Properties, Path - Get Properties, rename and setProperties answered
