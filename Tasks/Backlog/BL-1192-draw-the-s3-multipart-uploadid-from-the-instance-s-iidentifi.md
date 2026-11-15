---
id: BL-1192
title: Draw the S3 multipart UploadId from the instance's IIdentifierRandom so --random-seed repeats it
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1156]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-064
created: 2026-11-15
completed:
---
# BL-1192 — Draw the S3 multipart UploadId from the instance's IIdentifierRandom so --random-seed repeats it

## Goal

The S3 multipart UploadId comes from the instance's IIdentifierRandom, so two instances with the same RandomSeed hand out the same UploadId for the same exchange order.

## Context

ADR-0163 classes the S3 UploadId as a seedable identifier under ADR-0124 decision 6. It is drawn today by `RandomNumberGenerator.GetHexString(32, lowercase: true)` in `S3CreateMultipartUpload.cs`; `S3RequestId.Draw(context.Identifiers)` already shows how the S3 server reaches the source.

## Acceptance criteria

- [ ] S3CreateMultipartUpload draws the 32 lowercase hex characters from `ExchangeContext.Identifiers`; no `RandomNumberGenerator` call remains for it.
- [ ] A test shows two instances with the same RandomSeed give the same UploadId for the same exchange order, and different seeds give different ones.
- [ ] An unseeded instance still gives a fresh UploadId each time.
- [ ] dotnet build is clean and the fast tests pass.

## Notes

## Log

- 2026-11-15: Created.
