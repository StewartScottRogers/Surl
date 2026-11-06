---
id: BL-669
title: Run the Curl port's s3, az and gs clients against files uploaded to surl over FTP
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-638, BL-421, BL-1023, BL-1026]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-669 — Run the Curl port's s3, az and gs clients against files uploaded to surl over FTP

## Goal

`Integration` tests show the Curl port's pinned `s3`, `az` and `gs` clients reading, from one `surl`, files that
pinned upstream curl uploaded over `ftp`, and upstream curl reading over `http` a file the port's clients wrote -
the shared content store seen from both families of client.

## Context

- Decisions: BL-622's ADR decision 5 (planning class `CurlPortClientReadsFilesSharedAcrossSurlProtocolsTests` in
  `Conformance/Surl.Conformance.UnitTests`), ADR-0083 decisions 1 to 3: the port's client is pinned in
  `CurlPortBuilds.json`, located by its own locator, a live client in `Integration` tests only and **never**
  evidence. What each end must show comes from the S3, Azure Blob and Cloud Storage specifications as their ADRs
  (BL-417, BL-423, BL-429) cite them; upstream curl's end from ADR-0003.
- The servers are registered by BL-421, BL-427 and BL-433. Model classes: the existing `CurlPortClient…Tests`
  (BL-422, BL-428, BL-434 add theirs) and `UpstreamCurlUploadsToSurlOverFtpTests`.
- A missing pinned port or upstream build marks the test Inconclusive.

## Acceptance criteria

- [ ] The class exists, every test in it is `[TestCategory("Integration")]`, and each named test asserts the
      fetched bytes equal the uploaded bytes for: `ftp` upload then `s3` GetObject; `ftp` upload then `az` Get
      Blob; `ftp` upload then `gs` GET Object; `s3` PutObject then `http` GET.
- [ ] No assertion's expected value comes from the port's output.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-427 replaced by BL-1023 (BL-427 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-06: depends-on BL-433 replaced by BL-1026 (BL-433 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
