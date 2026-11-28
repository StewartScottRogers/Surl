# ADR-0158 — How the Redis keyspace, the LDAP directory and the DNS zones are seeded, reset, snapshotted, restored and observed

- **Status:** Accepted
- **Date:** 2026-11-14
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-14, in BL-1151.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) (decisions 1 to 3 give the four
  shared stores their events, seed, reset, snapshot and restore), ADR-0100 (the shared stores), ADR-0106
  (the Redis server and its bounds), ADR-0072 (the LDAP directory), ADR-0092 (the DNS zones), ADR-0083
  (the extended servers are judged by their specifications), ADR-0131 and ADR-0129 (how the mail and content stores
  do the same) and ADR-0003 (upstream curl is the only oracle).
- **Amends:** ADR-0124 decision 2 (the bullet "Stores are not replaceable" gains a registration seam),
  decision 3 (the order across stores grows by three), decision 7 (three requests join the control
  endpoint's table) and decision 8 (three tools join `Surl.Mcp`'s table).
- **Amended (2026-11-27, BL-1857):** the order across stores that decisions 5 and 8 give is superseded by
  ADR-0124's amendment of the same date; see Amendment 2.
- **Amended (2026-11-27, BL-1896):** decision 8's registration seam (`IControllableStore`,
  `ProtocolServerRegistration.ControlledStore`) is superseded by the one the code has; see Amendment 3, decided
  by Claude under Stewart's delegation.
- **Amended (2026-11-27, BL-2028):** the keys decision 4's keyspace reset writes back are bounded; see
  Amendment 4, decided by Claude under Stewart's delegation.
- **Acted on by:** BL-1157, BL-1158, BL-1159, BL-1160, BL-1161, BL-1162, BL-1163, BL-1164, BL-1165,
  BL-1166, BL-1167, BL-1168 and BL-1169.

## Context

ADR-0124 made four stores controllable. Three protocol-owned stores were left out, and in the way
`Surl.Api` embeds - in memory, many side by side - each is useless as a stand-in:

- **The Redis keyspace** (`RedisKeyspace`, internal, built inside `RedisServer`'s constructor, memory
  only) publishes no event, never consults the fault plan at `FaultStage.StoreWrite` (it has only its own
  `maxmemory`-style bound, ADR-0106 decision 6), and no host can seed, reset or snapshot it. Keys leak from
  one test into the next.
- **The LDAP directory** (`LdapDirectory`, immutable, read once from `<path>/.surl/ldap/directory.ldif`)
  is empty without `--directory`, so an in-memory LDAP stand-in serves nothing.
- **The DNS zones** (`DnsZoneSet`, with `Add(fileName, content)`) are loaded from the data directory only,
  so an in-memory DNS stand-in answers no zone.

Neither the LDAP server nor the DNS server has a client write path today (no add, modify, delete or
dynamic update), so for those two stores a seed is the only write there is. `ComposedStores` also hard-codes
its four stores in `StoreOrder` and a private `StoreControl` record, so a fifth store means editing it.

## Decision

### 1. Three new `StoreKind` members

`StoreKind` (an enum that only grows) is appended with `Keyspace`, `Directory` and `Zones`, after
`Accounts`. On the control endpoint and in `Surl.Mcp` they are written `keyspace`, `directory` and `zones`.
Nothing is renumbered; a host that switched over the four old members still compiles. (BL-1157.)

### 2. What each store publishes

`StoreChangedEvent(Store, Change, Name, ByteCount, ExchangeId)`, one event per item changed, never carrying
a value, a password or an attribute's content.

**Keyspace.** `Name` is `<database>/<key>`: the database number 0 to 15, a `/`, then the key as UTF-8, with
each byte that is not valid UTF-8 written `\xHH` and the whole truncated at 256 characters. (The same text
is the `path` a fault rule matches, so `path=0/session` matches every key under `session` in database 0 by
the plan's segment-prefix rule.)

| Change | When | `ByteCount` | `ExchangeId` |
| --- | --- | --- | --- |
| `Written` | a command created a key or changed its value, one event per key (an `MSET` of three keys is three events) | the key's value bytes after the write | the connection's exchange |
| `Deleted` | a key was removed: `DEL`, `UNLINK`, the removal of an expired key when it is observed, and the removal a command such as `RENAME` makes of the old name | null | the exchange, or null for an expiry found by no command |
| `Deleted` | `FLUSHDB` and `FLUSHALL`: one event per non-empty database, `Name` the database number alone (`3`) | null | the exchange |
| `Seeded` | one per seeded key (decision 3) | the value's bytes | null |
| `Reset`, `Restored` | decision 4 and 5: one event for the store, `Name` empty, `ByteCount` 0 for reset and the restored bytes for restore | | null |

Publish/subscribe messages are not keyspace changes: the Redis server already publishes them through the
message bus (ADR-0106), so they stay `Bus` events and are not repeated here.

**Directory.** `Seeded` per entry, `Name` the entry's distinguished name as the directory normalises it for
display, `ByteCount` the entry's size as the directory's bound counts it. `Written` and `Deleted` are
reserved for the day the LDAP server answers add, modify, modify DN and delete: each will then publish
`Written` (the new name; a modify DN also `Deleted` for the old) or `Deleted` with the DN as `Name`. Adding
them is not a new decision.

**Zones.** `Seeded` per zone, `Name` the apex (`example.com.`), `ByteCount` the zone's byte count. `Written`
and `Deleted` are reserved in the same way for dynamic update.

A reset and a restore publish one event per store, as ADR-0124 decision 3 says.

### 3. Seed

A seed goes through the path a client writes by and **never consults the fault plan** (ADR-0124 decision 3).
It is all or nothing - one refused item leaves the store unchanged - and is refused with
`InvalidOperationException` (`409 conflict` on the control endpoint, `isError: true` in `Surl.Mcp`) naming the
bound or rule, and never truncates.

**Keyspace seed - a string key.** `SeedKeyAsync(int database, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value,
TimeSpan? expiry, CancellationToken)`. Database 0 to 15; the key at least 1 byte; the bound is
**the key's and value's bytes together at most 16 MiB**, the one bound every host seed has (ADR-0124
decision 8). `expiry` is 1 millisecond to 365 days and means `PX` on the instance's `TimeProvider`; absent, the
key does not expire. A seed replaces a key that exists, as `SET` does, and the keyspace's own bounds (ADR-0106
decision 6: 1,000,000 keys, 256 MiB) still apply and refuse the seed. Only strings are seeded: every other
Redis type (lists, hashes, sets, sorted sets, streams) is made by the commands a client sends, and a test that
needs one sends it as a client. Offering more types later only grows the surface.

**Directory seed - LDIF content records.** `SeedEntriesAsync(string ldif, CancellationToken)`. The text is an
LDIF file of content records (RFC 2849) - the same reader that loads `directory.ldif` - at most 16 MiB; a
`changetype:` record is refused (a test that wants a delete resets), and so is any value read from a file or a
URL (`attr:< file:///...`), because no host reads a local path (ADR-0103 decision 4's safety rule). An entry
must be valid as a file entry is: its parent present - in the directory already or earlier in the same text -
or the entry a naming context; an existing DN, a second DN in the text and a directory over its
bounds (`LdapDirectory.DefaultMaxEntries`, `DefaultMaxTotalBytes`) are refused. The seed is checked as the
combined set, so it is atomic.

**Zones seed - zone-file text.** `SeedZoneAsync(string apex, string zoneFileText, CancellationToken)`, at most
16 MiB, read as `DnsZoneSet.Add(apex + ".zone", ...)` reads a file; the apex is a domain name with or without
its final dot. A zone whose apex is already held is refused (`DnsZoneFileException`, "a second zone"): replace
by resetting, which is deterministic, never by an edit-in-place a test could half-apply. `$INCLUDE` is refused
by the reader already, and stays so.

**Start-time seeds are what reset returns to.** A host that supplies seeds at start (`Surl.Api`'s
`SurlServerOptions.RedisKeys`, `DirectoryLdif` and `DnsZones`; `Surl.Mcp`'s `redis_keys`, `directory_ldif`
and `dns_zones` start properties, each as the matching seed's shape) has them applied after the stores are
built, before any listener binds, through the same seed operations, and publishes `Seeded` events as any seed does.
Nothing on the command line is added: `--directory` already loads the directory and zones, and a Redis server
has no file to load (ADR-0106).

### 4. Reset

Reset returns a store to **its configured state**:

- **Keyspace:** all 16 databases empty, then the host's start-time keys. Expiry deadlines of those keys are
  measured again from the reset on the instance's clock. A `WATCH`ed key is treated as modified, so a
  pending `EXEC` answers its null reply as Redis does when a watched key changed; a transaction being queued
  is unaffected; a client blocked on a list keeps waiting.
- **Directory:** the entries loaded from `--directory`'s file (an in-memory copy read once, decision 6), then
  the host's start-time entries. With neither, empty.
- **Zones:** the zones loaded from `--directory`'s folder, then the host's start-time zones.

Reset waits for no one: it takes the store's own lock and swaps its contents (ADR-0124 decision 3's "live
sessions"), so a client connected to the Redis, LDAP or DNS server keeps its connection and sees the new
contents on its next command; a Redis connection keeps its selected database.

### 5. Snapshot and restore

A snapshot is an opaque, in-memory deep copy, never returned to a host (hosts hold its ID: `p1`, `p2`, ...).

- **Keyspace:** every database's keys, values and expiry as an **absolute deadline** on the instance's clock.
  A key restored after its deadline has passed expires on its first observation, with its `Deleted`
  event. Bytes counted: key plus value bytes.
- **Directory:** the set of entries, as the immutable entries themselves. A directory entry may hold a
  `userPassword` a test seeded as data; the snapshot is in memory, belongs to its instance and is never
  written, logged or echoed, as with the account book's hashes. Bytes counted: the directory's own entry
  size. *Clarified by
  [ADR-0132 amendment 3](ADR-0132-how-the-account-book-changes-while-the-server-runs.md#amendment-3-2026-11-27-bl-1840-what-a-snapshot-holds-of-each-password):
  an account snapshot holds more than hashes, password bytes among them; it too is never written, logged or
  echoed.*
- **Zones:** the set of zones. Bytes counted: the zones' byte count.

The three snapshots count toward `MaxSnapshotBytes` with the other four: a combined snapshot past it is
refused whole, never truncated. Restore replaces the store's contents with the snapshot's and refuses a
snapshot from another instance (ADR-0124 decision 3); it publishes one `Restored` and treats `WATCH`ed
keys as reset does.

**Order across stores.** ADR-0124 decision 3's order becomes content, mail, bus, accounts, **keyspace,
directory, zones**: the three are independent of every other store and of each other, and are appended so
the existing order does not change. The first failure still stops it, naming the store. *This order is
superseded; see Amendment 2.*

### 6. Is a directory or zones loaded from `--directory` "a store on disk"? No

The directory and the zones loaded from `--directory` are **an in-memory copy of a file read once**, so they
are not stores on disk, and reset, snapshot and restore are allowed on them. ADR-0124 decision 3 refuses the
disk stores so a fixture can never empty a real directory; that danger needs the store to write to the
directory, and these two never do: they read `directory.ldif` and the `.zone` files at start and
never write them back, a seed is held in memory only, and a reset or a restore replaces memory only. The
file is untouched. The Redis keyspace is memory only (ADR-0106 decision 3) and is likewise always controllable.
Consequence: a test that edits `directory.ldif` while the server runs is not seen (it was not before); reset returns to
the copy read at start, not a reread.

### 7. `out-of-space` at a Redis write

At `FaultStage.StoreWrite` the keyspace asks the fault plan before every **write that may grow the keyspace**
- the commands ADR-0106 decision 6 already refuses at its bound, the `deny-oom` commands in Redis's own table (`SET`,
`MSET`, `APPEND`, `INCR`, `LPUSH`, `HSET`, `SADD`, `ZADD` and the rest) - with `FaultPoint { Stage = StoreWrite, Path
= "<database>/<key>" (decision 2's `Name`), Account = the authenticated user or null }`. A matching `OutOfSpace` rule
makes the command write nothing and answer, **exactly as Redis answers a write past `maxmemory`**:

`-OOM command not allowed when used memory > 'maxmemory'.`

(`RedisErrorReplies.OutOfMemory`, the reply ADR-0106 decision 6 already sends at a bound, so a client's code
for one is the code for both.) Commands that do not grow the keyspace (`GET`, `DEL`, `EXPIRE`, every read,
`PUBLISH`) are not asked, as Redis lets them through while it is over `maxmemory`. Inside `MULTI` a queued
write that the rule matches is refused when queued, as Redis refuses a `deny-oom` command, and `EXEC`
answers `-EXECABORT Transaction discarded because of previous errors.`; the rule is applied (and
`FaultAppliedEvent` published) at the queueing. A multi-key write checks each key, and a rule that matches
one refuses the whole command with nothing written. The reply is the specification's, so no `reply` key is
involved; it is not an `error` fault and needs no row in ADR-0124 decision 4's tables. (BL-1159.)

The LDAP directory and the DNS zones have no client write, so they are never asked at `StoreWrite`: an
`out-of-space` rule has no store to act on there, which is not a gap. When the servers gain a write
operation the same stage applies, with the resultCode 53 (`unwillingToPerform`, RFC 4511)
and an RCODE 5 `REFUSED` for dynamic update, each decided with that operation.

### 8. The registration seam in `Surl.Composition`

The seam is two small pieces and no `switch`:

1. `Surl.Protocol.Abstractions` gains the interface a store implements to be controlled and a base class for its
   snapshot:

   ```csharp
   public interface IControllableStore
   {
       StoreKind Kind { get; }
       bool IsOnDisk { get; }                                  // true refuses reset, snapshot and restore
       Task ResetAsync(CancellationToken cancellationToken);
       Task<ControlledStoreSnapshot> SnapshotAsync(CancellationToken cancellationToken);
       Task RestoreAsync(ControlledStoreSnapshot snapshot, CancellationToken cancellationToken);
   }

   public abstract class ControlledStoreSnapshot { public abstract long ByteCount { get; } }
   ```

   Seeds are not on the interface: their inputs differ per store (a key, LDIF text, a zone), so each store keeps
   its typed seed method, and `ComposedStores` exposes it (`SeedKeyAsync`, `SeedEntriesAsync`, `SeedZoneAsync`).

2. `ProtocolServerRegistration` (ADR-0124 decision 2) gains an optional member `ControlledStore`,
   `Func<IProtocolServer, IControllableStore>?`, null for every server that owns no store. The Redis, LDAP and DNS registrations name the
   store their server built. `ComposedStores` is constructed from the **ordered list** of the four original stores
   (adapted to `IControllableStore`, in ADR-0124's order) followed by the `ControlledStore` of each composed
   protocol server, in registration order, so Redis, LDAP and DNS follow in the order decision 5 gives. Its
   static `StoreOrder` stays as it is (the four); an instance `Order` lists every store it holds. A new
   store joins by one registration entry and one `StoreKind` member, and `ComposedStores` is not edited.

Stores stay not replaceable by a test (ADR-0124 decision 2): the seam is for stores Surl ships, and the
`SurlProtocolServer` a test supplies has no `ControlledStore`. (BL-1158, then BL-1161, BL-1163, BL-1165 each
register one.) *The order this decision says Redis, LDAP and DNS follow in is superseded; see Amendment 2.*
*This decision's seam is superseded; see Amendment 3.*

### 9. Host spellings

**`Surl.Api`** - three members of `SurlServer` beside `Files`, `Mail`, `Messages` and `Accounts`, each over
`ComposedStores`:

| Member | Operations |
| --- | --- |
| `Keys` (`SurlServerKeys`) | `SetAsync(string key, string value, TimeSpan? expiry = null, int database = 0)`, `SetAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, TimeSpan? expiry = null, int database = 0)`; both `ArgumentOutOfRangeException` for a database outside 0 to 15 or an expiry outside 1 millisecond to 365 days, `InvalidOperationException` per decision 3 |
| `Directory` (`SurlServerDirectory`) | `AddEntriesAsync(string ldif)` |
| `Zones` (`SurlServerZones`) | `AddZoneAsync(string apex, string zoneFileText)` |

`SurlServerOptions` gains `RedisKeys` (`IReadOnlyList<SurlRedisKey>`: database, key, value, expiry),
`DirectoryLdif` (`string?`) and `DnsZones` (`IReadOnlyList<SurlDnsZone>`: apex, text) - the start-time seeds of
decision 3, additive, defaults empty. `ResetAsync`, `TakeSnapshotAsync` and `RestoreAsync` already cover all
stores and now cover these. (BL-1166.)

**`Surl.Mcp`** (amends ADR-0124 decision 8), after `add_account`, each taking `server_id` (required),
`additionalProperties: false`, results by ADR-0103 decision 3's rules, never echoing a value:

| Tool | Other properties | Result |
| --- | --- | --- |
| `set_key` | `key` or `key_base64`, `value` or `value_base64`, `database` (0 to 15, default 0), `expire_ms` (1 to 31536000000) | `{"key": "<Name as decision 2>", "byte_count"}` |
| `add_ldap_entries` | `ldif` | `{"entry_count"}` |
| `add_dns_zone` | `apex`, `zone_text` | `{"apex", "byte_count"}` |

`reset_server`, `snapshot_server` and `restore_server` are unchanged and cover the three. The server-start tool gains the optional
start-time properties of decision 3. Seeded text at most 16 MiB decoded; a bound, a duplicate or a refusal is
`isError: true` with its text; no local path is read. (BL-1167.)

**Control endpoint** (amends ADR-0124 decision 7), under `/v1/`:

| Request | Body | Answer |
| --- | --- | --- |
| `PUT /v1/keys/{database}/{key}` (key percent-encoded; query `expire_ms`) | the value's bytes | `204` |
| `POST /v1/directory` | `text/plain`: LDIF content records | `204` |
| `PUT /v1/zones/{apex}` | `text/plain`: the zone file | `204` |

A database outside 0 to 15, a bad `expire_ms`, an LDIF or zone the readers refuse: `400 bad_request` with the
reader's text. A bound or an existing DN or apex: `409 conflict`. A body over 16 MiB: `413 too_large`. `POST
/v1/reset`, `POST /v1/snapshots` and `POST /v1/snapshots/{id}/restore` cover the three stores. No `DELETE`
is offered for a key, an entry or a zone: reset does it deterministically. (BL-1168.)

### 10. What proves it

Each store's unit tests prove its events, seed, reset, snapshot and restore, and the keyspace the `-OOM`
reply, from bytes written from Redis's documentation (the Redis server is an extended server, ADR-0083). The
LDAP seed is proved with a **pinned upstream curl** `ldap://` search against an `Surl.Api` server whose
directory was seeded (BL-1169), the one place an original-protocol oracle exists: the DNS and Redis servers
are extended and judged by their specifications.

## Alternatives considered

- **A store interface with `SeedAsync(object)`.** Rejected: the three seeds have nothing in common but their
  name, and an untyped seed trades the compiler for a runtime cast.
- **Treating a `--directory` copy as a disk store and refusing reset.** Rejected: see decision 6; it would leave a
  test that mounts a directory with no way to reset an LDAP or DNS stand-in, for a danger that cannot happen.
- **Re-reading the file on reset.** Rejected: reset would then be as non-deterministic as the disk, and a file an
  outside process changed would reappear.
- **Seeding every Redis type.** Rejected for now: strings are what a test seeds; the rest are sent as a client
  would, and the surface only grows.
- **A free-text `reply` for the Redis out-of-space rule.** Rejected: Redis documents the `-OOM` reply for this
  situation, and a client's code for it is what a test wants to exercise.
- **One `StoreKind.Other`.** Rejected: a subscriber filtering on a store must be able to name it.
- **A `DELETE` route for keys, entries and zones.** Rejected: ADR-0124 decision 3's reset is the deterministic
  way back; a half-edited directory is the state a fixture should never have.

## Consequences

- `StoreKind` grows (BL-1157); `ComposedStores` gets its registration list (BL-1158); the three stores get
  events and out-of-space (BL-1159), control (BL-1160, BL-1162, BL-1164) and their composition (BL-1161,
  BL-1163, BL-1165); the hosts get their spellings (BL-1166, BL-1167, BL-1168); the pinned curl proof is BL-1169.
- `surl --aihelp` and the Surl.Api, Surl.Mcp and control-endpoint pages and the wiki each name the three stores
  when the task that builds the spelling lands.
- No package, no start-up cost with no subscriber, and no change to any byte upstream curl sends or
  expects.

## Amendment 1 (2026-11-19, BL-1404): `set_key`'s result key, and seeds on an instance that serves no such store

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), in BL-1404, from the Surly cookbook
bug report of 2026-11-19 09:30 (finding 7). Amends decision 9's `Surl.Mcp` table (the `set_key` result) and decision 5's
wording "when a redis server is composed" (and the same words for the directory and the zones, decisions 2 and 3).
Builds on ADR-0164 decision 9 (the read tools' shape), which this amendment keeps.

### A1. `set_key` answers the key the way `read_key` and `list_keys` do

One concept gets one spelling in every tool. `set_key`'s result is:

- UTF-8 key: `{"key": "<the key>", "database": <0 to 15>, "event_name": "<database>/<key>", "byte_count": <n>}`
- non-UTF-8 key: `{"key_base64": "<the key, base64>", "database": <0 to 15>, "event_name": "<database>/<key as the store publishes it>", "byte_count": <n>}`
  (no `key`; this is `ReadBackJson.WriteBytes`'s rule, as `read_key` and `list_keys` use it).

`key` and `key_base64` are the bare key, exactly what the caller sent and what `read_key` and `list_keys` answer, so a
test can pass `set_key`'s `key` straight to `read_key`. `event_name` is the store-change name of decision 2
(`<database>/<key>`), which is what a test waits on in the event stream. The value is never echoed.

This changes what `key` means in `set_key`'s result, against the "only grows" rule, and is accepted: decision 9's table
was built by BL-1167 on 2026-11-18 and no release carries it, `0/greeting` was the one place in the tool family that
did not match, and the old text is still available, unchanged, under `event_name`. Every other field is only added.
`ReadKeyTool`, `list_keys` and ADR-0164 decision 9 are unchanged; `Surl.Api`'s `SetAsync` already returns nothing and
the control endpoint answers `204`, so neither moves.

### A2. A seed of a store the instance does not serve is refused

The keyspace, the DNS zones and the LDAP directory exist on an instance **only when one of its listen URLs serves
them**: a `redis` or `rediss` URL for the keyspace, a `dns` (or `dns` over TLS, HTTPS or QUIC) URL for the zones, an `ldap` or
`ldaps` URL for the directory. Today the extended servers are composed whichever URLs are given and the directory is
always registered, so a seed of a store nobody serves succeeds and the test's client finds nothing later, which is the
failure a stand-in must not give. This replaces "when a redis server is composed" in decision 5 with "when a listen
URL serving the store is given", and the same for the directory and the zones.

The refusal lives in **`Surl.Composition`** (`SurlServerComposition.Start.cs` registers a store, and gives
`ComposedStores` its keyspace, zones and directory, only for a served store), so every host agrees with no host code.
`ComposedStores.SeedKeyAsync`, `SeedZoneAsync` and `SeedEntriesAsync` throw `InvalidOperationException` with the
text they already use: `This instance holds no Redis keyspace.`, `This instance holds no DNS zones.` and
`This instance holds no LDAP directory.`. Each host turns it the way it already turns a refused seed:

| Host | Answer |
| --- | --- |
| `Surl.Api` (`Keys`, `Zones`, `Directory`, and the start-time `RedisKeys`, `DnsZones`, `DirectoryLdif`) | `SurlSeedRefusedException` with that text; a start-time seed refuses the start |
| `surl-mcp` (`set_key`, `add_dns_zone`, `add_ldap_entries`) | `isError: true`, the same text |
| control endpoint (`PUT /v1/keys/...`, `PUT /v1/zones/...`, `POST /v1/directory`) | `409 conflict`, the same text |

On such an instance: **reset and snapshot** walk only the stores the instance holds, so the unserved store is not in
`ComposedStores.Order`, is not counted toward `MaxSnapshotBytes` and cannot be restored into; **the read tools**
answer as they do for a store that is absent: `read_key`, `read_directory` and `read_zone` answer `NotFound` with the
same text and `list_keys` and `list_zones` an empty page (ADR-0164 decision 9, unchanged otherwise).

### A3. Alternatives rejected

- **Refuse only in `surl-mcp`.** Rejected: `Surl.Api` and the control endpoint would stay permissive, so the same
  test passes or fails by which host it uses.
- **Keep today's behaviour (a seed of an unserved store is harmless).** Rejected: it is harmless only to the seed;
  the test then fails at a client that finds nothing, far from the cause. A loud refusal at the seed is what a test
  author is better served by, and it costs a test nothing it needs, since a test that seeds Redis serves Redis.
- **Keep `key` as `<database>/<key>` and add the bare key as a new field.** Rejected: two fields would leave the
  odd one out forever; the tool is unreleased, so the repair is cheaper now than as a permanent seam.
- **Drop the event name.** Rejected: a test waiting on the store-change event needs it.

### A4. Acted on by

BL-1410 (composition: serve-gated stores, refusal) and BL-1405 (`set_key`'s result, and the MCP tests of the
refusal). Neither half keeps today's behaviour, so no leftover is recorded for them.

## Amendment 2 (2026-11-27, BL-1857): the order across stores

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), in BL-1857.

The order decision 5 gives ("content, mail, bus, accounts, keyspace, directory, zones"), and decision 8's
statement that Redis, LDAP and DNS follow in that order, are superseded. The current order is the one in
[ADR-0124's amendment of 2026-11-27](ADR-0124-surl-is-a-stand-in-for-tests.md#amendment-2026-11-27-the-order-across-stores-is-thirteen-stores-bl-1857):
other stores now come before, between and after these three. No code changes.

## Amendment 3 (2026-11-27, BL-1896): how a store joins `ComposedStores`

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), in BL-1896. It records the seam
the code has; no code changes.

Decision 8's seam is superseded. `Surl.Protocol.Abstractions` declares no `IControllableStore` and no
`ControlledStoreSnapshot`, `ProtocolServerRegistration` has no `ControlledStore` member, and `ComposedStores` has no
`Order` member. This also replaces the `IControllableStore` text of this ADR's index row.

A store joins `ComposedStores` in `Surl.Composition`, as follows:

1. **The entry** is an internal record, `ComposedStoreRegistration(Store, ResetAsync, TakeSnapshotAsync)`: the
   `StoreKind`, a reset, and a snapshot that is given the bytes still free under `MaxSnapshotBytes`. A snapshot is
   a `TakenSnapshot(Store, ByteCount, RestoreAsync)`, which carries its own restore. There is no on-disk flag: a
   store refuses by throwing `InvalidOperationException`, which `ComposedStores` reports as a
   `ComposedStoreException` naming the store.
2. **A further store's entry** is built by a `*StoreRegistration` class, one per store a server holds (such as
   `RedisKeyspaceStoreRegistration`, `LdapDirectoryStoreRegistration`, `DnsZoneStoreRegistration` and
   `Amqp0ExchangeStoreRegistration`).
3. **The list** is made in `SurlServerComposition.Start.cs`: `ComposeStores` passes the entries to `ComposedStores`
   as `additionalRegistrations`, in order - the Redis keyspace, the LDAP directory,
   `ControlledBrokerAndSessionStores`, the DNS zones, then `ControlledUploads`. Which entries are present is decided
   there, from the types of the composed servers and from the schemes served (Amendment 1, A2).
4. **One entry per `StoreKind`**, since `ComposedStores` finds a store's control by kind.
   `SameKindStoreRegistrations.Combine` joins several of one kind into one.
5. **The other stores** join inside `ComposedStores`: content, mail, bus and accounts in its constructor, then the
   proxy accounts when it is given them, and the TLS-SRP accounts through `JoinTlsSrpAccounts` once server TLS is
   composed.

So a new store a server holds joins reset, snapshot and restore by a `StoreKind` member (when its kind is new), a
`*StoreRegistration` class and its place in one of those lists, with no change to how `ComposedStores` walks them.
A typed seed or read method is still a member of `ComposedStores`, which must then be given the store, so decision
8's "`ComposedStores` is not edited" holds for the registration alone. Stores stay not replaceable by a test: no
member lets a server a test supplies name a store.

## Amendment 4 (2026-11-27, BL-2028): which keyspace seeds a reset returns to, and their bound

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), in BL-2028. Adds to decisions 3
and 4.

`RedisKeyspace.SeedKey` takes `keepOnReset`, `false` by default, the name `DnsZoneSet.SeedZone` uses. A reset
writes back exactly the keys seeded with it, in the order each database and key was first kept, and a later kept
seed of the same database and key replaces the earlier one.

**The kept keys are held to the keyspace's own bounds** (ADR-0106 decision 6): a kept seed is refused with
`InvalidOperationException`, writing and keeping nothing, when the kept keys already number 1,000,000 or their key
and value bytes already reach 256 MiB, as a seed is refused when the keyspace is full. The keyspace's own check is
not enough: a client's `DEL` or an expiry frees room in the keyspace but leaves the key kept, so repeated kept
seeds would hold memory without limit and a reset could fill the keyspace past its bounds. A host's start-time
seeds, applied before any client connects, meet the keyspace's bound first, so nothing changes for them unless a
seeded key's expiry passes while they are still being applied; a later kept seed may then be refused by this bound.

Rejected: no bound on the kept keys, for the reason above; and bounding at reset by leaving keys out, because a
reset that silently returns less than the configured state is not deterministic to a test.

## Amendment 5 (2026-11-27, BL-2032): how `surl-mcp`'s start-time seed properties are refused

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), in BL-2032. Adds to decision 9.

`start_server`'s `redis_keys` and `dns_zones` items are nested objects in the tool's input schema, each with
`additionalProperties: false`, so a refusal comes from one of three places:

| What is wrong | Answer |
| --- | --- |
| The input schema refuses it: an unknown nested property, a wrong type, a `database` or `expire_ms` out of range, a zone without `apex` or `zone_text`, an empty `apex`, `zone_text` or `directory_ldif` | JSON-RPC `-32602` naming the path, e.g. `arguments.redis_keys[1].ttl: not allowed` (ADR-0103 decision 1) |
| A `redis_keys` item's content: both or neither of `key` and `key_base64`, or of `value` and `value_base64`, an empty key, a bad base64 string | `isError: true` naming the item, e.g. `redis_keys[1]: give exactly one of key and key_base64` |
| A store refuses the seed, or no listen URL serves it | `isError: true` with the `SurlSeedRefusedException` text; nothing is left running |

None of them shows a Redis value, and the schema's and the item's refusals show no seeded text at all. A store's
refusal of `directory_ldif` or of a zone is passed on in the store's own words, as `add_ldap_entries` and
`add_dns_zone` pass it, and the reader's words may quote part of the text (an unknown record type, a DN). The nested `key` has no `minLength`, unlike `set_key`'s, so an empty key reaches the
tool and is answered naming its item.

Rejected: checking nested properties again in the tool so an unknown one is `isError`. The schema check runs first,
so no client would ever see that answer, and every other unknown argument of every tool is already `-32602`.
