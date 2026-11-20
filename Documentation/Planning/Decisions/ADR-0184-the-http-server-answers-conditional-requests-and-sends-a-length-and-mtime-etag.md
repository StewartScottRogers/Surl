# ADR-0184 — The HTTP server answers conditional requests and sends an `ETag` made from a file's length and modification time

- **Status:** Accepted
- **Date:** 2026-11-20
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-20, in BL-1447. No
  package, no threshold change, no download.
- **Amends:** [ADR-0133](ADR-0133-how-surl-http-answers-range-and-stores-put-uploads.md): its sentence "the server
  sends no `ETag`, so an entity-tag `If-Range` never matches" is replaced by decision 6 below.
- **Rests on:** BL-1446's recordings of upstream curl 8.21.0 (the win-x64 build pinned in
  `UpstreamCurlBuilds.json`, SHA-256 `0E773709...8778`), in `Protocols/Http/Surl.Protocol.Http.UnitTests/Fixtures`
  (README, "Conditional requests"). The Curl port is not evidence (ADR-0003).
- **Work:** BL-1448 (date conditions) then BL-1449 (the `ETag` and entity-tag conditions).

## Context

The server sends `Last-Modified` at second precision and honours only an `If-Range` equal to it. Upstream curl's
`-z`/`--time-cond`, `--etag-compare` and `--etag-save` need a server that answers `If-Modified-Since`,
`If-Unmodified-Since`, `If-None-Match` and sends an `ETag`. What curl does (BL-1446):

- `-z '<D>'` sends `If-Modified-Since: <D>`; `-z '-<D>'` sends `If-Unmodified-Since: <D>`; `-z <file>` sends the
  file's mtime as an HTTP date in `If-Modified-Since` (`time-cond-file-mtime`).
- `--etag-compare <file>` sends `If-None-Match: <file contents>` verbatim (`"abc"`); `--etag-save <file>` writes the
  `ETag` field value plus LF, an empty file when there is none (`etag-save-with-etag`, `etag-save-no-etag`).
- Exit is 0 on a `304` or a `412` and 22 with `--fail` on a `412`; a `304` or `412` leaves stdout empty
  (`time-cond-since-304`, `time-cond-until-412`, `time-cond-until-412-fail`, `etag-compare-304`,
  `etag-compare-412`, `etag-compare-412-fail`). curl itself discards a `200` whose `Last-Modified` fails its own
  time test (`time-cond-since-200-older`, `time-cond-until-200`), so a server that answers `200` where `304`
  was right is survivable but wrong.

## Decision

1. **Scope.** The four validator conditions and an entity-tag `If-Range` are evaluated for `GET` and `HEAD` on a
   served file (and by the same answerer over HTTP/1.1, HTTP/2 and HTTP/3). A directory or a missing file keeps its
   answer (`404`, ADR-0008); preconditions are not evaluated on an error answer, and a login (ADR-0180) is judged
   first, so a conditional request never reveals a file to a client that may not read it.
2. **The `ETag`.** Every `200`, `206` and `304` for a file carries `ETag: "<mtime>-<length>"`: a **strong**
   entity-tag, a quoted string of the file's last-write time in UTC ticks and its length in bytes, each in
   lower-case hexadecimal (`"8de1a2b3c4d5e6f-11"`). It is computed from the `ContentFileStatus` the answerer already
   reads (`LastModifiedUtc.UtcTicks` and `Length`) and never reads the body. It is not clamped to the clock the way
   `Last-Modified` is. Every status that carries `ETag` also carries `Last-Modified`.
3. **When it changes, and the source.** It changes whenever the file's length or last-write time changes, so a
   write through any protocol that shares the content store (a `PUT`, an SFTP, FTP or SMB upload) gives the next
   request a new `ETag`; the same file has the same `ETag` over every HTTP version. The HTTP server does **not** use
   `ObjectMetadataIndex`: that index holds the object-storage servers' MD5 ETags, which cost a full read on first
   request, are tied to object keys the HTTP server does not use, and would need a `Surl.Content` entry point for a
   value no test can distinguish. No `Surl.Content` task is needed. The ETag is deliberately not a content hash: a
   test cannot tell them apart and a stat is cheap on a large file. A rewrite that restores the same length and
   the same tick count would not change it; the 100-nanosecond tick and the injected `TimeProvider`/file system
   make that a deliberate-collision case only.
4. **Evaluation order** (RFC 9110 section 13.2.2), after the login and the method check:

   | Step | Condition | Result when it decides |
   | --- | --- | --- |
   | 1 | `If-Match` present and no listed tag strongly matches the current `ETag` (`*` matches any existing file) | `412` |
   | 2 | else `If-Match` absent and `If-Unmodified-Since` is a valid date and `Last-Modified` is later than it | `412` |
   | 3 | `If-None-Match` present and a listed tag weakly matches (`*` matches any existing file) | `304` for `GET`/`HEAD`; `412` for any other method |
   | 4 | else `If-None-Match` absent and `If-Modified-Since` is a valid date, the method is `GET`/`HEAD`, and `Last-Modified` is not later than it | `304` |
   | 5 | `Range` with `If-Range` (decision 6) | `206` or the whole `200` as ADR-0133 |
   | 6 | none decided | `200` |

   A failed step 3 or 4 test falls to the next step, never to an error. Dates compare at second precision, the
   precision `Last-Modified` is sent at, against the `Last-Modified` the answer carries (so a clamped
   future mtime compares as it is sent). A condition with a value that is neither an accepted HTTP date (IMF-fixdate,
   RFC 850, asctime; RFC 9110 section 5.6.7) nor, for tags, a list of entity-tags, is **ignored**, as RFC 9110
   section 13.1.3 and 13.1.4 say for a non-date: `If-Modified-Since: nonsense` is a `200`. A `HEAD` evaluates the
   same conditions as a `GET`.
5. **The answers.**

   | Status | Fields sent | Body | Connection |
   | --- | --- | --- | --- |
   | `304 Not Modified` (RFC 9110 15.4.5) | `Date`, `Server`, `ETag`, `Last-Modified`, plus the challenge fields a `200` would carry; no `Content-Length`, `Content-Type` or `Content-Range` | none, even for `GET` | persists |
   | `412 Precondition Failed` (RFC 9110 15.5.13) | `Date`, `Server`, `Content-Length: 0` | none | persists; a request body still announced is drained as for any non-refusal answer |
   | `200`/`206` | unchanged from ADR-0133, plus `ETag` | as before | as before |

   The `304` sends no `Content-Length` (RFC 9110 15.4.5: a `304` has no content and may omit the field a `200`
   would carry). Upstream curl treats a `304` as bodiless whatever the length field says, so the recordings that
   used `Content-Length: 0` (`time-cond-since-304`, `etag-compare-304`) and the server's answer produce the same
   curl result: exit 0, empty stdout. BL-1448 pins the bytes with a recording of a `304` without the field.
6. **`If-Range`** (amends ADR-0133). The `Range` is honoured when the `If-Range` is either the exact `Last-Modified`
   the answer carries (as ADR-0133) or a **strong** entity-tag equal to the current `ETag`. A weak tag (`W/"..."`),
   a different tag, or any other value gets the whole file `200`, as RFC 9110 section 13.1.5 says. `If-Range` with
   no `Range` is ignored.
7. **`PUT` under `--allow-uploads`.** `If-Match` and `If-None-Match` are honoured, because a test uses them for
   optimistic concurrency and create-only writes: with `If-Match` the current `ETag` must match (a missing file
   matches nothing, so `If-Match: *` on a missing file is `412`); `If-None-Match: *` is `412` when the file
   exists; any other `If-None-Match` is `412` when a listed tag matches. The check runs after the login and
   before the upload session is committed, so a refused `PUT` leaves the file as it was; a body already read is
   discarded. The date conditions are honoured on a `PUT` too (`If-Unmodified-Since`: `412` when the file is
   newer; `If-Modified-Since` is ignored on a method other than `GET`/`HEAD`, RFC 9110 section 13.1.3). A `201`
   or `204` for a `PUT` carries the new file's `ETag`. This ships with BL-1449, after `GET`/`HEAD`.
8. **No off switch and no option.** Validators and preconditions are the protocol's own, not a loosening: nothing
   here widens what a peer may do, so there is no new command-line option or `--aihelp` option row; `--aihelp http`
   prose and example change in the BL-1448 and BL-1449 diffs.

## Work split

- **BL-1448** - `If-Modified-Since` and `If-Unmodified-Since` (decision 4 steps 2 and 4, decision 5), with the
  invalid-date-ignored rule, in `HttpRequestAnswerer`. It sends `Last-Modified` on the `304`.
- **BL-1449** - the `ETag` (decisions 2 and 3), `If-Match`, `If-None-Match`, the entity-tag `If-Range` (decision 6)
  and the `PUT` conditions (decision 7). It needs no `Surl.Content` change, so no further task is filed and its
  `depends-on` stays BL-1448.
- Both replay BL-1446's recorded requests and answers as fixtures and, in `Integration`, run pinned curl `-z`,
  `--etag-save` and `--etag-compare` against the live server.

## Consequences

- `curl --etag-save e.txt` saves `"<mtime>-<length>"` plus LF; `--etag-compare e.txt` then gets `304` until any
  protocol rewrites the file.
- A tool that expects a content-hash ETag (an S3-style MD5) will not find one on the HTTP server, by choice.
