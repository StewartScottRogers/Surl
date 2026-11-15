# ADR-0163 — The S3 UploadId and the OCI upload session UUID follow `--random-seed`; the GCS upload ID stays secret

- **Status:** Accepted
- **Date:** 2026-11-15
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-15, in BL-1156.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 6,
  [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md).
- **Amends:** [ADR-0130](ADR-0130-how-the-oci-registry-stages-uploads-and-rewrites-its-index.md) decision 7.
- **Acted on by:** BL-1192, BL-1193.

## Context

ADR-0124 decision 6 lets a generated identifier that grants nothing follow the instance's `IIdentifierRandom`
(`--random-seed`), and keeps anything that grants access on `RandomNumberGenerator`. Three upload
identifiers are still drawn unseeded: the S3 multipart `UploadId` (`S3CreateMultipartUpload.cs`), the OCI
upload session UUID (`Guid.NewGuid`, `OciRegistryProtocolServer.cs`) and the GCS resumable upload ID
(`GcsUploadStaging.cs`). The test for each is whether holding the ID, without the service's login, lets a
peer do anything.

## Decision

### 1. The S3 `UploadId` is seedable

Every S3 request, `UploadPart`, `CompleteMultipartUpload` and `AbortMultipartUpload` included, is judged by
the SigV4 login before the upload is looked up (`S3UploadPart.AnswerAsync` runs after `S3LoginResult`), and
`S3MultipartUploads.Find` also requires the ID to belong to the request's bucket and key. Amazon's
documentation calls the upload ID an opaque handle, not a credential. Knowing it grants nothing, so it
draws its 32 lowercase hex characters from `ExchangeContext.Identifiers`. Acted on by BL-1192.

### 2. The OCI upload session UUID is seedable

Every `/v2/` request, the upload requests included, needs a Bearer token from `OciTokenBook`
(`OciRegistryProtocolServer`), and the OCI Distribution Specification v1.1.0 names the session `<reference>`
an opaque string the registry chooses, not a secret. The UUID grants nothing without the token, so it is
drawn from `IIdentifierRandom`, as a version 4 UUID in form (version and variant bits set). The internal
constructor taking a `Func<Guid>` stays for tests. This reverses ADR-0130 decision 7, which chose
`Guid.NewGuid`. Acted on by BL-1193.

### 3. The GCS resumable upload ID stays on `RandomNumberGenerator`

Google's resumable-upload documentation says the session URI is itself the authentication for the rest of
the upload: a client continues the upload by `PUT` to that URI without presenting credentials again, and the
documentation warns that anyone holding the URI can upload to it. In Surl the ID is the only thing that
names the staged upload (`GcsStagedUploads.Find` checks the ID, kind and bucket, nothing else), so it grants
the right to write. ADR-0124 decision 6 keeps what grants access unseeded, so it stays at
`RandomNumberGenerator.GetHexString(32, lowercase: true)`. A test that needs a known value for it has none
to ask for, by design; it reads the `Location` the server answers.

## Alternatives considered

- **Seed all three.** Simplest and most deterministic, but a seeded GCS upload ID is guessable by anyone who
  knows the seed, and it is the only thing guarding the staged upload.
- **Seed none.** Leaves two tests unable to replay a recorded multipart or blob upload for no security gain,
  since both servers check a login on every request.

## Consequences

- Two instances with the same `RandomSeed` and the same exchange order give the same S3 `UploadId` and OCI
  upload session UUID.
- The GCS upload ID differs every run; a recorded GCS resumable exchange cannot be replayed byte for byte.
