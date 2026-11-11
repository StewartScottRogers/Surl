# Google Cloud Storage JSON API requirement matrix

These are the server-side requirements that `Surl.Extended.Protocol.GoogleCloudStorage` answers when a `gs`
request arrives on a JSON path (ADR-0083 decision 9). They are written from the Cloud Storage JSON API
reference pages that
[ADR-0148](../../Planning/Decisions/ADR-0148-how-the-google-cloud-storage-server-answers-the-json-api.md)
decides by, never from the Curl port's client matrix, which speaks the XML API
([GoogleCloudStorage.md](GoogleCloudStorage.md)). Each row binds the server. "Covered by" names the task that
will prove the row until that task replaces its ID with the named test, or it says why the row needs no test.
Nothing here is built yet: every row names its task.

IDs are:

- `GCSJ:<Page>-<n>` for a rule of a JSON API reference page;
- `GCSJ:Surl-<Decision>-<n>` for a rule ADR-0148 adds where the pages leave the server a choice.

## Routing and operations (ADR-0148 decisions 1 and 2)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| GCSJ:Surl-Routing-1 | MUST | A path-style request under `/storage/v1/`, `/upload/storage/v1/` or `/download/storage/v1/` is JSON; any other path is the XML API. | BL-1045 |
| GCSJ:Surl-Routing-2 | MUST | A virtual-hosted request is the XML API even when its path begins `/storage/v1/`. | BL-1045 |
| GCSJ:Surl-Routing-3 | MUST | The grammar is decided after the head limits and before authentication. | BL-1045 |
| GCSJ:Surl-Operations-1 | MUST | Any JSON request ADR-0148 decision 2 does not list is `501 notImplemented`, judged after authentication. | BL-1045 |
| GCSJ:Surl-Operations-2 | MUST | Bucket creation, update, patch and deletion, ACLs, IAM, notifications, HMAC keys, `watchAll`, `restore`, `moveTo` and unlisted query parameters are `501 notImplemented`. | BL-1045 |
| GCSJ:Surl-Operations-3 | MUST | A `HEAD` to a JSON path is `501 notImplemented` with its body. | BL-1045 |
| GCSJ:BucketsList-1 | MUST | `GET /storage/v1/b?project=<id>` answers `storage#buckets` with each top-level folder the caller may read, in UTF-8 byte order. | BL-1045 |
| GCSJ:BucketsList-2 | MUST | `project` is required: without it `400 required`. Any value is accepted and ignored. | BL-1045 |
| GCSJ:BucketsGet-1 | MUST | `GET /storage/v1/b/<bucket>` answers `storage#bucket`; no such bucket or no read access is `404 notFound`. | BL-1045 |
| GCSJ:ObjectsList-1 | MUST | `GET …/o` answers `storage#objects` with `items`, `prefixes` and `nextPageToken` in UTF-8 byte order. | BL-1045 |
| GCSJ:ObjectsList-2 | MUST | `prefix`, `delimiter`, `includeTrailingDelimiter`, `startOffset` (inclusive) and `endOffset` (exclusive) apply. | BL-1045 |
| GCSJ:ObjectsList-3 | MUST | `maxResults` defaults to and is capped at 1000, `0` is an empty page, bounds `items` and `prefixes` together; a bad value is `400 invalidParameter`. | BL-1045 |
| GCSJ:ObjectsList-4 | MUST | `pageToken` resumes after the last entry returned; one that does not decode is `400 invalidParameter`. | BL-1045 |
| GCSJ:ObjectsGet-1 | MUST | `GET …/o/<object>` answers `storage#object`; a missing object, or a `generation` other than the current one, is `404 notFound`. | BL-1045 |
| GCSJ:ObjectsGet-2 | MUST | `alt=media` (or `/download/storage/v1/…?alt=media`) serves the bytes with the XML API's headers; one `Range` is `206`, an unsatisfiable one `416 requestedRangeNotSatisfiable`. | BL-1045 |
| GCSJ:ObjectsGet-3 | MUST | An `alt` other than `json` and `media` is `400 invalidParameter`. | BL-1045 |
| GCSJ:ObjectsGet-4 | MUST | `ifGenerationMatch`, `ifGenerationNotMatch`, `ifMetagenerationMatch`, `ifMetagenerationNotMatch` and the `If-*` headers give `412 conditionNotMet` or `304 notModified`. | BL-1045 |
| GCSJ:ObjectsDelete-1 | MUST | `DELETE …/o/<object>` is `204`; a missing object is `404 notFound`; conditions apply. | BL-1045 |
| GCSJ:Surl-Names-1 | MUST | The object name after `/o/` is percent-decoded once as UTF-8; a literal `/` and `%2F` both separate folders. | BL-1045 |
| GCSJ:Surl-Names-2 | MUST | A name over 1024 UTF-8 bytes is `400 invalid`; a name the store cannot hold is `404 notFound` on a read and `400 invalid` on a write; a name in conflict with an entry of the other kind is `409 conflict`. | BL-1045 |
| GCSJ:Surl-Params-1 | MAY | `fields`, `prettyPrint`, `projection`, `quotaUser`, `userIp` and `key` select or tune the answer. | n/a: accepted and ignored; a whole resource is a valid answer (ADR-0148 decision 2) |
| GCSJ:Surl-Params-2 | MUST NOT | `access_token` and `oauth_token` in the query authenticate. | BL-1045 |

## Resources (ADR-0148 decision 3)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| GCSJ:ObjectResource-1 | MUST | A `storage#object` carries `kind`, `id`, `selfLink`, `mediaLink`, `name`, `bucket`, `generation`, `metageneration`, `contentType`, `storageClass`, `size`, `md5Hash`, `crc32c`, `etag`, `timeCreated`, `updated` and `timeStorageClassUpdated`, in that order, with 64-bit numbers as strings and times in RFC 3339 with milliseconds. | BL-1045 |
| GCSJ:ObjectResource-2 | MUST | `contentEncoding`, `contentDisposition`, `cacheControl`, `contentLanguage` and `metadata` appear when stored. | BL-1045 |
| GCSJ:ObjectResource-3 | MUST | `md5Hash` and `crc32c` are the base64 of the MD5 and the big-endian CRC-32C, equal to the XML API's `x-goog-hash`. | BL-1045 |
| GCSJ:Surl-Resource-1 | MUST | An object written over either grammar reads back over the other with the same generation, MD5, CRC-32C and metadata. | BL-1045 |
| GCSJ:Surl-Resource-2 | MUST | The metageneration is recorded in the object metadata index: 1 when the data is written, +1 on each patch or update; XML reads report it. | BL-1045, BL-1041 |
| GCSJ:BucketResource-1 | MUST | A `storage#bucket` carries `kind`, `id`, `name`, `selfLink`, `projectNumber`, `metageneration`, `location`, `storageClass`, `etag`, `timeCreated` and `updated`. | BL-1045 |

## Authentication and errors (ADR-0148 decisions 4 and 5)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| GCSJ:Surl-Auth-1 | MUST | With anonymous off, no credential is `401 required` with `WWW-Authenticate: Bearer realm="storage"`. | BL-1045 |
| GCSJ:Surl-Auth-2 | MUST | Under `--allow-anonymous`, unauthenticated reads are served and an anonymous write is `401 required`. | BL-1045 |
| GCSJ:Surl-Auth-3 | MUST | A Bearer token of an empty-name account's password is served; one that is no account, or one sent over plain text without `--allow-plaintext-auth`, is `401 authError` with an `error="invalid_token"` challenge. | BL-1045 |
| GCSJ:Surl-Auth-4 | MUST | Another scheme, `GOOG4-HMAC-SHA256` included, is `400 invalid`. | BL-1045 |
| GCSJ:Surl-Auth-5 | MUST | A write needs `--allow-uploads` and a login: otherwise `403 forbidden`; a bucket or object the caller may not read is `404 notFound`. | BL-1045, BL-1041 |
| GCSJ:Surl-Auth-6 | MUST | Each refusal waits the failed-login delay first. | BL-1045 |
| GCSJ:Errors-1 | MUST | A failure has its status and `{"error":{"code":…,"message":…,"errors":[{"message":…,"domain":…,"reason":…}]}}` as `application/json; charset=UTF-8` on one line. | BL-1045 |
| GCSJ:Errors-2 | MUST | Each status and reason pair is ADR-0148 decision 5's row, `notModified` with no body. | BL-1045, BL-1041, BL-1042, BL-1043 |
| GCSJ:Surl-Errors-1 | MUST | `message` is fixed text and no client byte (bucket, object, parameter, token) is echoed. | BL-1045 |
| GCSJ:Surl-Errors-2 | MUST | A chosen fault is asked of the plan with the method id as command and the request path as path; a reply that does not fit is noted and not applied. | BL-1045 |
| GCSJ:Errors-3 | MAY | `location` and `locationType` name the offending parameter. | n/a: not sent, ADR-0148 decision 5 (ADR-0006 section 3); the recorded exchange holds the request |

## Uploads, patch and update (ADR-0148 decision 7)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| GCSJ:MediaUpload-1 | MUST | `POST /upload/storage/v1/b/<bucket>/o?uploadType=media&name=<object>` stores the body and answers `storage#object`; `name` is required (`400 required`). | BL-1041 |
| GCSJ:MultipartUpload-1 | MUST | `uploadType=multipart` takes a `multipart/related` body of JSON metadata then bytes, naming the object by the metadata's or the query's `name`. | BL-1041 |
| GCSJ:MultipartUpload-2 | MUST | A third part, no boundary or an unterminated body is `400 badRequest`; metadata past 1 MiB is `400 invalid`. | BL-1041 |
| GCSJ:Surl-Upload-1 | MUST | `ifGenerationMatch=0` stores only when no object exists; a failing condition is `412 conditionNotMet`. | BL-1041 |
| GCSJ:Surl-Upload-2 | MUST | A body past `--max-filesize` is `413 uploadTooLarge`, then close; a sent `md5Hash` or `crc32c` that does not match is `400 invalid` and stores nothing. | BL-1041 |
| GCSJ:Surl-Upload-3 | MUST | Metadata past 8 KiB is `400 invalid`; a full object metadata index is `403 quotaExceeded`. | BL-1041 |
| GCSJ:ObjectsPatch-1 | MUST | `PATCH` merges the body into the writable metadata, a `null` removing a field or a `metadata` key, and raises the metageneration without changing bytes or generation. | BL-1041 |
| GCSJ:ObjectsUpdate-1 | MUST | `PUT …/o/<object>` replaces all writable metadata with the body's and raises the metageneration. | BL-1041 |
| GCSJ:ObjectsPatch-2 | MAY | `acl`, `customTime`, `retention`, `kmsKeyName` and holds are writable. | n/a: ignored on a write and absent from a read (ADR-0148 decision 3) |

## Resumable uploads (ADR-0148 decision 7; ADR-0110 amendment 2)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| GCSJ:ResumableUpload-1 | MUST | `POST …?uploadType=resumable` answers `200` with `Location: <session URI>` carrying `upload_id`, reading `X-Upload-Content-Type` and the JSON metadata. | BL-1042 |
| GCSJ:ResumableUpload-2 | MUST | A chunk with `Content-Range` is appended when it starts at the next byte expected, and answered `308` with `Range: bytes=0-<last>` until the total is in. | BL-1042 |
| GCSJ:ResumableUpload-3 | MUST | `Content-Range: bytes */<total>` with no body is a status check answering what is kept. | BL-1042 |
| GCSJ:ResumableUpload-4 | MUST | The last chunk answers `200` with `storage#object`. | BL-1042 |
| GCSJ:ResumableUpload-5 | MUST | A cancel (`DELETE` of the session URI) answers `499`; a later request to the session is `404 notFound`. | BL-1042 |
| GCSJ:Surl-Resumable-1 | MUST | A session belongs to one grammar: an XML session ID on a JSON URL, or the reverse, is `404 notFound`. | BL-1042 |
| GCSJ:Surl-Resumable-2 | MUST | The 1001st open upload (shared with the XML API) is `503 backendError`; an upload idle for 24 hours on `TimeProvider` is discarded. | BL-1042 |
| GCSJ:Surl-Resumable-3 | MUST | A `Content-Range` that does not parse or a chunk of the wrong length is `400 badRequest`; a total past `--max-filesize` is `413 uploadTooLarge`. | BL-1042 |

## Copy, rewrite and compose (ADR-0148 decision 7)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| GCSJ:ObjectsCopy-1 | MUST | `POST …/copyTo/b/<destBucket>/o/<destObject>` stores a new generation, metageneration 1, with the source's stored fields unless the body names new metadata. | BL-1043 |
| GCSJ:ObjectsRewrite-1 | MUST | `rewriteTo` answers `storage#rewriteResponse` with `done` true, `totalBytesRewritten`, `objectSize` and `resource`, in one call. | BL-1043 |
| GCSJ:ObjectsRewrite-2 | MAY | `rewriteToken` and `maxBytesRewrittenPerCall` continue or bound a rewrite. | n/a: ignored, a rewrite always finishes in one call (ADR-0148 decision 7) |
| GCSJ:ObjectsCompose-1 | MUST | `compose` concatenates 1 to 32 `sourceObjects` in order into the destination; 33 is `400 invalid`, a missing source `404 notFound`. | BL-1043 |
| GCSJ:Surl-Copy-1 | MUST | The source must be readable and the destination writable by the caller; `ifGenerationMatch=0` and source preconditions give `412 conditionNotMet`; a result past `--max-filesize` is `413 uploadTooLarge`. | BL-1043 |

## Surl's own help (ADR-0148 decision 9)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| GCSJ:Surl-Help-1 | MUST | `surl --aihelp gs` says the server also answers the JSON API on the same listener, with Bearer-only authentication and `501` for what is not served. | BL-1044 |
