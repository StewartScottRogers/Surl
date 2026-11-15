# ADR-0164 — How a test reads back the mail store, the content store and the message bus, and finds a store change by name

- **Status:** Accepted
- **Date:** 2026-11-15
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-15, in BL-1194.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) (the event stream, seed, reset, snapshot and
  restore, the fault model, the control endpoint, `Surl.Mcp`'s tools),
  [ADR-0103](ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md) decision 3
  (text-or-base64 bytes), ADR-0100 (the shared stores), ADR-0158 (the Redis keyspace, the LDAP directory and the DNS
  zones as controllable stores) and ADR-0032 (secure by default).
- **Amends:** ADR-0124 decision 1 (an event query gains a store-change name), decision 3 (each store gains reads
  beside seed, reset, snapshot and restore), decision 7 (read routes and event query parameters join the control
  endpoint's table) and decision 8 (read tools and event filters join `Surl.Mcp`'s table).
- **Acted on by:** BL-1195 to BL-1206, and the three read-back tasks filed under decision 9.

## Context

A test can seed, reset, snapshot and restore every store but cannot read one. To check that the code under test
sent mail to `bob`, uploaded `/reports/a.csv` or published to queue `orders`, it opens its own IMAP, HTTP or AMQP
client, or picks bytes out of `server.Exchanges`. And it cannot wait for "`/uploads/report.csv` was written": event
queries filter by store but not by `StoreChangedEvent.Name`, and `Surl.Mcp`'s and the control endpoint's event
queries filter by neither store nor name. A stand-in is **observable**: what a client left behind must be readable
through the same three hosts, in one shape.

## Decision

### 1. The reads, and what each returns

Every read returns a copy; nothing a caller holds changes the store.

- **Mail** (`MailboxStore`'s `ListMailboxes`, `ReadMailbox`, `FetchMessage`, through a `MailView`):
  - *list mailboxes* of an owner: name, message count, unseen count, `UIDVALIDITY`;
  - *list messages* of one mailbox of an owner: UID, size, flags, internal date, in UID order;
  - *read one message* by UID: the same fields and the message's bytes (`message/rfc822`).
  Reading never sets `\Seen` or any flag, never expunges and never changes a UID.
- **Content**:
  - *read a file*: its bytes, size, modification time and content type;
  - *list a folder*: each child's name, kind (`file` or `folder`) and size, in ordinal order of name.
  The ordinary content-store read path the HTTP server uses is the only one.
- **Bus**:
  - *browse a queue*: the ready messages in delivery order, each with a 0-based position, payload and the headers or
    properties it was seeded with; messages in flight (delivered, unacknowledged) are not ready and are not listed;
    the reply also carries `ready_count` and `in_flight_count` (what `InspectQueue` already gives);
  - *read a topic's retained message*: payload and QoS, or "none";
  - *read a topic log*: records from an offset (`ReadLog`), each with offset, timestamp, key and payload, plus the
    log's next offset (`GetLogOffsets`).
  **A bus read never consumes, acknowledges, redelivers, changes a counter or moves a subscription's offset.**
  BL-1195 adds the two reads the bus lacks (queue browse, retained lookup).

### 2. Bounds

- One item returned is at most 16 MiB (as seeding); a larger one is refused (`SurlReadRefusedException` in
  `Surl.Api`, `413 too_large` on the control endpoint, `isError: true` on `Surl.Mcp`) saying its size and the bound,
  never cut short.
- A list or browse returns at most `limit` items: default 100, range 1 to 1000 (mailboxes, mail messages, queue
  messages, log records, folder children). The reply carries `has_more`. Lists take an `after` cursor: the last UID,
  position or offset seen, or for a folder the last name seen.
- One reply holds at most 64 MiB of bytes in total; a list that would exceed it stops early with `has_more` true.

### 3. Reads publish nothing, never consult faults, and work on disk stores

A read publishes no event and no exchange, because it is not a client's exchange and changes nothing. It never
consults the fault plan, as a seed never does (ADR-0124 decision 3): a test checks state without tripping its own
refuse-login or out-of-space rule. Reads are allowed on a `--directory` store: reading empties nothing, unlike
reset, snapshot and restore, which refuse.

### 4. Hidden and confined names

A content read or listing sees the **served root only**. The `.surl` state folder is not served: a path that names
it or anything inside it is `not found` (`404 not_found`), as the HTTP server answers it, and it is absent from the
root listing. A path with `..`, an empty segment, a backslash or a NUL, or one that resolves outside the root, is
refused as `bad_request` before the store is asked (the refusal the seed `PUT` already gives). No read returns a
password, a hash or a token; a message's own bytes are returned as stored.

### 5. `Surl.Api` members

Each is on the existing area object (`SurlServerMail`, `SurlServerFiles`, `SurlServerMessages`), async, takes an
optional `CancellationToken`, throws `SurlReadRefusedException` for a refusal (bound, bad path) and
`SurlNotFoundException` for an absent item.

```csharp
// server.Mail
Task<IReadOnlyList<SurlMailboxInfo>> ListMailboxesAsync(string owner, CancellationToken ct = default);
Task<SurlMessagePage> ListMessagesAsync(string owner, string mailbox, long afterUid = 0, int limit = 100, CancellationToken ct = default);
Task<SurlMailMessage> ReadMessageAsync(string owner, string mailbox, long uid, CancellationToken ct = default);
// server.Files
Task<SurlFileContents> ReadAsync(string path, CancellationToken ct = default);   // Bytes, ByteCount, ModifiedAt, ContentType
Task<SurlFolderPage> ListAsync(string path, string? afterName = null, int limit = 100, CancellationToken ct = default);
// server.Messages
Task<SurlQueuePage> BrowseQueueAsync(string queue, int afterPosition = -1, int limit = 100, CancellationToken ct = default);
Task<SurlRetainedMessage?> ReadRetainedAsync(string topic, CancellationToken ct = default);   // null = none
Task<SurlLogPage> ReadLogAsync(string topic, long fromOffset = 0, int limit = 100, CancellationToken ct = default);
```

A mailbox name is as `MailboxStore` names it (`INBOX` case-insensitive); an owner is an account name. An absent
owner, mailbox, UID, file, folder, queue or log is `SurlNotFoundException`; an absent retained message is `null`.

### 6. `Surl.Mcp` tools

After `delete_file` in `tools/list` order; each takes `server_id` (required), `additionalProperties: false`, results
as ADR-0103 decision 3. An absent item is `isError: true` with its text. Bytes are `text` when well-formed UTF-8
without a control character other than tab, CR and LF, otherwise `base64`; a result holds one of the two.

| Tool | Properties | Result |
| --- | --- | --- |
| `list_mailboxes` | `owner` | `{"mailboxes": [{"name", "message_count", "unseen_count", "uid_validity"}]}` |
| `list_messages` | `owner`, `mailbox`, `after_uid`, `limit` | `{"messages": [{"uid", "size", "flags", "internal_date"}], "has_more"}` |
| `read_message` | `owner`, `mailbox`, `uid` | `{"uid", "size", "flags", "internal_date", "text" or "base64"}` |
| `read_file` | `path` | `{"path", "byte_count", "modified_at", "content_type", "text" or "base64"}` |
| `list_files` | `path`, `after_name`, `limit` | `{"entries": [{"name", "kind", "byte_count"}], "has_more"}` |
| `browse_queue` | `queue`, `after_position`, `limit` | `{"messages": [{"position", "text" or "base64", "headers"}], "ready_count", "in_flight_count", "has_more"}` |
| `read_retained_message` | `topic` | `{"topic", "qos", "text" or "base64"}` or `{"retained": false}` (not an error) |
| `read_log` | `topic`, `from_offset`, `limit` | `{"records": [{"offset", "timestamp", "key", "text" or "base64"}], "next_offset", "has_more"}` |

### 7. Control endpoint routes

All under `/v1/`, `GET`, JSON in snake_case, authenticated as every request (ADR-0124 decision 7); query names are
the tool properties above. Errors are ADR-0124 decision 7's: `400 bad_request`, `404 not_found`, `413 too_large`.

| Route | Answer |
| --- | --- |
| `GET /v1/mail/{owner}` | as `list_mailboxes` |
| `GET /v1/mail/{owner}/{mailbox}` (query `after_uid`, `limit`) | as `list_messages` |
| `GET /v1/mail/{owner}/{mailbox}/{uid}` | the message's bytes as `message/rfc822`, with `X-Surl-Flags` and `X-Surl-Uid` headers; `Accept: application/json` gets `read_message`'s shape |
| `GET /v1/files/{path}` | the file's bytes, with its content type, `Last-Modified` and `Content-Length` |
| `GET /v1/files/{path}/` (trailing slash; query `after_name`, `limit`) | as `list_files` |
| `GET /v1/queues/{name}/messages` (query `after_position`, `limit`) | as `browse_queue` |
| `GET /v1/topics/{name}/retained` | as `read_retained_message`, `404 not_found` when none |
| `GET /v1/topics/{name}/log` (query `from_offset`, `limit`) | as `read_log` |

A path segment is percent-decoded once; `{path}` may hold slashes.

### 8. The name filter and the store spelling

- **Name**: `SurlEventQuery.Name` (`string?`), and `name` on `Surl.Mcp`'s `get_events` and `wait_for_event` and the
  control endpoint's `GET /v1/events` and `GET /v1/events/wait`. It matches a `StoreChangedEvent`'s `Name` (the path,
  mailbox, topic, queue or account, ADR-0124 decision 1) **exactly or as a segment prefix**: `/uploads` matches
  `/uploads` and `/uploads/report.csv` but not `/uploads2`, as a fault rule's `path` does (ADR-0124 decision 4);
  case-sensitive. An event without a name matches only when `Name` is unset. For an exchange query, the exchange
  matches when one of its events does.
- **Store**: `store` on the same two hosts, the `StoreKind` member in snake_case as `RecordedEventWriter` writes it
  (`content`, `mail`, `bus`, `accounts`, `keyspace`, `directory`, `zones`); an unknown value is `bad_request` /
  `isError: true` naming the allowed ones.
- Both combine with the existing filters; every one given must match. Defaults keep today's behaviour.

### 9. The Redis keyspace, the LDAP directory and the DNS zones are also readable

Yes: a test that seeds them (ADR-0158) reads them back the same way, with decisions 2 to 4 unchanged (no event, no
fault, bounds, confinement). The shapes, for the three tasks to build:

- `server.Keys.GetAsync(key)` and `ListAsync(pattern, after, limit)` (string keys, Redis `KEYS` glob; other types are
  listed with their type and not read); tool `read_key`, `list_keys`; routes `GET /v1/keys/{key}`, `GET /v1/keys`.
- `server.Directory.ExportLdifAsync()` (the directory as LDIF, `userPassword` values omitted); tool
  `read_directory`; route `GET /v1/directory`.
- `server.Zones.ReadAsync(apex)` and `ListAsync()`; tool `read_zone`; routes `GET /v1/zones/{apex}`, `GET /v1/zones`.

## Alternatives considered

- **Tell tests to use a real client.** Rejected: it needs the protocol's client library in the test and gives a
  cross-protocol scenario nothing to assert on without a login.
- **Publish reads as events.** Rejected: a read changes nothing and would flood the stream a test waits on.
- **Plain string prefix for the name.** Rejected: `/uploads` would match `/uploads2`; the segment rule is what fault
  paths already follow.
- **Let a read consult the fault plan.** Rejected: faults break the client under test, not the test's assertions.

## Consequences

- BL-1195 to BL-1206 are built from decisions 1 to 8 without another question; BL-1195 (bus browse) is first.
- A read of a disk store shows a real directory's files, which HTTP already serves.
- No option, field, route or tool changes; every addition is optional and defaults keep today's behaviour.
