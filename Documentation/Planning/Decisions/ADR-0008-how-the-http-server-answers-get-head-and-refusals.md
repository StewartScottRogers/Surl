# ADR-0008 — How the HTTP server answers GET, HEAD and what it refuses

- **Status:** Accepted; partly superseded by [ADR-0019](ADR-0019-how-the-http-server-enforces-the-hardening-limits.md) (the 431 row, the persistence rule for a request with a body, and how refusals are written) and by [ADR-0023](ADR-0023-how-the-http-server-answers-a-file-system-failure.md) (the 404 row and the shrinking-file rule, widened to file-system failures)
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28

## Context

BL-018 gives `Surl.Protocol.Http.UnitLibrary` its first protocol server,
`HttpProtocolServer`: GET and HEAD for files in the content store over HTTP/1.0 and
HTTP/1.1. The task left five answers to decide: the status for a refused path, a missing
path and a directory; for an unsupported method; for a request head BL-017's reader
cannot parse; and the `Content-Type` sent before `Surl.Content` knows media types. The
answers must follow RFC 9110 and RFC 9112, keep ADR-0006's rule that a peer cannot tell
a hidden or refused entry from a missing one, and be accepted by pinned upstream curl
8.21.0 (ADR-0003).

## Decision

| Request | Status | Fields beyond `Date` and `Content-Length` | Connection afterwards |
| --- | --- | --- | --- |
| GET or HEAD of a file | `200 OK`; the file's bytes for GET, none for HEAD | `Last-Modified`, `Content-Type: application/octet-stream` | as persistence decides (below) |
| A path the content store refuses (`/%2e%2e/x`), where nothing exists, or a directory | `404 Not Found`, empty body | none | as persistence decides |
| `POST`, `PUT`, `DELETE`, `CONNECT`, `OPTIONS`, `TRACE` (RFC 9110) or `PATCH` (RFC 5789) | `405 Method Not Allowed`, empty body | `Allow: GET, HEAD`, `Connection: close` | half-closed |
| Any other method, including a lower-case `get` (methods are case-sensitive) | `501 Not Implemented`, empty body | `Connection: close` | half-closed |
| A malformed request line or header field, whitespace before a colon, or an HTTP/1.1 request without exactly one `Host` (any request with two) | `400 Bad Request`, empty body | `Connection: close` | half-closed |
| A head past the reader's size limit | `431 Request Header Fields Too Large`, empty body (ADR-0006, section 5) | `Connection: close` | half-closed |
| A major version other than 1 | `505 HTTP Version Not Supported`, empty body | `Connection: close` | half-closed |
| The client closes before a whole head arrives | nothing is sent | - | left to the engine's dispose |

- **Refused, missing and directory are all 404.** ADR-0006, section 2 rejects 403 for a
  refused path because it confirms the entry exists, and says a refused listing is
  answered as absent; directory listings are off by default and a later task.
- **405 versus 501** follows RFC 9110, sections 15.5.6 and 15.6.2: a method the server
  knows but the resource refuses is 405 with `Allow`; an unknown one is 501. ADR-0006
  already names 405 with `Allow` for a refused upload.
- **Refusals close.** A refused request may carry a body the server has not read, so the
  next request cannot be found after it; `Connection: close` and a half-close are the
  RFC 9112, section 9.6 way out. A 404 carries no request body on a GET or HEAD, so it
  keeps the connection.
- **Persistence** (RFC 9112, section 9.3): a `close` connection option closes; an
  HTTP/1.1 request otherwise keeps the connection; an HTTP/1.0 request keeps it only with
  `keep-alive`, answered `Connection: keep-alive`. A GET or HEAD that announces a body
  (`Transfer-Encoding`, or a `Content-Length` other than `0`) closes too, because request
  bodies are not read yet.
- **The status line always says `HTTP/1.1`**, the highest version the server speaks
  (RFC 9110, section 2.5), also to an HTTP/1.0 request.
- **`Content-Type: application/octet-stream`** until `Surl.Content` has media types: it
  is what RFC 9110, section 8.3 says a recipient may assume anyway, so sending it says
  nothing false.
- **`Date`** comes from the exchange context's `TimeProvider` in IMF-fixdate.
  **`Last-Modified`** is the file's modification time, but never later than `Date`
  (RFC 9110, section 8.8.2.1).
- **A file that shrinks while it is sent** aborts the connection: its `Content-Length`
  can no longer be met, and a reset tells curl the transfer failed.
- **Schemes** lists only `http` for now; `https` joins when the TLS contract lands, so
  the engine never routes a secure listener to a server that cannot secure it.

Upstream curl 8.21.0 (the pinned win-x64 build) was fed the 200 GET, 200 HEAD (`-I`) and
404 (`--fail`) responses byte for byte before the tests pinned them, and exited 0, 0 and
22; the recordings are in `Surl.Protocol.Http.UnitTests/Fixtures/` (`get-file`,
`head-file`, `not-found-fail`).

## Consequences

- Error bodies are empty; a later task may add a short fixed text body, measured against
  curl first.
- An absolute-form request target (`http://host/path`, RFC 9112, section 3.2.2) is
  served by its path; its authority is not compared with `Host` or the listen URL.
- Still to do, filed as follow-up tasks: draining unread request bytes before a close
  (RFC 9112, section 9.6), a 400 for an invalid `Content-Length` (section 6.3), and an
  answer for a file-system failure instead of letting the exception end the exchange.
- Reading request bodies, ranges, conditional requests, listings, media types and the
  ADR-0006 limits (head timeout, configurable head size) change rows of this table in
  their own tasks; each such change is a new ADR that supersedes the row.
