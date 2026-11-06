# ADR-0142 — How Surl names a file's media type from its extension

- **Status:** Accepted
- **Date:** 2026-11-06
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-06, in BL-686.
- **Supersedes:** the `Content-Type: application/octet-stream` of [ADR-0008](ADR-0008-how-the-http-server-answers-get-head-and-refusals.md)'s
  "GET or HEAD of a file" row, which said it held "until `Surl.Content` has media types".
- **Acts on:** [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decision 2.3: a file with no stored `Content-Type` is read over the object servers with the type
  "named by `Surl.Content`'s media types from the extension (as HTTP names it), else
  `application/octet-stream`".

## Context

Until now every file the HTTP server answered carried `Content-Type: application/octet-stream`, and
`ObjectMetadataIndex.DefaultContentType` gave every object stored without one the same. A test that
uses Surl as a stand-in for a web site or a bucket sees `text/html` and `image/png` from the real
thing; a browser-like client or an SDK that branches on the type saw only octet-stream from Surl.

Measured first (ADR-0003): pinned upstream curl 8.21.0 (win-x64 reference build) fed a `200` with
`Content-Type: text/plain; charset=utf-8` and with `Content-Type: image/png` by
`Record-CurlExchange.ps1`, run as `-s -o NUL -w [%{content_type}]`, exited 0 and wrote
`[text/plain; charset=utf-8]` and `[image/png]`: curl reports the field verbatim and does nothing
else with it for a download, so any well-formed media type completes the exchange. The HTTP
fixtures that serve `file.txt` (`get-file`, `head-file`, `expect-continue-get`,
`digest-401-then-200`, and the four `range-*` cases) were re-recorded with `Content-Type: text/plain`
on 2026-11-06 with the same build; each exited 0 as before.

## Decision

1. **One table, in `Surl.Content`.** `ContentMediaTypes.Of(path)` names a file's media type from the
   extension of the last segment of `path` (split at `/`, `\` and the platform's separator): the text
   after its last `.`, compared ignoring case, when that `.` is not the name's first character. A
   name with no extension, a dot-file such as `.profile`, a name ending in `.`, and every extension
   not in the table are `ContentMediaTypes.Unknown`, `application/octet-stream` (RFC 9110, section
   8.3). `ContentMediaTypes.Table` exposes the table. The table holds 36 extensions:

   | Extensions | Media type | Source |
   | --- | --- | --- |
   | `html`, `htm` | `text/html` | IANA media types |
   | `txt` | `text/plain` | RFC 2046 |
   | `css` | `text/css` | RFC 2318 |
   | `csv` | `text/csv` | RFC 4180 |
   | `js`, `mjs` | `text/javascript` | RFC 9239 |
   | `md` | `text/markdown` | RFC 7763 |
   | `json` | `application/json` | RFC 8259 |
   | `xml` | `application/xml` | RFC 7303 |
   | `yaml`, `yml` | `application/yaml` | RFC 9512 |
   | `pdf` | `application/pdf` | RFC 8118 |
   | `zip` | `application/zip` | IANA media types |
   | `gz` | `application/gzip` | RFC 6713 |
   | `tar` | `application/x-tar` | no registered type; the common one |
   | `wasm` | `application/wasm` | IANA media types |
   | `png` | `image/png` | IANA media types |
   | `jpg`, `jpeg` | `image/jpeg` | IANA media types |
   | `gif` | `image/gif` | RFC 2046 |
   | `svg` | `image/svg+xml` | IANA media types |
   | `webp` | `image/webp` | RFC 9649 |
   | `avif` | `image/avif` | IANA media types |
   | `bmp` | `image/bmp` | RFC 7903 |
   | `ico` | `image/vnd.microsoft.icon` | IANA media types |
   | `mp3` | `audio/mpeg` | RFC 3003 |
   | `wav` | `audio/wav` | the type browsers send; `audio/vnd.wave` (RFC 2361) is rarely recognised |
   | `ogg`, `oga` | `audio/ogg` | RFC 5334 |
   | `mp4` | `video/mp4` | RFC 4337 |
   | `webm` | `video/webm` | the WebM project's registration |
   | `woff`, `woff2`, `ttf`, `otf` | `font/woff`, `font/woff2`, `font/ttf`, `font/otf` | RFC 8081 |

   The table is the common web and data formats a test is likely to serve, not a copy of a system
   MIME database: it is the same on every platform, so a test gets the same answer on Windows, Linux
   and macOS. It only grows; an extension once named keeps its type.
2. **No `charset`.** A `text/*` type carries no `charset` parameter: the content store does not know
   a file's encoding, and RFC 9110, section 8.3.2 lets the parameter be left out.
3. **HTTP** answers a file's `GET` and `HEAD` (`200`, `206`) with `Content-Type` from
   `ContentMediaTypes.Of` on the mapped file's name. Every other row of ADR-0008 is unchanged.
4. **The object servers.** `ObjectMetadataIndex` answers an object with no stored `Content-Type` with
   `ContentMediaTypes.Of(key)`. The S3, Azure Storage and Cloud Storage readers' own defaults for a
   file in a share (which the index does not key) use the same table on the file's name, so a file
   answers the same type over HTTP and over every object server. `ObjectMetadataIndex.DefaultContentType`
   stays, now meaning the type of a file whose extension names none (`ContentMediaTypes.Unknown`).

## Consequences

- A `Content-Type` sent at upload still wins over the table, as ADR-0100 decision 2.3 says.
- The HTTP proxy's FTP answers and the OCI registry's blobs keep `application/octet-stream`: neither
  names a file in the content store by its extension (an FTP fetch through a proxy, a blob by digest).
- `surl --aihelp http` still says a file is answered `application/octet-stream`; BL-686 files the
  follow-up that corrects it, because `Surl.Cli` was held by another lane.
