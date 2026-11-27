# ADR-0167 — What a reset, snapshot and restore do with in-progress object uploads and the OCI and MCP servers' tokens

- **Status:** Accepted
- **Date:** 2026-11-15
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-15, in BL-1216.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) (decision 3: reset, snapshot, restore; its
  "live sessions" rule), [ADR-0158](ADR-0158-how-the-redis-keyspace-the-ldap-directory-and-the-dns-zones-are-seeded-reset-snapshotted-restored-and-observed.md)
  (decision 2's registration seam, `IControllableStore`), [ADR-0159](ADR-0159-which-broker-held-state-a-reset-snapshot-and-restore-of-the-message-bus-cover.md)
  (the three classes, decisions 3 and 4), ADR-0032 (secure by default), ADR-0006 section 5 (upload sessions)
  and ADR-0003.
- **Amends:** ADR-0124 decision 3 (a whole-instance reset, snapshot and restore also cover the state below, and
  a removed account's tokens stop working at once).
- **Acted on by:** BL-1228 to BL-1233 (decision 6).
- **Amended (2026-11-27, BL-1857):** decision 3's order across stores: the sessions and uploads entries follow the
  accounts, and the uploads entry is last; see Amendment 1.

## Context

ADR-0124 decision 3 and ADR-0159 cover the shared stores and the brokers' state. Five extended servers keep state
of their own in memory, per server instance, that no reset reaches. After `ResetAsync` an upload begun in the
previous test can still be completed into the fresh store, its open upload still counts toward a cap of 1000, and
a token issued to an account the reset removed is still accepted. Each class was read for this audit.

Where the staged bytes live: every upload goes through `ContentUploadSession` (`Surl.Content.UnitLibrary`), which
writes a dot-file beside the target in the content store's file system and renames it over the target on commit;
disposing the session without a commit deletes the file (ADR-0006 section 5). Staged bytes are therefore
content-store temporary files, not server memory; the server classes hold only the sessions and their bookkeeping.

## Decision

### 1. The audit

Classes as ADR-0159 decision 1: **configured**, **test data**, **live session state**.

| Server | State | Held how | Class |
| --- | --- | --- | --- |
| S3 | `S3MultipartUploads`: open multipart uploads (max 1000, 24 h idle) | one `ContentUploadSession` each | test data tied to the content store |
| Azure Storage | `AzureStagedBlocks`: uncommitted blocks and their session (per blob) | one `ContentUploadSession` per blob while blocks are staged | test data tied to the content store |
| Azure Storage | `AzureStagedBlocks.committed` and `committedFile`: the last committed block list, honoured only while the file's status equals `committedFile` | memory | test data tied to the content store |
| Google Cloud Storage | `GcsStagedUploads`: resumable and XML multipart uploads (max 1000, 24 h idle) | one `ContentUploadSession` each | test data tied to the content store |
| OCI registry | `OciUploadSessions`: blob upload sessions (max 1000, 1 h idle), owned by an account name | one `ContentUploadSession` each | test data tied to the content store |
| OCI registry | `OciTokenBook`: bearer tokens (max 10000, 300 s) with the account each was issued to | memory | live session state |
| MCP server | `McpAuthorizationGrants` clients: dynamic client registrations (never expire) | memory | test data |
| MCP server | `McpAuthorizationGrants` codes, access tokens and refresh tokens (10 min, 1 h, 24 h) | memory | live session state |
| MCP server | `McpHttpSessionTable`: `Mcp-Session-Id` sessions (max 1000, 30 min idle) | memory | live session state |
| all five | caps, lifetimes, endpoint path, scopes, OAuth metadata | start-up settings | configured (untouched) |

No row is configured state: each exists because a client or a test did something.

### 2. What reset, snapshot and restore do

- **Upload sessions** (S3, Azure uncommitted blocks, GCS, OCI). Reset aborts every open upload: each session is
  disposed without a commit, so its temporary file is deleted and its slot freed. A client that then sends a
  part, a block or a commit gets the server's ordinary "no such upload" answer, as for an expired upload today.
  Snapshot holds none: a snapshot is of contents (ADR-0124), a partial upload is not content, and the content
  snapshot already holds committed objects. Restore aborts every open upload as reset does, because the content
  it was staging for was just replaced.
- **Azure committed block list.** Reset and restore drop it. It is a record beside the file, honoured only while
  the file's status matches, so after the content store is replaced it is dead weight or a stale list. Get Block
  List then answers as for a blob never block-staged (`CommittedFor` returns the empty list).
- **OCI tokens.** Reset and restore revoke every token; the client's next call gets `401` with the ordinary
  challenge and fetches a new token, as after a registry restart. Snapshot holds none: a token is a secret.
- **MCP OAuth.** Reset and restore delete every registered client, authorization code, access token and refresh
  token. Snapshot holds none. A client's next token call is `invalid_client` or `invalid_grant` and it registers
  again.
- **MCP HTTP sessions.** Reset and restore end every session (`McpHttpSession.End`); the next request with its
  `Mcp-Session-Id` is answered `404`, which the MCP specification says makes the client start a new session.
  This is the one place a live session is ended rather than left to run on: ADR-0124's rule covers a connection,
  while an MCP session is a server-side object that cannot be re-validated once its stores change.
- **Caps.** Every upload, token and session slot is free afterwards, so a long run cannot exhaust a cap by
  never finishing uploads.
- **Seeds** never create any of this: a seed writes a finished object.

### 3. How each server learns of it

Each server registers through ADR-0158 decision 2's seam (`IControllableStore`), as
`Amqp0ExchangeStoreRegistration` does, from `Surl.Composition`, never by watching events (ADR-0159 decision 3):

- `StoreKind.Uploads` (new), one registration for each server with uploads, its scheme (`s3`, `azure`, `gcs`,
  `oci`) as the event's name; one `StoreChangedEvent` (`Reset`, `Restored`) each.
- `StoreKind.Sessions` (existing, ADR-0159) for the OCI tokens, the MCP OAuth state and the MCP HTTP sessions.

Order across stores: content, mail, bus, then the server registrations in ordinal order of scheme (uploads before
tokens within a scheme), then accounts. Uploads follow content because their temporary files live in it. These
registrations' snapshots hold nothing (`ByteCount` 0) but still take part, so a restore clears the state.
*The "Order across stores" sentence above is superseded; see Amendment 1.*

### 4. A removed account's tokens stop at once

Yes. A token is judged when it is **used**: an OCI bearer token, and an MCP access or refresh token, issued to an
account is honoured only while that account still exists in the account book at that moment. This covers
`remove_account`, an accounts reset and an accounts restore alike, needs no ordering rule and no callback from the
account book, and needs no sweep. A token issued to no account (anonymous) is unaffected. An MCP HTTP session
opened by a removed account is refused on its next request. This amends ADR-0124's "a removed account's open
sessions run on until they end (a login is judged when it is made)" for these token kinds: a request carrying a
token is a login made afresh each time, so there is nothing to run on.

### 5. `--directory`

Staged uploads sit in the real directory when the content store is on disk. The content store refuses reset,
snapshot and restore there (ADR-0124) and the order puts it first; the upload registrations repeat the refusal
(`InvalidOperationException`, `409` on the control endpoint) so one used without the content store still cannot
delete a file in a real directory. Token and MCP registrations are memory only and have no refusal.

### 6. Acted on by

One task per library that changes, each depending on BL-1216 and, after the first, on BL-1228: BL-1228 the
`StoreKind.Uploads` member, BL-1229 S3, BL-1230 Azure Storage, BL-1231 Google Cloud Storage, BL-1232 the OCI
registry (uploads, tokens and the per-use account check) and BL-1233 the MCP server (OAuth, sessions and the
per-use account check). Each touches its library, its tests and `Application/Surl.Composition.UnitLibrary` with
its tests.

## Alternatives considered

- **Leave uploads to their idle lifetime.** Rejected: 24 h outlasts any test run, and that leak is the audit.
- **Snapshot in-progress uploads.** Rejected: a partial upload has no content, and a restored temporary file would
  outlive the session that owns it.
- **Revoke tokens when the account book publishes a removal.** Rejected: ADR-0159 decision 3 rejects watching
  events, and a restore from a snapshot has no per-account removal event.
- **Keep tokens across a reset.** Rejected: a token naming a removed account would work, and a token naming a
  surviving one is cheap to fetch again.

## Consequences

- A reset between tests leaves no upload, token, client registration or MCP session from the last test, and no
  slot used.
- A removed account is locked out of the OCI registry and the MCP server on its next request.
- Six tasks follow.

## Amendment 1 (2026-11-27, BL-1857): the order of the sessions and uploads entries

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), in BL-1857. It records the order
the code runs; no code changes. The whole order is in
[ADR-0124's amendment of 2026-11-27](ADR-0124-surl-is-a-stand-in-for-tests.md#amendment-2026-11-27-the-order-across-stores-is-thirteen-stores-bl-1857).
This replaces decision 3's "Order across stores" sentence.

- Both entries follow the accounts. The sessions entry comes before the DNS zones, and the uploads entry is last.
- The server registrations of one store kind run as one entry, each still publishing its own event. Neither entry
  runs in ordinal order of scheme:
  - **sessions:** the MQTT 5 sessions, then the OCI tokens, then the MCP OAuth state and HTTP sessions;
  - **uploads:** S3, then Azure Storage, then Google Cloud Storage, then the OCI registry.
- So the OCI tokens are reset before the OCI uploads, not after them. Uploads still follow content, where their
  temporary files live.
