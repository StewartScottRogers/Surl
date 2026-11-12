# ADR-0130 — How the OCI registry stages uploads and rewrites its index

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-437,
  which built uploads, manifest pushes and deletes.
- **Amends:** [ADR-0115](ADR-0115-how-the-oci-registry-server-answers-oci.md) decisions 2.2
  (when a repository is created) and 2.4 (where an upload session stages its bytes).
- **Details:** ADR-0115 decisions 3.1 and 5.4, on points they leave open.

## Context

ADR-0115 decision 2.4 stages an upload session's bytes in
`<path>/.surl/objects/uploads/oci-<session id>/data`. `ContentStore` refuses every write under
`/.surl` through its public surface (`OpenUploadAsync` answers `NotPermitted` there), and
`Surl.Content` belongs to another task while BL-437 runs, so the server cannot reach that area
without a change to the content store. BL-437 also had to decide what decisions 3.1 and 5.4 leave
open: how many `MANIFEST_BLOB_UNKNOWN` entries a body carries, what a deleted tag leaves behind, and
how `index.json` is written. The oracle is the OCI Distribution and Image specifications v1.1.0
(ADR-0083); the Curl port is never evidence (ADR-0003).

## Decisions

1. **Staging.** An upload session is a `ContentStore` upload session (`OpenUploadAsync`) whose
   target is `<repository>/blobs/oci-upload-<session id>`. Its bytes live in the store's hidden
   `.surl-upload-<guid>` file beside that target, which no protocol lists or serves, so a
   half-finished blob is never visible. The session hashes the bytes with both `sha256` and
   `sha512` as they arrive. On completion the session commits to its target and the file is
   renamed straight to `blobs/<algorithm>/<hex>`. Between the commit and the rename the target
   name exists, but no digest path ever names it. A single `POST` and a mount stage through the
   same path. Cancelling, a refusal, or expiry after one idle hour on the injected `TimeProvider`
   discards the hidden file.
2. **When a repository is created.** A repository is created when an upload session opens or a
   manifest push is checked, not after the first successful upload. The staging file needs the
   repository's `blobs` folder to exist. So a push that fails afterwards (a wrong digest, a
   missing blob) can leave an empty repository: an `oci-layout` and an empty `blobs` folder.
3. **No start-up sweep.** ADR-0115's sweep of `oci-` staging folders at start has nothing to
   sweep: upload sessions live in memory, and their hidden files are the content store's. A hidden
   file left behind by a process that was killed stays until another protocol removes it. This
   case is follow-up work for the content store.
4. **`MANIFEST_BLOB_UNKNOWN`.** The error body lists the code once per missing digest. Each entry
   carries the fixed message and no `detail`, so no client byte is echoed (ADR-0006 section 3).
5. **Deleting a tag** removes that tag's descriptor. If no other descriptor lists the manifest, it
   stays listed untagged, still pulled by digest and still found by the referrers API, as a tag
   moved by a push leaves it (decision 3.1).
6. **`index.json` is rewritten whole** through `ContentStore.WriteUploadAsync`, which writes a
   temporary file and renames it over the old one. One request at a time per server holds the
   index lock while it reads, changes and writes. Each descriptor is written with `mediaType`,
   `digest`, `size`, `artifactType` and `annotations`, followed by every other field the descriptor
   held when it was read (such as `platform`, `urls` or `data`), each written back as the raw JSON
   value it had (BL-873). Only the `org.opencontainers.image.ref.name` annotation changes when a
   tag moves or is deleted.
7. **Upload session UUIDs** come from `Guid.NewGuid`, a version 4 UUID from the operating system's
   cryptographic random source. A test injects its own through the server's internal constructor.

## Consequences

- Every blob upload, manifest push and delete is atomic as seen by FTP, SFTP, SMB and HTTP.
- A failed first push can leave an empty repository folder behind.
- A layout written by another tool loses its descriptors' extra fields on the first push to it.
