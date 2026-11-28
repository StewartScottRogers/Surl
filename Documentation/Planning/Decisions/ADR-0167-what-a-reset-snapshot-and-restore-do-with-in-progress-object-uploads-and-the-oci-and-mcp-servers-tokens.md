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
- **Amended (2026-11-27, BL-1896):** the registration seam "Builds on" and decision 3 name (`IControllableStore`)
  is superseded by the one the code has; see Amendment 2, decided by Claude under Stewart's delegation.
- **Amended (2026-11-27, BL-2003):** decision 4: a token issued to a user only the `--user-ldap` directory knows is
  honoured for a bounded time; see Amendment 3, decided by Claude under Stewart's delegation.

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
*The `IControllableStore` sentence that opens this decision is superseded; see Amendment 2.*

### 4. A removed account's tokens stop at once

Yes. A token is judged when it is **used**: an OCI bearer token, and an MCP access or refresh token, issued to an
account is honoured only while that account still exists in the account book at that moment. This covers
`remove_account`, an accounts reset and an accounts restore alike, needs no ordering rule and no callback from the
account book, and needs no sweep. A token issued to no account (anonymous) is unaffected. An MCP HTTP session
opened by a removed account is refused on its next request. This amends ADR-0124's "a removed account's open
sessions run on until they end (a login is judged when it is made)" for these token kinds: a request carrying a
token is a login made afresh each time, so there is nothing to run on.
*A token of a user only the `--user-ldap` directory knows is also honoured, for a bounded time; see Amendment 3.*

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

## Amendment 2 (2026-11-27, BL-1896): how each server's state joins `ComposedStores`

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), in BL-1896. It records the seam
the code has; no code changes. The seam is described in
[ADR-0158 Amendment 3](ADR-0158-how-the-redis-keyspace-the-ldap-directory-and-the-dns-zones-are-seeded-reset-snapshotted-restored-and-observed.md#amendment-3-2026-11-27-bl-1896-how-a-store-joins-composedstores).

This supersedes the `IControllableStore` text of "Builds on" and decision 3: no such interface exists. In
`Surl.Composition`, each state decision 3 lists has a `*StoreRegistration` class that builds a
`ComposedStoreRegistration` over the composed servers, or none when there is no such server:

| `StoreKind` | Classes, in the order they run | Listed by (`SurlServerComposition.Start.cs`) |
| --- | --- | --- |
| `Sessions` | `Mqtt5SessionStoreRegistration` (ADR-0159), `OciTokenStoreRegistration`, `McpSessionStoreRegistration` | `ControlledBrokerAndSessionStores` |
| `Uploads` | `S3UploadStoreRegistration`, `AzureStagedUploadStoreRegistration`, `GcsStagedUploadStoreRegistration`, `OciUploadStoreRegistration` | `ControlledUploads` |

`SameKindStoreRegistrations.Combine` joins each row into the one entry `ComposedStores` has for that kind. Decision
3's other rules stand: no server learns of a reset by watching events, each class publishes its own
`StoreChangedEvent`, and the snapshots of this ADR's own classes (all but the MQTT 5 one) hold nothing
(`ByteCount` 0).

## Amendment 3 (2026-11-27, BL-2003): a directory user's OCI and MCP tokens

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), 2026-11-27, in BL-2003 (filed as
BL-1995 and renumbered the same day; the task numbered BL-1995 since then is another one). It amends decision 4 for
a **directory user**: a name that only the `--user-ldap` directory of
[ADR-0149](ADR-0149-how-an-ldap-directory-serves-as-an-optional-account-source.md) knows, and the account book does
not. No upstream curl measurement applies: upstream curl has neither `oci` nor `mcp`. This amendment changes no
code; points 2 to 6 say what the tasks in point 6 are to build.

### 1. What the code does on 2026-11-27 (read from source, not run)

- `AuthenticationPolicy.CheckPasswordLoginAsync` accepts a directory user on a scheme the directory serves, so the
  OCI registry's `/token` issues a bearer token to the name, and the MCP server's `/authorize` and
  `client_credentials` grant accept it.
- At each use `OciAccessCheck` and `McpAuthorizationGrants` ask `IAuthenticationPolicy.NamesAccount`, which asks
  only the account book. The token is therefore refused at once: `401` with `error="invalid_token"` on the
  registry; on the MCP server `401` for an access token and `400 invalid_grant` for a refresh token. Redeeming an
  authorization code asks nothing, so the code still yields tokens that are then refused.

### 2. Issued and honoured

A directory user is issued every token an account-book account is issued, as today. A token issued to a name is
honoured when it is used if **either** holds:

- the account book, as the server's per-scheme view shows it, names it (decision 4, unchanged); or
- **both**: the directory accepted that name's password through `CheckPasswordLoginAsync` of this server's policy,
  and no fault refused that login, less than **one hour** ago on the injected `TimeProvider`; and the account book
  has **never held** an account of that name, on any scheme, since the instance started.

The token's own lifetime and its other checks (grants, resource, `client_id`) still apply. The hour is a constant
of `Surl.Authentication`, not an option, and equals the MCP access token's lifetime. Each accepted directory login
of the name on that policy starts the hour again. Each directory login still publishes its `LoginJudgedEvent` and
takes `refuse-login` (ADR-0149 decision 6); a token check never contacts the directory, publishes no event and
asks the fault plan nothing, so a test sees a refused token only in the server's answer.

| Token | Directory user: issued | Honoured until |
| --- | --- | --- |
| OCI bearer | at `/token`, after an accepted Basic login | its own expiry, 300 s (the hour never cuts it short) |
| MCP access | by the code, refresh and `client_credentials` grants | its own expiry (1 h) or one hour after the name's last accepted directory login, whichever is first |
| MCP refresh | by the code and refresh grants (`client_credentials` gets none, as today) | its own expiry (24 h) or one hour after the name's last accepted directory login, whichever is first |

Refreshing is not a login and does not start the hour again, so a chain of rotated refresh tokens cannot outlive
it. The client then authorizes again, which checks the password against the directory.

### 3. After the directory disables or deletes the user

`surl` is not told and does not ask. The next login is refused (ADR-0149 decision 4), so nothing starts the hour
again. Tokens already issued keep working: an OCI token until its expiry, at most 300 s; MCP tokens until at most
one hour after the last accepted login. A changed password or an unreachable directory leaves issued tokens the
same way. A directory that still accepts the bind counts as accepting the user. If the directory accepts the name
again, an earlier token still unexpired and not consumed is honoured again, as a re-added account's is under
decision 4.

To take a directory user's access away at once, a test resets the sessions store (`StoreKind.Sessions`, or a reset
of the whole instance), which revokes every OCI token and all MCP OAuth state (decision 2; the sessions reset also
works with `--directory`). A `refuse-login` rule stops later logins, and so the hour starting again, but ends no
token. `remove_account` of a directory user's name removes nothing, and an accounts reset or restore leaves the
directory user's tokens as they were (ADR-0149 decision 6).

### 4. What stays refused

| Case | Verdict |
| --- | --- |
| An account-book account removed by `remove_account`, reset or restore | **Kept: refused** from the next use, whether or not the directory holds the name, and still after the directory later accepts that name, because the book has held it |
| A name the book holds for other schemes only (its `protocols=`) | **Kept: refused.** The directory is never asked about a name the book holds |
| A scheme the `--user-ldap` `protocols=` leaves out (a TLS twin of a named scheme is admitted, ADR-0149 amendment 2) | **Kept: refused.** The directory accepts no login there |
| A name that never logged in through this server's policy | **Kept: refused.** This covers a name whose logins were all refused (wrong password, disabled, directory down, `refuse-login`) and one that logged in only on another server |
| A directory login an hour old or older | Refused |
| The empty name | Refused |

A refused login never ends an earlier accepted one's hour, or a stranger sending a wrong password could end another
user's tokens. Names are compared ordinally, as account names are elsewhere.

Two results of the "never held" rule:

- A directory user whose name the book held and no longer holds can log in, but the user's tokens are refused until
  the instance restarts. It fails closed: a token of the removed account cannot be told from the directory user's by
  name. A test that wants the directory to supply a name keeps that name out of the book.
- While the book holds a name, decision 4 alone decides it, so adding an account with a directory user's name,
  allowed on the server's scheme, makes that user's unexpired tokens the account's, as re-adding an account does.
  Removing the account then refuses them.

### 5. Azure Storage's path-style check

It answers the same with and without `--user-ldap`. `NamesAccount` is unchanged and names account-book accounts
only, so a directory user's name, logged in or not, never turns a path's first segment into an account
([ADR-0109](ADR-0109-how-the-azure-storage-server-answers-az-abfs-and-abfss.md) decision 2). No Azure Storage
login reaches the directory (ADR-0149 decision 2).

### 6. The member and the libraries

The token check is answered by a new member, `IAuthenticationPolicy.HonoursTokenIssuedTo(string accountName)`,
whose default implementation returns `NamesAccount(accountName)`, so a policy with no directory behaves as before.
`AuthenticationPolicy` implements point 2. It keeps, per policy instance (one per server, so `mcp` and `mcps`
share one), each directory-accepted name and the time of its last such login, no secret: at most 10000 names, and
when full it drops those an hour old or older, then the one whose last login is oldest (its tokens are refused
until it logs in again). The account book keeps the names it has held for the life of the instance; a reset or
restore does not forget them. Everything is in memory and per instance, with no static state; a restart forgets it
with the tokens.

| Library | Change | Task |
| --- | --- | --- |
| `Surl.Protocol.Abstractions` | the new member and its documentation | BL-2018 |
| `Surl.Authentication` | `AuthenticationPolicy` implements it; the account book remembers held names | BL-2004 |
| `Surl.Extended.Protocol.OciRegistry` | `OciAccessCheck` asks it instead of `NamesAccount` | BL-2019 |
| `Surl.Extended.Protocol.McpServer` | `McpAuthorizationGrants.AccountOf` and `RedeemRefreshToken` ask it instead of `NamesAccount` | BL-2020 |
| `Surl.Cli` | the `--user-ldap` text of `--aihelp` says how long a directory user's token is honoured | BL-2021 |

`Surl.Composition`, `Surl.Api`, `Surl.Mcp` and the Azure Storage server do not change.

### Alternatives considered

- **Search the directory at each use.** Rejected: a service bind and a search on every request, which still cannot
  see a disabled account whose entry remains, and anonymous search is not always allowed.
- **Honour a directory user's token to its own expiry.** Rejected: rotation would let a disabled user refresh for as
  long as the server runs.
- **Issue directory users no token, or no refresh token.** Rejected: the first leaves them able to log in and do
  nothing; the second needs both servers to learn where a login was judged.
- **Change what `NamesAccount` answers.** Rejected: its name and Azure Storage's use mean the account set, and
  where one policy serves several servers a login on one would change how another reads a path.
- **Mark each code and token with where its login was judged.** It would let a directory user take over a removed
  account's name safely. Rejected: the mark must pass through both servers' code and token records, and the "never
  held" rule keeps every refusal without it.
- **A `--user-ldap` key for the hour.** Rejected: ADR-0149 decision 10 makes its bounds constants, bar `timeout`.
