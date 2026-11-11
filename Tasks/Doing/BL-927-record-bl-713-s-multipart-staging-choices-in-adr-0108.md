---
id: BL-927
title: Record BL-713's multipart staging choices in ADR-0108
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-713]
touches: [Documentation/Planning/Decisions/ADR-0108-how-the-s3-server-answers-s3.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-927 — Record BL-713's multipart staging choices in ADR-0108

## Goal

ADR-0108 says where and how the S3 server stages multipart uploads, as BL-713 built it, marked "Decided by
Claude under Stewart's delegation".

## Context

- ADR-0108 decision 3 ("Multipart staging") says parts are staged under ADR-0100's `.surl/objects/uploads/<upload
  id>/`. `ContentStore` refuses every write under `/.surl`, and BL-713 touched only the S3 library, so - as
  BL-715 did for Azure blocks (BL-922) - each upload's parts sit one after another in a `ContentUploadSession`
  opened at the key at CreateMultipartUpload and never committed: a hidden `.surl-upload-<guid>` dot-file beside
  the object. CompleteMultipartUpload copies the named parts into a fresh session and commits it.
- The other choices BL-713 made, all in
  `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary/CLAUDE.md` and the BL-713 task's Notes: the key's
  folders are created at CreateMultipartUpload; the staged bytes of one upload together are held to
  `--max-filesize` (past it the upload is aborted, `400 EntityTooLarge`); a part uploaded again replaces the old
  one; the multipart requests run one at a time per server, and each first aborts uploads idle more than 24
  hours; an `uploadId` of another bucket or key is `NoSuchUpload`; a refused completion leaves the upload open;
  `Checksum*` elements in a `Part` are skipped; the completion's order of checks (body and signature,
  `NoSuchUpload`, `MalformedXML`, `InvalidPartOrder`, `InvalidPart`, `EntityTooSmall`); `Location` is
  `http(s)://<Host or listen host><path>`; an object query other than the four (ListParts too) is `501`.

## Acceptance criteria

- [ ] ADR-0108 decision 3 states where multipart parts are staged, and no longer says `.surl/objects/uploads/`
      for S3.
- [ ] ADR-0108 records each of the other choices listed in Context, with its reason.

## Notes

Filed by BL-713.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 3 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-927-lane-3-20261111-060243; start with git cherry-pick --no-commit factory/BL-927-lane-3-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
