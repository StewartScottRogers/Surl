---
id: BL-437
title: Answer OCI blob uploads, manifest pushes and deletes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-436, BL-636, BL-656]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-437 — Answer OCI blob uploads, manifest pushes and deletes

## Goal

The OCI registry accepts blob uploads in every way the spec defines: POST then PUT, a single POST with
`?digest=`, chunked PATCH with `Content-Range`, and cross-repository mount. It accepts manifest pushes by tag
and digest (with the `OCI-Subject` header for a manifest with a subject), and deletes if BL-435's ADR
serves them. It checks every digest and refuses mismatches with `DIGEST_INVALID`.

## Context

- Decision: BL-435's ADR, decisions 2, 3 and 7. Pushes only under `--allow-uploads`.
- Distribution spec v1.1.0, "Push": `202 Accepted` with `Location` for an upload session, `Range: 0-<n>` on
  PATCH, `416` for an out-of-order chunk, `201 Created` with `Location` and `Docker-Content-Digest` on completion,
  and `MANIFEST_BLOB_UNKNOWN` for a manifest naming a missing blob.
- Upload sessions expire after the ADR's lifetime, on an injected `TimeProvider`.
- Shared stores (Stewart, 2026-11-04; BL-622's ADR decision 2, which BL-435's ADR applies): blobs, manifests and
  tags live in the repository's folder of the one shared `ContentStore`, laid out as BL-622's ADR decides
  (planning: the OCI Image Layout), with media types in BL-636's object metadata - not in a private `.surl/` area.
  Every store call passes the checked account (or anonymous) and scheme `oci` to BL-656's share access check.

## Acceptance criteria

- [x] Named tests show:
      - each of the four upload ways completing, with the stored blob readable by digest;
      - a chunk out of order gets `416`;
      - a wrong digest gets `DIGEST_INVALID`;
      - a manifest naming a missing blob gets `MANIFEST_BLOB_UNKNOWN`;
      - a manifest with a subject is listed by the referrers API and answered with `OCI-Subject`;
      - an expired upload session gets `BLOB_UPLOAD_UNKNOWN`;
      - a push refused when uploads are off;
      - a pushed blob present as a file at the layout path in the content store;
      - a push refused by share access answered as the ADR says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Built in `Surl.Extended.Protocol.OciRegistry`: `OciWriteAnswerer` sends every write (upload
  requests, `PUT` and `DELETE`) to `OciBlobUploads` (end-4a, end-4b, end-5, end-6, end-11, end-13,
  session cancel), `OciManifestPush` (end-7) or `OciDeletes` (end-9, end-10). Writes need the
  store's `AllowUploads` (`403 DENIED`), and the store's share access refuses the rest (`403
  DENIED`; a share the caller may not see is `404 NAME_UNKNOWN`). Until BL-438 adds tokens, every
  request is the anonymous caller.
- Tests, each named for what it shows: the four upload ways in `OciBlobUploadsTests`
  (`AnswerSessionAsync_PostThenPut_…`, `OpenAsync_SinglePostWithDigest_…`,
  `AnswerSessionAsync_ChunksInOrder_…`, `OpenAsync_MountFromARepositoryHoldingTheBlob_…`);
  `…ChunkOutOfOrder_Returns416…`; `…WrongDigest_Returns400DigestInvalid…`;
  `OciManifestPushTests.AnswerAsync_ManifestNamingMissingBlobs_…`;
  `…ManifestWithASubject_AnsweredWithOciSubjectAndListedByTheReferrersApi`;
  `…SessionUntouchedForAnHour_Returns404BlobUploadUnknown`; `…UploadsOff_Returns403Denied`;
  `OpenAsync_PushedBlob_IsAFileAtTheLayoutPathInTheContentStore`; and in
  `OciRepositoryCreationTests`, the read-only share, read-only grant and invisible share cases.
  The library's coverage, measured with the MSTest coverage collector: 100% line, 100% branch.
- Decision (ADR-0127, amends ADR-0115 2.2 and 2.4): ADR-0115 staged uploads under `.surl/objects/uploads/`,
  but `ContentStore` refuses writes under `/.surl`, and `Surl.Content` belongs to BL-789 while it is
  in Doing. So a session is a `ContentStore` upload session targeting
  `<repository>/blobs/oci-upload-<id>`, committed and then renamed to `blobs/<algorithm>/<hex>`.
  The repository is created when a session opens or a manifest push is checked. The same ADR
  covers `MANIFEST_BLOB_UNKNOWN` once per missing digest, a deleted tag leaving its manifest
  untagged, and how `index.json` is rewritten.
- Touches: added `Documentation/Planning/Decisions` for ADR-0127 and its README row; no Doing task
  names it.
- Defaults taken: upload session UUIDs come from `Guid.NewGuid`, or from a source a test passes to
  the server's internal constructor. A single server-wide lock covers every `index.json` rewrite,
  which is simple and enough for a stand-in. Content-Range is read in Dist's `<start>-<end>` form
  only.
- Follow-ups filed: BL-868 (keep the extra descriptor fields such as `platform` when `index.json`
  is rewritten) and BL-869 (remove `.surl-upload-` files a killed process left behind).

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-636 and BL-656; repositories live in the shared content store.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The oci registry accepts blob uploads (session, chunks, single POST, mount), manifest pushes by tag or digest with OCI-Subject, and deletes, checking every digest
