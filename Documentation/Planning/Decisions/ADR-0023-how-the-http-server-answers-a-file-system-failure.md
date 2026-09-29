# ADR-0023 — How the HTTP server answers a file-system failure

- **Status:** Accepted; supersedes the "A path the content store refuses, where nothing exists, or a directory" row and the "A file that shrinks while it is sent" bullet of [ADR-0008](ADR-0008-how-the-http-server-answers-get-head-and-refusals.md), by widening both
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29

## Context

Until BL-060, `HttpRequestResponder` let an exception from the content store escape
`HttpProtocolServer.ServeAsync`. When `ContentStore.GetFileStatus` threw (a file without
read permission, a disk fault), the client got no response at all before the engine
aborted the connection (ADR-0004, section 5). When `ContentStore.CopyFileBytesAsync`
threw after the `200` head was written (the file was deleted or locked in between), the
connection was aborted with no note saying why. BL-060 asked for HTTP's own answer
before any byte is sent, an abort with a note after, and a decision between `404` and
`500` for an unreadable file.

## Decision

The failures answered are `IOException` (with its subclasses, such as
`FileNotFoundException`) and `UnauthorizedAccessException`: what the base class library
throws for a file it cannot read. Any other exception is a defect and still escapes.

| When the content store throws | Answer | Connection afterwards | Log note |
| --- | --- | --- | --- |
| Reading the file's status (`GetFileStatus`), before any byte is sent | `404 Not Found`, empty body, exactly the bytes of a missing file | as persistence decides, as for any 404 | `<method> <target>: 404, <location> could not be read (<exception type>: <message>)` |
| Opening or reading the file after the `200` head was sent | nothing more | aborted | `<location> could not be read after the 200 head was sent (<exception type>: <message>); the connection was aborted.` |

- **404, not 500.** ADR-0006, section 2 says a peer must not be able to tell a hidden or
  refused entry from a missing one; a `500` for a file without read permission would
  confirm that something exists at that path, which is exactly what a 404 for a refused
  path hides. The same argument covers a transient disk fault: the client cannot act on
  the difference, and the verbose log carries the real reason for the operator. A 404 is
  already recorded against pinned upstream curl 8.21.0 (the `not-found-fail` fixture,
  ADR-0008), so no new byte is pinned and no new recording is needed.
- **The reply never carries the exception.** The 404 is the fixed bytes of any 404
  (ADR-0006, section 3); the exception's type and message, which may name a local path,
  go to the exchange log only.
- **After the head, abort.** The `200` head has promised a `Content-Length` that can no
  longer be met, so the connection is aborted, as ADR-0008 already does for a file that
  shrinks; a reset tells curl the transfer failed.
- **A failure of the connection is not the file's.** The copy writes onto the connection
  through `ConnectionWriteStream`, whose `HasFailedWrite` records that a write threw; an
  `IOException` from a failed write is rethrown to the engine, not noted as a file
  failure.

## Consequences

- A GET or HEAD for an unreadable file keeps a persistent connection open, as for a
  missing file, and the next request on it is answered.
- Directory listings, uploads and ranges, when they arrive, answer their own file-system
  failures in their own tasks, by the same rule: fixed text before the head, abort after.
