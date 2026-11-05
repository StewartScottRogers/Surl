# Surl.Extended.Protocol.OciRegistry.UnitLibrary

The `oci` server (ADR-0115): answers OCI Distribution Specification v1.1.0 registry requests over
HTTP/1.1 on the shared `ContentStore`, where a repository `a/b` is the folder `a/b` holding an OCI
Image Layout - `oci-layout`, `blobs/<algorithm>/<hex>`, `index.json` (ADR-0100 decision 2.4). An
extended server (ADR-0083): judged by the distribution and image specifications, never by upstream
curl (which has no `oci`) and never by the Curl port. Namespace `Surl.Extended.Protocol.OciRegistry`.

Reads (BL-436, ADR-0115 decisions 1, 2, 3 and 5):

- `GET /v2/` - `200 {}`.
- Manifests (end-3), `GET` and `HEAD` by tag or digest: `Content-Type` the listing descriptor's
  media type without parameters, `Docker-Content-Digest`. `Accept` neither converts nor refuses.
  A digest names a manifest when `index.json` lists it or an image index it lists names it.
- Blobs (end-2), `GET` and `HEAD` by digest: `application/octet-stream`, `Docker-Content-Digest`,
  `Accept-Ranges: bytes`; one `Range` is `206`, an unsatisfiable one `416` with
  `Content-Range: bytes */<size>`, several ranges are ignored. A manifest's digest is no blob.
- The tag list (end-8): ordinal order, `n` (0 to 10000, else `400 UNSUPPORTED`), `last`, and a
  `Link: <...>; rel="next"` while tags remain. Tags off Dist's grammar in `index.json` are skipped.
- The referrers API (end-12): an image index of the manifests whose `subject.digest` is the digest,
  plus the entries of an image index tagged `<alg>-<hex>`, filtered by `artifactType` (with
  `OCI-Filters-Applied: artifactType`), ordered by digest; an invalid digest is `400 DIGEST_INVALID`.

Every file is served only after its bytes hash to the digest its name claims (`sha256` or `sha512`);
one that does not is `BLOB_UNKNOWN` or `MANIFEST_UNKNOWN` and noted. The check runs on every read;
caching it in `ObjectMetadataIndex` is follow-up work. Manifests are read up to 4 MiB and
`index.json` up to 16 MiB; an `index.json` that does not parse is read as empty and noted.

Every other request: a path no endpoint matches `404 UNSUPPORTED`; an endpoint's path with another
method `405 UNSUPPORTED` with `Allow`; a name off the grammar or past 255 bytes `400 NAME_INVALID`;
no repository `404 NAME_UNKNOWN`. Every answer carries `Date`, `Server: surl` and
`Docker-Distribution-API-Version: registry/2.0`; every error the one-line JSON body
`{"errors":[{"code":…,"message":…}]}` with Dist's description as the fixed message (none on `HEAD`).
A connection past a connection limit is `429 TOOMANYREQUESTS` (`OciRegistryProtocolServer` is the
`IConnectionRefusalWriter`).

Writes (BL-437, ADR-0115 decisions 3, 3.1 and 5.4, ADR-0130), each `403 DENIED` unless the store
allows uploads (`--allow-uploads`), and `403 DENIED` where the share forbids the write:

- Blob uploads: `POST .../blobs/uploads/` opens a session (`202`, `Location`, `Range: 0-0`,
  `Docker-Upload-UUID`); `PATCH` appends a chunk (`Content-Range` must start at the bytes held,
  else `416 BLOB_UPLOAD_INVALID`, and span the `Content-Length`, else `400 SIZE_INVALID`); `GET` is
  its status (`204`); `PUT ?digest=` closes it with an optional last chunk; `DELETE` cancels it. A
  single `POST ?digest=` uploads in one request; `?mount=&from=` copies a blob from a repository the
  caller may read (`201`), or opens a session (`202`). A digest the bytes do not hash to is
  `400 DIGEST_INVALID`; past `--max-filesize` `413 SIZE_INVALID`. Sessions (at most 1000, else
  `429`) expire after an idle hour on the `TimeProvider`: `404 BLOB_UPLOAD_UNKNOWN`. Bytes stage in
  a hidden content store upload file in `blobs/`, then are renamed to `blobs/<algorithm>/<hex>`.
- Manifest pushes (`PUT .../manifests/<tag or digest>`): OCI or Docker schema 2 media types, the
  body checked, stored byte for byte and listed in `index.json` (tagged or untagged); missing
  blobs or manifests `400 MANIFEST_BLOB_UNKNOWN` once per digest; past 4 MiB `413 MANIFEST_INVALID`;
  a `subject` answered with `OCI-Subject`; 10000 descriptors `403 DENIED`, `repository is full`.
- Deletes: a manifest by digest (file and descriptors), a tag (its descriptor only), a blob
  (`202`).
- The first upload session or manifest push creates the repository (`oci-layout`, `blobs`);
  a file in the way, a name inside another repository's `blobs`, or a non-empty non-repository
  folder is `400 NAME_INVALID`; a share the caller may not see `404 NAME_UNKNOWN`.

Not yet: token authentication (BL-438; until then every request is the anonymous caller on `oci`),
and serving `oci` from `surl` with TLS (BL-439).

- `OciRegistryProtocolServer` (public) - `IConnectionProtocolServer` over `Http1ConnectionServer`.
- `OciRequestAnswerer` - the `IHttpRequestHandler`: endpoint, method, name, repository, endpoint.
- `OciRequestTarget`, `OciEndpoint`, `OciQueryParameters`, `OciNames`, `OciDigest` - reading requests.
- `OciRepository`, `OciDescriptor`, `OciManifestJson`, `OciManifestFacts`, `OciFoundManifest`,
  `OciFoundBlob` - the layout.
- `OciPullAnswers`, `OciRangeSelection`, `OciTagsList`, `OciReferrers` - the answers.
- `OciWriteAnswerer`, `OciBlobUploads`, `OciManifestPush`, `OciDeletes` - the writes;
  `OciRepositoryCreation`, `OciPushedManifest`, `OciIndexFile` - what they check and write;
  `OciUploadSession`, `OciUploadSessions`, `OciUploadStream`, `OciServerState` - upload sessions and
  what one server keeps between requests; `OciRequestFields` - header fields.
- `OciReply`, `OciError`, `OciErrorBody` - common fields and error bodies.

This library references `Surl.Protocol.Abstractions`, `Surl.Content` and `Surl.HttpServing`, nothing
else (`ProtocolIsolationTests`), and never constructs a transport type. Tests replay request bytes
written from the specifications through `InMemoryConnection` on an `InMemoryContentFileSystem`.
