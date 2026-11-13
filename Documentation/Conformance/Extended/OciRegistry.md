# OCI registry requirement matrix

The server-side requirements `Surl.Extended.Protocol.OciRegistry` answers, for the `oci` scheme
(ADR-0083 decision 9). Written from the OCI Distribution Specification v1.1.0
(<https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md>), the OCI Image Format
Specification v1.1.0 and Docker's Token Authentication Specification, as
[ADR-0115](../../Planning/Decisions/ADR-0115-how-the-oci-registry-server-answers-oci.md) decides by
them, never from the Curl port's client matrix. Each row binds the server; "Covered by" names the
task that proves it, until that task replaces its ID with the named test, or says why the row needs
no test.

IDs are `OCI:Dist-<Section>-<n>` for the Distribution Specification, `OCI:Image-<Section>-<n>` for
the Image Format Specification, `OCI:Token-<Section>-<n>` for the Token Authentication
Specification and RFC 6750, and `OCI:Surl-<Decision>-<n>` for a rule ADR-0115 adds where the
specifications leave the server a choice.

## Determining support and errors ("Determining Support", "Error Codes")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| OCI:Dist-Support-1 | MUST | `GET /v2/` answers `200` to a caller allowed in (end-1). | BL-436 |
| OCI:Dist-ErrorCodes-1 | MUST | A JSON error body is `{"errors":[{"code":…,"message":…}]}`, its `code` one of code-1 to code-14. | BL-436 |
| OCI:Dist-ErrorCodes-2 | MUST | Each code is sent with the status ADR-0115 decision 5.1's table gives it. | BL-436 |
| OCI:Surl-Errors-1 | MUST | `message` is fixed text per code and `detail` is omitted, so no client byte is echoed; a `HEAD` error has no body. | BL-436 |
| OCI:Surl-Errors-2 | MUST | A path no endpoint matches is `404 UNSUPPORTED`, another method on an endpoint `405 UNSUPPORTED` with `Allow`, both judged before the token, since the endpoints are public (ADR-0115 Amendment 1, point 1). | BL-436 |
| OCI:Surl-Headers-1 | MUST | Every answer carries `Docker-Distribution-API-Version: registry/2.0`. | BL-436 |
| OCI:Dist-Warnings-1 | MAY | `Warning` headers with code 299. | n/a: no warning is sent (ADR-0115 decision 1) |

## Names, references and digests (the `<name>` and tag grammars; Image "Descriptors")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| OCI:Dist-Name-1 | MUST | A `<name>` off Dist's grammar, or past 255 bytes, is `400 NAME_INVALID`. | BL-436 |
| OCI:Dist-Tag-1 | MUST | A tag is at most 128 characters matching `[a-zA-Z0-9_][a-zA-Z0-9._-]{0,127}`; another is `404 MANIFEST_UNKNOWN` on a read and `400 MANIFEST_INVALID` on a write. | BL-436 |
| OCI:Image-Digest-1 | MUST | `sha256` (64 lower-case hex digits) and `sha512` (128) are the digests served. | BL-436 |
| OCI:Surl-Digest-1 | MUST | On a read, a digest that does not parse or names another algorithm is `404 BLOB_UNKNOWN` or `MANIFEST_UNKNOWN`. | BL-436 |
| OCI:Surl-Digest-2 | MUST | On a write, such a digest is `400 DIGEST_INVALID`. | BL-437 |

## Storage on the content store (ADR-0100 decision 2.4; Image "OCI Image Layout Specification")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| OCI:Image-Layout-1 | MUST | A repository is a content-store folder holding `oci-layout` `{"imageLayoutVersion":"1.0.0"}`; a folder without it is no repository (`404 NAME_UNKNOWN`). | BL-436 |
| OCI:Image-Layout-2 | MUST | Blobs and manifests are stored at `blobs/<algorithm>/<hex>`; a pushed blob is a file at that path. | BL-437, `CurlPortClientTransfersContentWithSurlOverOciTests.BlobPush_WithATokenLogin_StoresTheBlobUnderItsDigest` |
| OCI:Image-Layout-3 | MUST | `index.json` lists one descriptor per tag, annotated `org.opencontainers.image.ref.name`, and one untagged descriptor for each other pushed manifest. | BL-437, `CurlPortClientTransfersContentWithSurlOverOciTests.ManifestPush_WithATokenLogin_StoresTheManifestAndListsItsTag` |
| OCI:Surl-Store-1 | MUST | A manifest's `Content-Type` is its listing descriptor's `mediaType`; a digest is a manifest only when `index.json`, or an index it lists, names it. | BL-436 |
| OCI:Surl-Store-2 | MUST | `index.json` is rewritten whole through a temporary file, one writer per repository; one that does not parse reads as empty and is noted. | BL-437 |
| OCI:Surl-Store-3 | MUST | A blob or manifest whose bytes do not hash to its file name is `404 BLOB_UNKNOWN` or `MANIFEST_UNKNOWN` and noted; the check is cached against the file's stamp. | BL-436 |
| OCI:Surl-Store-4 | MUST | Pushed content survives a restart under `--directory`. | BL-437 |
| OCI:Surl-Store-5 | MUST | A reserved device-name segment, a file in the name's path, a name inside another repository's `blobs`, or a first push to a non-empty non-repository folder is `400 NAME_INVALID` on a write. | BL-437 |
| OCI:Surl-Store-6 | MUST | A first write to a new name creates its folder and `oci-layout`. | BL-437 |
| OCI:Surl-Store-7 | MUST | A repository the caller may not read is `404 NAME_UNKNOWN`; a write the share forbids is `403 DENIED`. | BL-437 |
| OCI:Surl-Store-8 | MUST | An upload stages under `.surl/objects/uploads/oci-<id>/` and is invisible until completed; stale `oci-` staging is removed at start. | BL-437 |

## Pull ("Pulling manifests", "Pulling blobs", "Checking if content exists")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| OCI:Dist-PullManifests-1 | MUST | `GET /v2/<name>/manifests/<reference>` by tag or digest answers `200` with the stored bytes. | BL-436, `CurlPortClientTransfersContentWithSurlOverOciTests.ManifestPull_ByTag_ReturnsTheStoredManifest`, `CurlPortClientTransfersContentWithSurlOverOciTests.ManifestPull_ByDigest_ReturnsTheStoredManifest` |
| OCI:Dist-PullManifests-2 | SHOULD | The answer carries `Docker-Content-Digest`. | BL-436 |
| OCI:Dist-PullManifests-3 | SHOULD NOT | `Content-Type` carries parameters. | BL-436 |
| OCI:Dist-PullManifests-4 | MUST | A manifest not found is `404 MANIFEST_UNKNOWN`; a repository not found `404 NAME_UNKNOWN`. | BL-436 |
| OCI:Surl-PullManifests-1 | MUST | `Accept` never converts or refuses: the stored manifest is served. | BL-436 |
| OCI:Dist-PullBlobs-1 | MUST | `GET /v2/<name>/blobs/<digest>` answers `200` with the blob, `Docker-Content-Digest` matching the body. | BL-436, `CurlPortClientTransfersContentWithSurlOverOciTests.BlobPull_ByDigest_ReturnsTheStoredBlob` |
| OCI:Dist-PullBlobs-2 | MUST | A blob not found is `404 BLOB_UNKNOWN`. | BL-436 |
| OCI:Surl-PullBlobs-1 | MUST | One `Range: bytes=` range is `206` with `Content-Range`; an unsatisfiable one `416` with `bytes */<size>`; several are ignored. | BL-436 |
| OCI:Dist-Exists-1 | MUST | `HEAD` on an existing blob or manifest is `200` with the GET's head and no body; a missing one `404`. | BL-436 |
| OCI:Dist-Exists-2 | SHOULD | A `HEAD` answer carries `Content-Length`. | BL-436 |

## Push ("Pushing blobs", "Pushing manifests")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| OCI:Dist-PushPost-1 | MUST | `POST /v2/<name>/blobs/uploads/` answers `202` with a `Location` holding a UUID session ID. | BL-437, `CurlPortClientTransfersContentWithSurlOverOciTests.BlobPush_WithATokenLogin_StoresTheBlobUnderItsDigest` |
| OCI:Dist-PushPut-1 | MUST | `PUT <location>?digest=<digest>` completes the upload with `201` and `Location`. | BL-437, `CurlPortClientTransfersContentWithSurlOverOciTests.BlobPush_WithATokenLogin_StoresTheBlobUnderItsDigest` |
| OCI:Dist-PushSinglePost-1 | MAY | `POST …/uploads/?digest=<digest>` with the blob answers `201`. | BL-437 |
| OCI:Dist-PushChunked-1 | MUST | Each `PATCH` chunk answers `202` with `Location` and `Range: 0-<end>`. | BL-437 |
| OCI:Dist-PushChunked-2 | MUST | A chunk out of order is `416`. | BL-437 |
| OCI:Dist-PushChunked-3 | MUST | `GET` on an active session is `204` with `Location` and `Range`. | BL-437 |
| OCI:Dist-PushChunked-4 | MUST | The closing `PUT`, with or without a last chunk, answers `201` with `Location`. | BL-437 |
| OCI:Surl-PushChunked-1 | MUST | A `PATCH` without `Content-Range` appends its whole body; a `Content-Range` span not matching the body is `400 SIZE_INVALID`. | BL-437 |
| OCI:Dist-PushChunked-5 | SHOULD | A minimum chunk size is advertised with `OCI-Chunk-Min-Length`. | n/a: there is no minimum (ADR-0115 decision 5.4) |
| OCI:Surl-PushDigest-1 | MUST | A blob whose bytes do not hash to `digest` is `400 DIGEST_INVALID`, its staging removed and its session ended. | BL-437 |
| OCI:Dist-Mount-1 | MUST | A mount from a readable `from` holding the blob answers `201` with `Location`. | BL-437 |
| OCI:Dist-Mount-2 | SHOULD | A mount that cannot be made answers `202` and opens a session. | BL-437 |
| OCI:Surl-Mount-1 | MUST | Without `from`, or with a `from` the caller may not read, no blob is cross-mounted. | BL-437 |
| OCI:Surl-Upload-1 | MUST | `DELETE` on a session cancels it with `204`; an unknown, expired or another account's session is `404 BLOB_UPLOAD_UNKNOWN`. | BL-437 |
| OCI:Dist-PushManifests-1 | MUST | `PUT /v2/<name>/manifests/<reference>` stores the exact bytes and answers `201` with `Location` and `Docker-Content-Digest` equal to a digest reference. | BL-437, `CurlPortClientTransfersContentWithSurlOverOciTests.ManifestPush_WithATokenLogin_StoresTheManifestAndListsItsTag` |
| OCI:Dist-PushManifests-2 | SHOULD | `Content-Type` parameters are ignored. | BL-437 |
| OCI:Surl-PushManifests-1 | MUST | A media type other than the OCI and Docker v2 manifest and index types, a body off ADR-0115 decision 3.1's shape, or a `mediaType` unlike `Content-Type`, is `400 MANIFEST_INVALID`. | BL-437 |
| OCI:Surl-PushManifests-2 | MUST | A manifest pushed by digest whose bytes hash otherwise is `400 DIGEST_INVALID`. | BL-437 |
| OCI:Dist-PushManifests-3 | MAY | A manifest naming a blob or manifest the repository lacks is rejected with one `MANIFEST_BLOB_UNKNOWN` per missing digest (`400`). | BL-437 |
| OCI:Dist-PushManifests-4 | SHOULD | A manifest past the size limit (4 MiB) is `413`. | BL-437 |
| OCI:Dist-Subject-1 | MUST | A manifest with a `subject` is accepted whether or not the subject exists. | BL-437 |
| OCI:Dist-Subject-2 | MUST | Its push is answered with `OCI-Subject: <subject digest>`. | BL-437 |
| OCI:Surl-Tags-1 | MUST | A push by tag moves that tag; the manifest it named stays as an untagged descriptor. | BL-437 |

## Content discovery ("Listing Tags", "Listing Referrers")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| OCI:Dist-Tags-1 | MUST | `GET /v2/<name>/tags/list` answers `200` `{"name":…,"tags":[…]}`, the tags in lexical order, `[]` when none. | BL-436 |
| OCI:Dist-Tags-2 | MUST | `n` returns `n` tags unless fewer remain; `n=0` returns an empty list and no `Link`. | BL-436 |
| OCI:Dist-Tags-3 | MUST | `last` starts the list after that tag. | BL-436 |
| OCI:Surl-Tags-2 | MUST | When tags remain, `Link: </v2/<name>/tags/list?n=<n>&last=<tag>>; rel="next"`; an `n` off 0 to 10000 is `400 UNSUPPORTED`. | BL-436 |
| OCI:Dist-Referrers-1 | MUST | `GET /v2/<name>/referrers/<digest>` answers `200` with `Content-Type: application/vnd.oci.image.index.v1+json`, an index of the manifests whose `subject` is that digest. | BL-436 |
| OCI:Dist-Referrers-2 | MUST | Each descriptor carries `artifactType` (the manifest's, else its `config.mediaType`, else omitted) and the manifest's `annotations`. | BL-436 |
| OCI:Dist-Referrers-3 | MUST | An unknown subject is `200` with an empty list, never `404`; an invalid digest is `400 DIGEST_INVALID`. | BL-436 |
| OCI:Dist-Referrers-4 | SHOULD | `artifactType` filters the list; the answer then carries `OCI-Filters-Applied: artifactType`. | BL-436 |
| OCI:Dist-Referrers-5 | MUST | Manifests pushed with a `subject` are listed, and image indexes tagged with the referrers tag schema contribute their entries. | BL-437 |
| OCI:Dist-Referrers-6 | MUST | A list that cannot be one answer is paged with `Link`. | n/a: a repository holds at most 10000 descriptors, so the list is always one answer (ADR-0115 decision 5.3) |

## Content management ("Deleting tags", "Deleting manifests", "Deleting blobs")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| OCI:Dist-DeleteTags-1 | MUST | `DELETE /v2/<name>/manifests/<tag>` removes the tag and answers `202`. | BL-437 |
| OCI:Dist-DeleteManifests-1 | MUST | `DELETE /v2/<name>/manifests/<digest>` removes the manifest and every descriptor of it, `202`; a missing repository `404`. | BL-437 |
| OCI:Dist-DeleteBlobs-1 | MUST | `DELETE /v2/<name>/blobs/<digest>` removes the blob, `202`; a missing blob `404 BLOB_UNKNOWN`. | BL-437 |
| OCI:Surl-Delete-1 | MUST | A delete needs `--allow-uploads` and a token granting `delete`; otherwise `403 DENIED`. | `OciTokenEndpointTests.AnswerAsync_Scopes_GrantWhatTheyNameAndDropTheRest`, `OciAccessCheckTests.CheckAsync_WriteWithoutAToken_Returns401WithTheScopeItNeeds` |

## Authentication (Token; RFC 6750 section 3.1)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| OCI:Token-Challenge-1 | MUST | A request with no token, anonymous off, is `401 UNAUTHORIZED` with `WWW-Authenticate: Bearer realm=…,service="surl",scope="repository:<name>:<actions>"` (no `scope` on `/v2/`). | `OciAccessCheckTests.CheckAsync_PullWithoutAToken_Returns401WithTheRealmServiceAndScope`, `OciAccessCheckTests.CheckAsync_V2WithoutAToken_Returns401WithNoScope`, `CurlPortClientTransfersContentWithSurlOverOciTests.BlobPush_WithoutCredentials_IsRefusedAndStoresNothing` |
| OCI:Surl-Auth-1 | MUST | The realm is built from the listen host or the connection's local address and port, never from `Host`. | `OciChallengeTests` |
| OCI:Token-Request-1 | MUST | `GET /token?service=surl&scope=…` with good Basic credentials answers `200` `{"token","access_token","expires_in":300,"issued_at"}`. | `OciTokenEndpointTests.AnswerAsync_GoodCredentials_IssuesATokenWithItsLifetimeAndIssueTime`, `CurlPortClientTransfersContentWithSurlOverOciTests.BlobPush_WithATokenLogin_StoresTheBlobUnderItsDigest` |
| OCI:Token-Request-2 | MUST | Missing or bad credentials at the token endpoint are `401 UNAUTHORIZED` with a Basic challenge, after the failed-login delay the authentication policy applies. | `OciTokenEndpointTests.AnswerAsync_BadOrUnreadableCredentials_Returns401WithTheBasicChallenge`, `OciTokenEndpointTests.AnswerAsync_NoCredentialsWithAnonymousOff_Returns401WithTheBasicChallenge` |
| OCI:Token-Scope-1 | MUST | A token grants an account `pull`, and `push` and `delete` only under `--allow-uploads`; the share's access is applied by the content store on each request (ADR-0115 Amendment 1, point 3); other resource types, unknown actions, invalid names and scopes past 16 are dropped. | `OciTokenEndpointTests.AnswerAsync_Scopes_GrantWhatTheyNameAndDropTheRest`, `OciTokenEndpointTests.AnswerAsync_ScopesPastSixteen_AreDropped` |
| OCI:Surl-Auth-2 | MUST | Under `--allow-anonymous`, reads are served without a token and an anonymous token grants `pull` only; an anonymous write is challenged. | `OciAccessCheckTests.CheckAsync_AnonymousPullUnderAllowAnonymous_IsServedAsTheAnonymousCaller`, `OciAccessCheckTests.CheckAsync_AnonymousPushUnderAllowAnonymous_Returns401WithTheChallenge`, `OciTokenEndpointTests.AnswerAsync_NoCredentialsUnderAllowAnonymous_IssuesAPullOnlyToken` |
| OCI:Surl-Auth-3 | MUST | `POST /token` is `405 UNSUPPORTED`; another `service` is `400 UNSUPPORTED`. | `OciTokenEndpointTests.AnswerAsync_Post_Returns405WithAllowGet`, `OciTokenEndpointTests.AnswerAsync_AnotherService_Returns400Unsupported` |
| OCI:Token-Bearer-1 | MUST | A request with a token granting its repository and action is served. | `OciAccessCheckTests.CheckAsync_PullWithAPullScopedToken_IsServed` |
| OCI:Token-Bearer-2 | MUST | An unknown or expired token is `401` with `error="invalid_token"`. | `OciAccessCheckTests.CheckAsync_ExpiredToken_Returns401InvalidToken`, `OciAccessCheckTests.CheckAsync_UnknownTokenOrAnotherScheme_Returns401InvalidToken` |
| OCI:Token-Bearer-3 | MUST | A token not granting the action is `403 DENIED` with `error="insufficient_scope"`. | `OciAccessCheckTests.CheckAsync_PushWithAPullOnlyToken_Returns403DeniedWithInsufficientScope` |
| OCI:Surl-Auth-4 | MUST | Tokens are 32 random bytes in base64url, held in memory, expiring after 300 seconds on `TimeProvider`; 10000 live at most (`429 TOOMANYREQUESTS`). | `OciTokenBookTests`, `OciTokenEndpointTests.AnswerAsync_PastTheLiveTokenBound_Returns429TooManyRequests` |
| OCI:Surl-Auth-5 | MUST | Basic on `/v2/` is answered with the Bearer challenge, `error="invalid_token"`. | `OciAccessCheckTests.CheckAsync_UnknownTokenOrAnotherScheme_Returns401InvalidToken` |
| OCI:Surl-Auth-6 | MUST | A write while `--allow-uploads` is off is `403 DENIED`. | BL-437 |
| OCI:Token-Refresh-1 | MAY | A `refresh_token` is issued. | n/a: none is issued (ADR-0115 decision 4.3) |

## TLS and limits (ADR-0115 decisions 6 and 7; ADR-0006)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| OCI:Surl-Tls-1 | MUST | `oci` is implicit TLS, `--cert` or `--self-signed` required (58 without), ALPN `http/1.1` only. | BL-1028 |
| OCI:Surl-Tls-2 | MUST | `--oci-plaintext` serves plain HTTP/1.1 and is warned on start. | BL-971 |
| OCI:Surl-Tls-3 | MUST | Over plain text without `--allow-plaintext-auth`, credentials and tokens are `403 DENIED`. | credentials: `OciTokenEndpointTests.AnswerAsync_CredentialsOverPlainTextRefused_Returns403Denied`; tokens: BL-890 |
| OCI:Surl-Limits-1 | MUST | A blob past `--max-filesize` is `413 SIZE_INVALID` and its session ended. | BL-437 |
| OCI:Surl-Limits-2 | MUST | 1000 open sessions at most (`429 TOOMANYREQUESTS`); a session idle 1 hour expires (`404 BLOB_UPLOAD_UNKNOWN`). | BL-437 |
| OCI:Surl-Limits-3 | MUST | A repository past 10000 descriptors is `403 DENIED`. | BL-437 |
| OCI:Surl-Limits-4 | MUST | Past `--max-connections` or `--max-connections-per-address` is `429 TOOMANYREQUESTS`, then close. | BL-436 |
| OCI:Surl-Limits-5 | MUST | `--head-timeout`, `--idle-timeout` and `--max-time` close with no bytes. | BL-436 |
