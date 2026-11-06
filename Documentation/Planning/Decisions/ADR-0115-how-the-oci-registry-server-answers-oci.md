# ADR-0115 — How the OCI registry server answers `oci`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-435. BL-436 builds the server with `/v2/`, pulls, the tags list, the referrers API and the
  error body (decisions 1, 2, 3 and 5), BL-437 the blob uploads, manifest pushes and deletes
  (decisions 1, 2, 3 and 7), BL-438 the token endpoint and the Bearer check (decision 4), and BL-439
  serves `oci` from `surl` with its TLS, its option and its `--aihelp` topic (decisions 6 and 8).
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof; port 443),
  [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decisions 1, 4 and 5 (`Surl.HttpServing`, logins through `IHttpAuthenticationSession`, content on
  `Surl.Content`), [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decisions 2.1 to 2.4 and 4.1 (the repository folder as an OCI Image Layout, refused names, the
  object metadata index, upload staging, the shared accounts), [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  decisions 2.2 to 2.4 (share access), [ADR-0108](ADR-0108-how-the-s3-server-answers-s3.md) (the S3
  server, whose shape this one follows where the two services agree),
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) (secure by
  default) and [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (limits, fixed error text).
- **Amends:** nothing. BL-435's own Goal planned storage "under the content store's `.surl/` area";
  Stewart's shared-stores approval and ADR-0100 decision 2.4 replaced that before this ADR, and
  decision 2 below applies ADR-0100.

## Context

`Surl.Extended.Protocol.OciRegistry` (in `Protocols/FileTransfer/`, ADR-0083's table) answers the
`oci` scheme the Curl port adds, on port 443 (ADR-0083 decision 7). By ADR-0083 decision 1 it is
judged by its specifications, not by upstream curl, which has no `oci` scheme, and not by the Curl
port. The documents read (2026-11-05) and relied on, cited below by section:

- **OCI Distribution Specification v1.1.0**
  (<https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md>), cited as "Dist":
  "Conformance", "Determining support", "Pull" (manifests, blobs, checking existence), "Push"
  (monolithic POST then PUT, single POST, chunked, mounting a blob, pushing manifests, pushing
  manifests with subject), "Content Discovery" (listing tags, listing referrers), "Content
  Management" (deleting tags, manifests and blobs), "Endpoints" (end-1 to end-13), "Error Codes"
  (code-1 to code-14), "Warnings", and the `<name>` and tag grammars.
- **OCI Image Format Specification v1.1.0**, cited as "Image": "Descriptors" (digests, the
  registered algorithms `sha256` and `sha512` and their lower-case hexadecimal encodings), "Media
  Types", "Image Manifest", "Image Index" and "OCI Image Layout Specification".
- **Docker's Token Authentication Specification** and its "Token Scope Documentation"
  (<https://distribution.github.io/distribution/spec/auth/token/>), cited as "Token": the
  `WWW-Authenticate: Bearer realm,service,scope` challenge, the token request, the token response's
  `token`, `access_token`, `expires_in` and `issued_at`, and the `repository:<name>:<actions>` scope
  grammar.
- **RFC 6750** section 3.1 (`invalid_token`, `insufficient_scope`), **RFC 7617** (Basic), **RFC 8288**
  (the `Link` header, which Dist cites as RFC 5988).

The specification's conformance suite (opencontainers/distribution-spec `conformance/`) is read as a
checklist of workflows - pull, push, content discovery, content management - and each of its
workflows is served below; none of its code is used.

Which requests a real client sends, as background and not as a verdict: the port's
`Curl.Extended.Protocol.OciRegistry.UnitLibrary` pulls a manifest by tag or digest and a blob by
digest, pushes with POST then PUT, asks for a token on a Bearer challenge with Basic credentials and
the `repository:<name>:push,pull` scope, and uses neither content discovery nor deletion. Docker,
containerd, ORAS and skopeo send the rest: chunked `PATCH` uploads, cross-repository mounts, the
referrers API, the tag list and deletes, so the server answers them.

## Decision

### 1. The endpoints served, and the one answer for any other (Dist "Endpoints", "Conformance")

Every endpoint of Dist's table is served, so the server claims all four categories, Pull, Push,
Content Discovery and Content Management (Dist "Conformance": a claimed category is served whole):

| ID | Request | Success | Failures sent | Task |
| --- | --- | --- | --- | --- |
| end-1 | `GET /v2/` | `200`, body `{}` | `401` (decision 4) | BL-436 |
| end-2 | `GET`, `HEAD /v2/<name>/blobs/<digest>` | `200` (`206` for one range) | `404` | BL-436 |
| end-3 | `GET`, `HEAD /v2/<name>/manifests/<reference>` | `200` | `404` | BL-436 |
| end-4a | `POST /v2/<name>/blobs/uploads/` | `202` | `404` | BL-437 |
| end-4b | `POST /v2/<name>/blobs/uploads/?digest=<digest>` | `201` | `404`, `400` | BL-437 |
| end-5 | `PATCH /v2/<name>/blobs/uploads/<reference>` | `202` | `404`, `416` | BL-437 |
| end-6 | `PUT /v2/<name>/blobs/uploads/<reference>?digest=<digest>` | `201` | `404`, `400` | BL-437 |
| end-7 | `PUT /v2/<name>/manifests/<reference>` | `201` | `404`, `400`, `413` | BL-437 |
| end-8a, end-8b | `GET /v2/<name>/tags/list[?n=<n>&last=<tag>]` | `200` | `404` | BL-436 |
| end-9 | `DELETE /v2/<name>/manifests/<reference>` | `202` | `404`, `400` | BL-437 |
| end-10 | `DELETE /v2/<name>/blobs/<digest>` | `202` | `404` | BL-437 |
| end-11 | `POST /v2/<name>/blobs/uploads/?mount=<digest>&from=<other>` | `201`, or `202` when not mounted | `404` | BL-437 |
| end-12a, end-12b | `GET /v2/<name>/referrers/<digest>[?artifactType=<type>]` | `200` | `404`, `400` | BL-436 |
| end-13 | `GET /v2/<name>/blobs/uploads/<reference>` | `204` | `404` | BL-437 |

Beside the table, two requests every common client sends:

- **`DELETE /v2/<name>/blobs/uploads/<reference>`** cancels an upload session: `204`, its staged
  bytes removed; an unknown session `404 BLOB_UPLOAD_UNKNOWN`. Dist does not list it; Docker and
  containerd send it to abandon a push, and answering it frees the session before decision 7's
  idle expiry.
- **`GET /token`**, the token endpoint (decision 4).

How a path is read:

- `<name>` is everything between `/v2/` and the last `/blobs/`, `/manifests/`, `/tags/list` or
  `/referrers/` segment, so a name may itself hold a segment such as `blobs` wherever decision
  2.2 allows it. The path is percent-decoded once; a `<name>` that does not match
  Dist's grammar `[a-z0-9]+((\.|_|__|-+)[a-z0-9]+)*(\/[a-z0-9]+((\.|_|__|-+)[a-z0-9]+)*)*`, or is
  longer than 255 bytes (the bound Dist says many clients impose on host, `/` and name together;
  Surl bounds the name alone), is **`400 NAME_INVALID`**.
- A manifest `<reference>` is a **digest** when it holds a `:` (decision 3's grammar), else a
  **tag**, which must match Dist's `[a-zA-Z0-9_][a-zA-Z0-9._-]{0,127}`. A tag that does not is `404
  MANIFEST_UNKNOWN` on a read and `400 MANIFEST_INVALID` on a write (Dist's codes have no tag code;
  the manifest's address is what is invalid).
- **Every other request** - another path under `/v2/`, a path outside `/v2/` and `/token`, a
  method an endpoint does not list - is answered after authentication (decision 4), so an
  unauthenticated peer learns nothing about which requests exist: a path no endpoint matches is
  **`404 UNSUPPORTED`**, an endpoint's path with another method **`405 UNSUPPORTED`** with an
  `Allow` field naming the endpoint's methods (RFC 9110 section 15.5.6).
- **Response headers.** Every answer carries `Docker-Distribution-API-Version: registry/2.0`. Dist
  names it only as a legacy header a client may meet and binds no registry to it; Docker's client
  still reads it from `/v2/` to recognise a version 2 registry, it costs one line, and it says
  nothing a probing peer could not learn from the path grammar. No `Warning` field is sent (Dist
  "Warnings": a registry MAY).

### 2. Storage: the repository folder (ADR-0100 decision 2.4, applied)

#### 2.1 The layout

- **Repository `a/b` = folder `a/b`** of the one shared `ContentStore`, resolved through
  `IContentShareLookup` (ADR-0100 decision 2.6) on its first segment, holding an OCI Image Layout
  (Image "OCI Image Layout Specification"):
  - `oci-layout`, `{"imageLayoutVersion":"1.0.0"}`; a folder is a repository exactly when it holds
    this file;
  - `blobs/<algorithm>/<hex>` for every blob and every manifest, by its digest (decision 3);
  - `index.json`, an image index (`"schemaVersion":2`, `"mediaType":"application/vnd.oci.image.index.v1+json"`)
    whose `manifests` hold one descriptor (`mediaType`, `digest`, `size`, and `artifactType` when
    the manifest has one) **per tag**, annotated `org.opencontainers.image.ref.name` = the tag, and
    one descriptor **without** that annotation for each manifest pushed that no tag names. Every
    manifest pushed is therefore listed, which is what keeps it alive for any layout tool's garbage
    collector and what decision 5.3's referrers walk reads.
- **What a manifest is.** A digest names a manifest in a repository exactly when `index.json` lists
  it, or an image index `index.json` lists names it among its `manifests` (the per-platform
  manifests of a multi-platform image a layout tool wrote). Its `Content-Type` is the listing
  descriptor's `mediaType`. Every other file under `blobs/` is a blob. So the media type needs no
  entry in ADR-0100's object metadata index: the layout records it.
- **`index.json` is rewritten whole** through `index-<guid>.json` beside it and `MoveFileReplacing`
  after every change (ADR-0050 decision 7's pattern), one writer at a time per repository, so a
  reader over any protocol sees the old or the new index, never half of one. An `index.json` that
  does not parse is read as an empty index and noted with `IExchangeLog.Note`; the next write
  replaces it.
- **Pushed content survives a restart** whenever `--directory` names a folder: the layout is the
  state, and nothing else is kept on disk. Without `--directory` the content store is in memory
  and so is the registry. Upload sessions (decision 7) and tokens (decision 4) are in memory only
  and do not survive; a client whose session or token is gone is answered `404
  BLOB_UPLOAD_UNKNOWN` or `401` and starts again, as Dist and Token expect of a client.
- **A layout another tool wrote**, or one copied in over FTP, SFTP, SMB or HTTP, is served as it
  stands. A blob or manifest file is served only after its bytes hash to the digest its name
  claims; the check runs on the first read and its result is cached in ADR-0100's object metadata
  index against the file's stamp (size and last-write time), so a file replaced by another protocol
  is checked again. One that does not match is `404 BLOB_UNKNOWN` or `MANIFEST_UNKNOWN` and noted
  (ADR-0100 decision 2.4).

#### 2.2 Names the store refuses (ADR-0100 decision 2.2's OCI row)

- Dist's `<name>` grammar admits no empty segment, no `.` or `..`, no `\`, `:` or control
  character, no trailing dot and no leading dot, so `.surl` is never a repository. A segment that
  is a reserved device name (`con`, `nul`, `aux`, …) is still refused by
  `RequestPathSegments.CheckSegment`: **`400 NAME_INVALID`** on a write, `404 NAME_UNKNOWN` on a
  read.
- **Conflicts**, each `400 NAME_INVALID` on a write and `404 NAME_UNKNOWN` on a read: a segment of
  the name that is a file; a name that would put a repository inside another repository's
  `blobs` folder (`a/blobs` when `a` is a repository); a first push to a folder that exists, is not
  empty and holds no `oci-layout` (Surl does not turn a folder of other files into a repository). A
  repository may hold another repository's folder beside its layout (`library` and
  `library/alpine` are both repositories), as Dist's grammar allows.
- **Creating a repository.** The first successful blob upload, mount or manifest push to a name
  that is no repository creates its folder (and missing parent folders) with `oci-layout`, under
  the write checks of decision 4. A repository is never removed over `oci`; it is a folder, which
  FTP, SFTP or SMB may remove.

#### 2.3 Share access (ADR-0102 decision 2.3)

Every store call names the caller (the token's account, or anonymous) and the scheme `oci`. A
repository whose share the caller may not read is `404 NAME_UNKNOWN`, as one that does not exist;
a write the share forbids is **`403 DENIED`**. The token endpoint grants only the actions the share
allows (decision 4.3), so a well-behaved client meets the second only when access changes during a
token's life.

#### 2.4 Upload staging (ADR-0100 decision 2.3)

An upload session stages its bytes in `<path>/.surl/objects/uploads/oci-<session id>/data`. On
completion the bytes are hashed as they were written, compared with the digest (decision 3), and
moved into `blobs/<algorithm>/<hex>` through the content store's upload session, so a half-finished
blob is never visible to FTP, HTTP or another pull. A mount copies the source repository's blob file
the same way (ADR-0100 decision 2.4: blobs are per repository). At start the server removes every
`oci-` staging folder, because no session survives a restart.

### 3. Digests (Image "Descriptors"; Dist "Push")

- **Grammar.** A digest is `<algorithm>:<encoded>`. The algorithms served are Image's registered
  two: **`sha256`**, encoded as 64 lower-case hexadecimal digits, and **`sha512`**, 128. Both hash
  with the BCL's `SHA256` and `SHA512`.
- **On a read** (end-2, end-3, end-10, end-12), a digest that does not parse, or names another
  algorithm, is `404 BLOB_UNKNOWN` or `MANIFEST_UNKNOWN` (no such content can exist, ADR-0100
  decision 2.2), except on the referrers endpoint, where Dist requires **`400 DIGEST_INVALID`**
  for an invalid digest.
- **On a write**, a `?digest=` or manifest reference that does not parse, or names another
  algorithm, is **`400 DIGEST_INVALID`**; Dist end-4b and end-6 list `400` and no other
  algorithm can be checked.
- **Blob uploads** (end-4b, end-6, the last `PUT` of a chunked upload): the bytes received are
  hashed with the digest's algorithm while they are staged; a mismatch is **`400 DIGEST_INVALID`**,
  the staged bytes are removed and the session ends. A matching blob is stored and answered `201`
  with `Location: /v2/<name>/blobs/<digest>` and `Docker-Content-Digest: <digest>`.
- **Manifest pushes** (end-7): the body is stored byte for byte (Dist: "the exact byte
  representation"). Pushed by digest, its hash with that algorithm must equal the reference, else
  `400 DIGEST_INVALID`. Pushed by tag, its canonical digest is `sha256` of the body. The answer is
  `201` with `Location: /v2/<name>/manifests/<digest>` and `Docker-Content-Digest` equal to the
  reference when it is a digest, else to the `sha256` digest.
- **Pulls** carry `Docker-Content-Digest`: the requested digest, or for a manifest by tag the digest
  of its `index.json` descriptor.

#### 3.1 What a manifest push checks (Image "Media Types", "Image Manifest", "Image Index"; Dist "Push")

- **`Content-Type`**, parameters ignored (Dist: SHOULD ignore), must be one of
  `application/vnd.oci.image.manifest.v1+json`, `application/vnd.oci.image.index.v1+json`,
  `application/vnd.docker.distribution.manifest.v2+json` or
  `application/vnd.docker.distribution.manifest.list.v2+json` (the Docker forms Image's "Media
  Types" compatibility matrix names), else `400 MANIFEST_INVALID`. Docker's schema 1 is refused
  with it: Docker itself retired it.
- **The body** must be a JSON object with `"schemaVersion":2`, and a `mediaType`, when present,
  equal to `Content-Type`; else `400 MANIFEST_INVALID`. An image manifest needs `config` and
  `layers`, an index `manifests`, each descriptor with a `mediaType`, a valid `digest` and a
  non-negative `size`; else `400 MANIFEST_INVALID`. The JSON is read with `System.Text.Json`'s
  `Utf8JsonReader` to a depth of 64.
- **References.** An image manifest's `config` and every `layers` blob, and an index's every
  `manifests` entry, must already be in the repository, else **`400 MANIFEST_BLOB_UNKNOWN`**, one
  error per missing digest in the body (Dist: a registry MAY reject with one or more). A layer
  descriptor with `urls` (a non-distributable layer) is not required to be present.
- **`subject`** is never required to exist (Dist: MUST initially accept a manifest with a
  `subject`). A manifest with a `subject` is answered with **`OCI-Subject: <subject digest>`**,
  since the referrers API is served (Dist "Pushing Manifests with Subject").
- **Tags.** A push by tag adds or moves that tag's descriptor in `index.json`; the manifest it
  pointed at stays as an untagged descriptor if nothing else names it. A push by digest adds an
  untagged descriptor unless one already lists the digest.

### 4. Authentication (Token; Dist end-1; ADR-0100 decision 4.1)

#### 4.1 What is required

- **By default every `/v2/` request carries `Authorization: Bearer <token>`** with a token this
  server issued. A request with none is **`401 UNAUTHORIZED`** with the challenge
  `WWW-Authenticate: Bearer realm="<realm>",service="surl",scope="repository:<name>:<actions>"`
  (Token "How to authenticate"), `<actions>` being `pull` for a read, `pull,push` for a write and
  `delete` for a delete, and no `scope` on `/v2/` itself.
- **`--allow-anonymous`** serves `/v2/` and every read (end-1, end-2, end-3, end-8 and end-12; not
  end-13, which belongs to a write session) without a token, as the anonymous caller. A write without
  a token is still the `401` challenge above, so the client fetches a token and an anonymous one
  is never granted `push`. A request that does carry a token is checked even then; a bad token is
  not served as anonymous.
- **Writes** - every upload request, end-7, end-9, end-10, end-11, end-13 and the session
  cancel - need `--allow-uploads` (ADR-0006 section 2) and a token granting `push` (or `delete`
  for end-9 and end-10).
- **Basic directly on `/v2/`** is not accepted: Token's flow is the only login, and a client that
  sends Basic is answered the Bearer challenge.
- **Order:** request head and limits, then the path (`/token` or not), then authentication, then
  the endpoint, the name, the repository, the share access and the operation.

#### 4.2 The realm and the service

- **Realm** = `<scheme>://<authority>/token`: `https`, or `http` under `--oci-plaintext` (decision
  6). The authority is the listen URL's host when it is a DNS name, else the local address the
  connection arrived on, with the bound port; never the request's `Host` field, because a realm
  taken from it would let a peer's `Host` send another client's password elsewhere.
- **Service** = `surl`. Token says only that the client echoes it; a fixed value works behind any
  host name. A token request naming another `service` is `400 UNSUPPORTED`.

#### 4.3 The token endpoint (`GET /token`)

- **Request** (Token "Requesting a Token"): `GET /token?service=surl&scope=<scope>[&scope=<scope>…]`
  with optional `account` (ignored: the Basic name is the account) and `client_id`, and
  `Authorization: Basic` (RFC 7617), checked through `IHttpAuthenticationSession`'s Basic method
  against the one account set (ADR-0100 decision 4.1). At most 16 `scope` parameters; each is
  `repository:<name>:<action>[,<action>…]` with actions `pull`, `push`, `delete` or `*` (all
  three). A scope of another resource type, a `<name>` off Dist's grammar or an unknown action is
  dropped from the grant, as Token lets a server grant less than asked.
- **Granted** for each scope: `pull` when the account may read the repository's share; `push` and
  `delete` when `--allow-uploads` is on and the account may write it. With no `Authorization`
  and `--allow-anonymous`, an anonymous token granting `pull` only; with neither, `401
  UNAUTHORIZED` and `WWW-Authenticate: Basic realm="surl", charset="UTF-8"`. Bad credentials are
  the same `401` after the failed-login delay ADR-0032 applies.
- **Response** `200`, `Content-Type: application/json`:
  `{"token":"<t>","access_token":"<t>","expires_in":300,"issued_at":"<yyyy-MM-ddTHH:mm:ssZ>"}`
  (Token "Token Response Fields": `access_token` for OAuth 2.0 clients; never less than 60 seconds).
  No `refresh_token` is issued.
- **`POST /token`** (Token's OAuth 2.0 form) is `405 UNSUPPORTED` with `Allow: GET`: containerd and
  Docker fall back to `GET` on it, and one request form keeps one credential path.
- **The token** is opaque: 32 bytes from an injected random source (`RandomNumberGenerator` in
  production), base64url without padding, kept **in memory** with its account, its granted scopes
  and its expiry, 300 seconds after issue on the injected `TimeProvider`. Not signed: the token
  never leaves the server that checks it, so a signature would prove nothing a lookup does not. At
  most 10000 live tokens; expired ones are dropped first, and a request past the bound is `429
  TOOMANYREQUESTS`.

#### 4.4 Each refusal

| Failure | Status, code, challenge |
| --- | --- |
| No token on a request that needs one | `401 UNAUTHORIZED`, Bearer challenge with the scope it needs |
| A token that is unknown or expired, or another `Authorization` scheme | `401 UNAUTHORIZED`, Bearer challenge with `error="invalid_token"` (RFC 6750 section 3.1) |
| A token not granting the repository and action | `403 DENIED`, Bearer challenge with the scope it needs and `error="insufficient_scope"` (RFC 6750 section 3.1) |
| A write while `--allow-uploads` is off, or one the share forbids | `403 DENIED` |
| Token endpoint: no credentials with anonymous off, or bad credentials | `401 UNAUTHORIZED`, `WWW-Authenticate: Basic realm="surl", charset="UTF-8"` |
| Token endpoint: another `service` | `400 UNSUPPORTED` |
| Credentials or a token over plain text without `--allow-plaintext-auth` (decision 6) | `403 DENIED`, message `credentials over plain text are refused` |

Each failed login waits ADR-0032's delay, then answers; the connection stays open.

### 5. Answers: error bodies, pulls, and content discovery

#### 5.1 The error body (Dist "Error Codes")

```
{"errors":[{"code":"MANIFEST_UNKNOWN","message":"manifest unknown"}]}
```

`Content-Type: application/json`, one line, UTF-8. `code` is one of Dist's fourteen; `message` is
fixed per code (ADR-0006 section 3: fixed text), Dist's description lower-cased; `detail` is
omitted (Dist: OPTIONAL), so no client byte is echoed. A `HEAD` error has its status and no body.
The status each code is sent with, from the endpoint table's failure columns where Dist gives one:

| ID | Code | Status | When |
| --- | --- | --- | --- |
| code-1 | `BLOB_UNKNOWN` | 404 | No blob at the digest, its bytes do not match it, a digest unreadable on a read, a blob delete of nothing |
| code-2 | `BLOB_UPLOAD_INVALID` | 400; 416 | A `PATCH` or `PUT` body that cannot be read as the session's next bytes; `416` for a chunk out of order (Dist "Pushing a blob in chunks") |
| code-3 | `BLOB_UPLOAD_UNKNOWN` | 404 | An unknown, expired, finished or another account's session |
| code-4 | `DIGEST_INVALID` | 400 | Decision 3's digest errors |
| code-5 | `MANIFEST_BLOB_UNKNOWN` | 400 | A manifest naming a blob or manifest the repository lacks |
| code-6 | `MANIFEST_INVALID` | 400; 413 | Decision 3.1's checks, a write's invalid tag; `413` for a manifest past 4 MiB (decision 7) |
| code-7 | `MANIFEST_UNKNOWN` | 404 | No manifest at the reference, or a reference unreadable on a read |
| code-8 | `NAME_INVALID` | 400 | Decision 1's grammar, decision 2.2's refusals and conflicts |
| code-9 | `NAME_UNKNOWN` | 404 | No repository, or one the caller may not read |
| code-10 | `SIZE_INVALID` | 400 | A body whose length is not its `Content-Length` or its `Content-Range`'s span |
| code-11 | `UNAUTHORIZED` | 401 | Decision 4.4 |
| code-12 | `DENIED` | 403 | Decision 4.4, decision 2.3, a full repository (decision 7) |
| code-13 | `UNSUPPORTED` | 404; 405; 400 | Decision 1's every other request; another method; a bad `n` (5.2); another token `service` |
| code-14 | `TOOMANYREQUESTS` | 429 | The connection limits, 1000 open upload sessions, 10000 live tokens (decision 7) |

#### 5.2 Pulls and the tag list (Dist "Pull", "Listing Tags")

- **Manifests** (end-3): `200`, the stored bytes, `Content-Type` the descriptor's `mediaType`
  without parameters, `Content-Length`, `Docker-Content-Digest`. `Accept` is read but never
  converts or refuses: the stored manifest is the only representation (Dist defines no `406`, and
  a registry that cannot convert serves what it has). `HEAD` gives the same head, no body.
- **Blobs** (end-2): `200`, `Content-Type: application/octet-stream`, `Content-Length`,
  `Docker-Content-Digest`, `Accept-Ranges: bytes`; one `Range: bytes=` range is `206` with
  `Content-Range`, an unsatisfiable one `416` with no body and `Content-Range: bytes */<size>`
  (RFC 9110 section 15.5.17; Dist gives blob ranges no code), several ranges ignored and the whole
  blob served with `200`. No `307`
  redirect: the registry is its own blob store.
- **Tags** (end-8): `200`, `{"name":"<name>","tags":[…]}`, the tags in ordinal (lexical) order;
  an empty repository lists `[]`. Without `n`, every tag (decision 7 bounds them). `n` is a decimal
  integer 0 to 10000, else `400 UNSUPPORTED`; `n=0` is an empty list with no `Link`. With `last`,
  the list starts after that tag. When tags remain after the `n` returned, `Link:
  </v2/<name>/tags/list?n=<n>&last=<last tag returned>>; rel="next"` (RFC 8288).

#### 5.3 Referrers (Dist "Listing Referrers")

- `200`, `Content-Type: application/vnd.oci.image.index.v1+json`, an image index whose `manifests`
  holds one descriptor for each manifest in the repository's `index.json` whose `subject.digest`
  is the requested digest: `mediaType`, `digest`, `size`, `artifactType` (the manifest's own, else
  an image manifest's `config.mediaType`, else omitted, Dist's rule) and the manifest's
  `annotations`. Manifests tagged with the referrers tag schema (`<alg>-<hex>`) that are image
  indexes contribute their entries too (Dist "Enabling the Referrers API").
- An unknown subject is `200` with an empty `manifests`, never `404` (Dist: MUST NOT); an unknown
  repository is `404 NAME_UNKNOWN`; an invalid digest `400 DIGEST_INVALID`.
- **`artifactType`** filters on that value; the answer then carries `OCI-Filters-Applied:
  artifactType`.
- Ordered by digest, ordinal. Never paged: a repository's `index.json` holds at most 10000
  descriptors (decision 7), so the answer is one index and no `Link` is needed.

#### 5.4 Uploads (Dist "Push")

- **Opening** (end-4a, end-11 not mounted): `202`, `Location: /v2/<name>/blobs/uploads/<id>`
  (relative, as Dist allows), `Range: 0-0`, `Docker-Upload-UUID: <id>`, no
  `OCI-Chunk-Min-Length` (no minimum). `<id>` is a version 4 UUID from `RandomNumberGenerator`
  (Dist: the location MUST contain a UUID). The session belongs to the account that opened it.
- **`PATCH`** (end-5): with `Content-Range: <start>-<end>` the chunk must start at the bytes held
  so far, else **`416 BLOB_UPLOAD_INVALID`**, and its length must be `<end> - <start> + 1` and the
  `Content-Length`, else `400 SIZE_INVALID`; without `Content-Range` (streamed, as Docker sends)
  the whole body is appended. `202`, `Location`, `Range: 0-<last byte offset held>`.
- **Status** (end-13): `204`, `Location`, `Range` as `PATCH` answers it. An empty session's
  `Range` is `0-0` in every answer, as the distribution reference implementation sends it; the
  ambiguity is Dist's own.
- **Closing** (end-6): the `PUT` may carry a last chunk (with or without `Content-Range`, as
  `PATCH`); decision 3 checks the digest.
- **Single POST** (end-4b): the body is the whole blob; `201` as decision 3.
- **Mount** (end-11): when `from` names a repository the caller may read (its token grants `pull`
  on it) holding the blob, it is copied and answered `201` with `Location` and
  `Docker-Content-Digest`; otherwise `202` and a new session (Dist: SHOULD). Without `from`, never
  a cross-mount: searching other repositories would tell the caller a blob exists where it may not
  look.
- **Deletes** (end-9, end-10): a manifest by digest removes its file and every `index.json`
  descriptor of that digest; by tag, that tag's descriptor only (tag deletion is served); a blob,
  its file. Each `202`. Nothing checks whether a deleted blob is still referenced, as Dist's
  "Deleting Blobs" asks nothing of it.

### 6. TLS

- **`oci` is HTTPS**: implicit TLS from the first byte on port 443 (ADR-0083 decision 7), wrapped
  in `ImplicitTlsSchemeServer` as `https` is (ADR-0010). TLS 1.2 and 1.3 (ADR-0006 section 4);
  `--cert` or `--self-signed` is required, `CertificateProblem` (58) without, as ADR-0032 decides
  for every secure listen URL. ALPN offers `http/1.1` only.
- **`--oci-plaintext`** (no argument, category `oci`, off by default, warned on start as ADR-0032's
  loosening options are) serves `oci` as plain HTTP/1.1, for the `--insecure-registry` and
  `localhost:5000` setups every registry client supports. Basic credentials and Bearer tokens over
  it need `--allow-plaintext-auth` as well (decision 4.4), because unlike SigV4 they are the secret
  itself. BL-439 adds the option with its `OptionArgumentType`, help category and `--aihelp` facts.

### 7. Limits (ADR-0006)

| Limit | How the server applies it | Answer when hit |
| --- | --- | --- |
| `--max-request-head` (100 KiB) | `Surl.HttpServing`'s head reader | `431`, then close (as the HTTP server) |
| `--max-filesize` (100 MiB) | **the largest blob**: a single `POST`, the sum of a session's chunks, and a mount's source; checked against `Content-Length` before a body is read and while it is staged | `413 SIZE_INVALID`, the session ended, then close (the body is not drained) |
| a manifest | 4 MiB, the size Dist says registries SHOULD accept at least | `413 MANIFEST_INVALID` |
| upload sessions | 1000 open per server; a session untouched for **1 hour** is expired and its staging removed, on `TimeProvider`; ended by completion, cancel or expiry | `429 TOOMANYREQUESTS`; `404 BLOB_UPLOAD_UNKNOWN` |
| a repository | 10000 `index.json` descriptors | `403 DENIED`, message `repository is full` |
| a name | 255 bytes | `400 NAME_INVALID` |
| tokens | 10000 live, 300 seconds each; 16 scopes a request | `429 TOOMANYREQUESTS` |
| `--head-timeout` (30 s), `--idle-timeout` (120 s), `--max-time` (3600 s) | `Surl.HttpServing`'s and `Surl.Core`'s | close with no bytes |
| `--max-connections`, `--max-connections-per-address` | `Surl.Core`'s, through the server's refusal writer | `429 TOOMANYREQUESTS` with the JSON body, then close (ADR-0006 section 5: the protocol's own error) |

An hour, not S3's 24: an OCI chunked upload is one client pushing one layer, and a client that has
been silent an hour has gone; S3's multipart uploads are deliberately long-lived. No other new
option is added; the fixed bounds are guards and ADR-0006's options cover what a test harness tunes.

### 8. `--aihelp`

The topic is `oci` (ADR-0083 decision 8). Its about text says the server answers OCI Distribution
Specification v1.1.0 registry requests on the shared content store, keeps each repository as an OCI
Image Layout folder, is judged by the specification and not by upstream curl, requires a token from
its own `/token` endpoint by default, and is HTTPS unless `--oci-plaintext`. BL-439 writes it with
its example.

## Alternatives considered

- **Signed (JWT) tokens.** Rejected: the server that issues the token is the only one that checks
  it, so an in-memory lookup proves the same with no key to manage; the cost, losing tokens on a
  restart, is a re-fetch every client already does on `401`.
- **Accepting Basic on `/v2/` directly.** Rejected: Token's flow is the specified login, and two
  login paths double the refusal table.
- **A private `.surl/oci` area** (BL-435's original plan). Superseded by Stewart's shared-stores
  approval and ADR-0100 decision 2.4.
- **Media types in the object metadata index.** Rejected: `index.json`'s descriptors already hold
  them, in a form any layout tool reads.
- **Refusing deletes** (Dist allows a registry to disable them). Rejected: the conformance suite's
  content-management workflow and ORAS use them, and the standing rule leaves nothing out because
  it is work; `--allow-uploads` already gates them.
- **`401` for an insufficient scope**, as the distribution reference implementation answers.
  Rejected: RFC 6750 section 3.1 makes `insufficient_scope` a `403`, and Dist's `DENIED` says the
  same.
- **Cross-mounting without `from`.** Rejected (decision 5.4).

## Consequences

Good:

- `docker`, `containerd`, `oras` and `skopeo` can push to and pull from `surl oci://`, and what they
  push is an OCI Image Layout that FTP, SFTP, HTTP and SMB serve as plain files.
- Every refusal is one fixed row with Dist's code, so tests pin each byte from the specifications.

Costs and caveats:

- Tokens and upload sessions do not survive a restart.
- A layout written by another tool is hashed once per file stamp before it is served.
- The matrix is `Documentation/Conformance/Extended/OciRegistry.md` (ADR-0083 decision 9).
- `Documentation/Product/Requirements.md` gains no row here, as ADR-0108 to ADR-0114 added none:
  FR-056 and FR-058 cover the server.
