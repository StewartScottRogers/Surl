# ADR-0133 — How surl http answers Range and stores PUT uploads

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in
  BL-723, which built it.
- **Details:** RFC 9110 sections 13.1.5 (`If-Range`), 14 (range requests) and 9.3.4 (`PUT`);
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) section 4 (a write
  needs a login and `--allow-uploads`); [ADR-0045](ADR-0045-an-aws-signature-version-4-body-is-bound-after-it-is-read.md) (a login bound to the body);
  [ADR-0081](ADR-0081-how-surl-serves-http-2.md) decisions 5 and 7 (an HTTP/2 upload's bounds).

## Context

BL-371 found that against a live `surl http://` or `https://`, pinned upstream curl 8.21.0's `-r 0-9`
was answered `200` with the whole file, and `-T` under `--allow-uploads` with an account was answered
`405`, on HTTP/1.1, HTTP/2 and HTTP/3 alike. `HttpAnswerBody.ContentFile` already carried a
`ContentByteRange`, and every version's writer already sent just that range, so a range answer
needed only the answerer; the content store's `OpenUploadAsync` session already gave an upload that
is committed whole or not at all, as the S3 server uses it.

### What upstream curl sends and accepts

Measured with `Record-CurlExchange.ps1` from the reference win-x64 build (SHA-256
`0E773709…8778`), on 2026-11-05; the fixtures are `Protocols/Http/Surl.Protocol.Http.UnitTests/Fixtures/range-*`
and `put-*`, each with the `response.bin` fed to curl:

| Case | curl sends | Answer fed | curl |
| --- | --- | --- | --- |
| `-r 0-9` | `Range: bytes=0-9` after `Host` | `206`, `Content-Range: bytes 0-9/17`, 10 bytes | writes the 10 bytes, exits 0 |
| `-r -5` | `Range: bytes=-5` | `206`, `Content-Range: bytes 12-16/17`, 5 bytes | writes the 5 bytes, exits 0 |
| `-r 100-200` | `Range: bytes=100-200` | `416`, `Content-Range: bytes */17`, empty | writes nothing, exits 0 |
| `-r 0-1,3-4` | `Range: bytes=0-1,3-4` | `200` with the whole 17-byte file (before Amendment 1) | writes the 17 bytes, exits 0 |
| `-r 0-9 -H "If-Range: <Last-Modified>"` | the field as given, after `Accept` | `206` as for `-r 0-9` | writes the 10 bytes, exits 0 |
| `-u user:secret -T <16 bytes>` (new file) | `PUT`, `Authorization`, `Content-Length: 16`, the body; no `Expect` | `201 Created`, `Content-Length: 0` | exits 0 |
| the same over an existing file | the same | `204 No Content`, no `Content-Length` | exits 0 |

Live, the same curl over HTTP/2 (the static 8.21.0 build ADR-0081 names) and HTTP/3 (the 8.22.0
supplementary build) completes `-r 0-9` with `206` and the first ten bytes, and a 262,144-byte `-T`
under `--allow-uploads` with a login with `201` and the stored file
(`UpstreamCurlFetchesFromSurlOverHttp2Tests`, `UpstreamCurlFetchesFromSurlOverHttp3Tests`).

## Decision

1. **Range** (`HttpRangeSelection`, `HttpRequestAnswerer.FileAnswer`), for a `GET` of a file only; a
   `HEAD` and every other method ignore it (RFC 9110 section 14.2 defines ranges for `GET` alone).
   1. The unit `bytes` is matched without regard to case, with spaces and tabs around the range
      ignored. `first-last`, `first-` and the suffix `-n` are read; a `last` past the end is clamped.
   2. **One satisfiable range** is answered `206 Partial Content` with the file's fields, then
      `Content-Range: bytes <first>-<last>/<length>` before `Content-Length`, and those bytes.
   3. **A range naming no byte** - its first at or past the end, a zero suffix, or any range of an
      empty file - is answered `416 Range Not Satisfiable` with `Date`, `Server`,
      `Content-Range: bytes */<length>` and `Content-Length: 0`. It is not a refusal: the connection
      is kept as a `404` keeps it.
   4. **Ignored, so the whole file is served `200`:** another unit, a syntax RFC 9110 does not give (a
      sign, a non-digit, a last before the first) and a `Range` sent more than once. RFC 9110
      section 14.2 lets a server ignore a `Range`, and curl takes the `200` and exits 0. Several
      ranges are answered `206` as `multipart/byteranges` (Amendment 1).
   5. **`If-Range`**: the range is served only when the field, trimmed, is the `Last-Modified` the
      answer carries, character for character; otherwise the whole file is served `200`. The server
      sends no `ETag`, so an entity-tag `If-Range` never matches (RFC 9110 section 13.1.5).
   6. `Accept-Ranges` is not sent: it is optional (RFC 9110 section 14.3), curl does not need it, and
      adding it would change every `200` already pinned.
2. **PUT** (`HttpRequestAnswerer.Uploads.cs`, `HttpUploadStream`), served only under
   `--allow-uploads`; without it every method but `GET` and `HEAD` is the `405` it was, with
   `Allow: GET, HEAD`. Under it the `405` of `POST`, `DELETE` and the rest says
   `Allow: GET, HEAD, PUT`.
   1. **Who may write** is unchanged: the authentication session judges the request first, and a
      write always needs a login unless `--allow-anonymous` (ADR-0032 section 4). The caller is the
      account the verdict names, so a share's write grants apply ([ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)).
   2. **The upload** is the content store's upload session for the mapped path
      (`ContentUploadOpening(StartsFromExistingBytes: false, CreatesMissingFile: true,
      RefusesExistingFile: false)`): the body is copied into a temporary dot-file beside the target
      and renamed over it only once all of it has arrived, so a body that ends early, goes past the
      limit or fails its login leaves the old file byte for byte.
   3. **Answers:** `201 Created` with `Content-Length: 0` when no file was there, `204 No Content`
      with no `Content-Length` (RFC 9110 section 8.6) when one was replaced; `403 Forbidden` when the
      store does not permit the location (refused, hidden, under `/.surl`, a share the caller may not
      write, or a target that stopped taking a file while the body arrived); `409 Conflict` when the
      location is a directory or its directory does not exist. `403` and `409` are not refusals, and
      leave the body unread, as a `404` does, when nothing needed it read.
   4. **Limits:** a `Content-Length` past `--max-filesize` is the `413` before any body byte, as
      today (ADR-0019, ADR-0081 decision 5). A body that grows past it while it is copied - chunked,
      or an HTTP/2 or HTTP/3 stream with no `content-length` - is a `413` refusal and nothing is
      stored. A body the server cannot read (an unknown transfer coding) is the `400` refusal.
   5. **A login bound to the body** (ADR-0045: AWS Signature Version 4 over the payload) is judged
      after the body is copied into the upload and hashed, and the upload is committed only once the
      body check accepts; refused, the `401` or `403` goes out and nothing is stored. When the upload
      could not be opened the body is still read and judged first, so a client that may not write
      learns nothing about the location.

## Consequences

- `curl -r`, `curl -C -` and `curl -T` work against `surl http://` and `https://` on HTTP/1.x,
  HTTP/2 and HTTP/3, so a test can upload over HTTP and read back over any protocol that shares the
  content store, or the reverse.
- `--aihelp`'s `http` topic still describes the old `405`; BL-889 brings it up to date.
- Several ranges are answered `206` as `multipart/byteranges` (Amendment 1, BL-888).

## Alternatives considered

- **Answer several ranges with only the first.** Rejected: RFC 9110 lets a server ignore or coalesce
  ranges, not drop some of them, and curl would write bytes the user did not ask for as if they were.
- **Buffer a body-bound PUT in memory before opening the upload.** Rejected: `--max-filesize`
  defaults to 100 MiB, and the upload session already keeps the body off the target until it is
  committed.
- **Answer every store refusal `403`.** Rejected: a missing directory or a directory at the target is
  a conflict with the resource's state (RFC 9110 section 15.5.10), which a client can fix by creating
  the directory, not a question of permission.

## Amendment 1 — several ranges as `multipart/byteranges` (BL-888)

Decided by Claude under Stewart's delegation (2026-11-12). It replaces the "several ranges" clause of
decision 1.4.

Measured with `Record-CurlExchange.ps1` against pinned upstream curl 8.21.0 (fixtures
`Fixtures/range-multiple-206` and `Fixtures/range-overlapping-206`): `-r 0-1,3-4` sends
`Range: bytes=0-1,3-4` and, given a canned `206` with `Content-Type: multipart/byteranges;
boundary=SURL_BYTERANGES`, writes the 181-byte multipart body verbatim to stdout and exits 0; `-r 0-4,2-6`
sends `Range: bytes=0-4,2-6` and, given a single-part `206 bytes 0-6/17`, writes `Hello f` and exits 0.
curl neither parses nor rejects the body, so the choices below come from RFC 9110 section 14.6 and
RFC 2046 section 5.1.

1. **Boundary:** the fixed `SURL_BYTERANGES`, so every answer is the same bytes every run (a test
   stand-in must be deterministic).
2. **Part fields:** each part carries the file's own `Content-Type`, then `Content-Range: bytes
   <first>-<last>/<length>`, an empty line and the bytes; parts are joined by CRLF `--SURL_BYTERANGES`
   and the body ends with CRLF `--SURL_BYTERANGES--` CRLF. The answer's `Content-Type` is
   `multipart/byteranges; boundary=SURL_BYTERANGES` and its `Content-Length` is the body's length.
3. **Unsatisfiable ranges** are dropped; if none is left the answer is `416` as for one range.
4. **Coalescing:** ranges that overlap or touch become one range, sorted by first byte (RFC 9110
   section 14.2 permits it). A set that coalesces to one is a plain single-part `206`. Otherwise the
   ranges keep the order sent.
5. **Cap:** more than 100 ranges, or any malformed range, ignores the field and serves the whole file
   `200`, as for one malformed range.
6. **Mechanism:** `HttpAnswerBody.ContentFileParts` (preamble plus range per part, then an epilogue) is
   written by `Http1AnswerWriter`, `Http2StreamAnswerWriter` and `Http3StreamAnswerWriter` alike; the
   writers know nothing of MIME.
