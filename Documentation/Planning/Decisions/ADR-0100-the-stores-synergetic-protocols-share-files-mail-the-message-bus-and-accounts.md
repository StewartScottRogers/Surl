# ADR-0100 — The stores synergetic protocols share: files, mail, the message bus and accounts

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-622. Stewart approved the direction on 2026-11-04 ("Layer 1 - shared stores across
  synergetic protocols"): servers that do the same job share one store per `surl` process. That
  approval is his and is not reopened here; this ADR decides the design.
- **Amends:** [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3: its table of
  horizontal libraries gains `Surl.MessageBus.UnitLibrary` (decision 3.1).
  [ADR-0093](ADR-0093-how-the-mqtt-5-broker-answers-mqtt5-mqtt5s-and-mqtt5-ws.md) decision 0:
  `mqtt5`'s retained messages, subscriptions' routing and shared subscriptions go through the bus,
  so a message published on `mqtt://` is seen on `mqtt5://` and the other way round (decision 3.9).
  [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decision 5: its "each server's own ADR decides where any metadata a file system cannot hold lives"
  is decided here, once, for all four object-storage servers (decision 2).
- **Amended:** decision 3.2's AMQP 1.0 row by
  [ADR-0107](ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md) decision 4
  (2026-11-05, BL-509): the AMQP 1.0 container drops one leading `/` from a link address, and an
  address with neither `topic/` nor `queue/` names the queue of that name instead of being refused
  with `amqp:not-found`. `Amqp1AddressNames` is unchanged; the container rewrites the address
  before calling it. STOMP's row stands.
- **Amended:** decisions 3.3 and 3.6 by
  [ADR-0112](ADR-0112-how-the-amqp-0-9-1-broker-answers-amqp-and-amqps.md) decisions 6 and 7
  (2026-11-05, BL-503): a named queue delivers in two priority levels, high (AMQP 0-9-1 priority 5
  to 9) before normal, each in order, as AMQP 0-9-1's basic rule 06 requires (BL-730); and the
  AMQP 0-9-1 column's `506 resource-error` is a connection exception (`connection.close`), not a
  channel close, because the XML classes it `hard-error`. `311` stays a channel close.
- **Amended:** decision 2.3 by its own section 2.3.1 (2026-11-06, BL-685): the object metadata
  index's byte format and the choices BL-636 made building it, among them that stale entries are
  dropped lazily rather than on every write.
- **Amended:** decision 3.2's NATS and Kafka rows and "What does not survive a mapping" by its own
  section 3.2.1 (2026-11-06, BL-691, recording BL-630): NATS's last `>` becomes `+/#` (one or more
  levels), not `#`, and Kafka names empty levels (`a..b` is `a//b`); only `.` and `..` are refused.
- **Amended:** decisions 3.3 and 3.6 by their own text (2026-11-06, BL-694, recording BL-631): the
  named-queue rules BL-631 chose building the queues (3.3, "The named queue's rules") and the five
  queue outcomes with each queue server's answer (3.6).
- **Builds on:** [ADR-0003](ADR-0003-upstream-curl-is-surls-only-oracle.md) (upstream curl is the
  only oracle), ADR-0006 (every store a peer can fill is bounded), ADR-0014 (the MQTT 3.1.1
  server), ADR-0031 decisions 5 to 7 (`.surl` is never served, persistence, the lock), ADR-0032
  (accounts), ADR-0050 (the mail store), ADR-0079 (category folders), ADR-0083 (the extended
  servers and their oracle), [ADR-0099](ADR-0099-one-server-composition-for-surl-and-surl-api-and-surl-apis-programming-interface.md)
  (`Surl.Composition`).
- **Leaves to layer 2:** the configuration options that name mail domains, mailboxes, shares,
  buckets, topics, queues and groups (BL-641 and BL-642), and LDAP as an account source (BL-671).
  This ADR leaves one seam for each (decisions 2.6, 3.10 and 4.3).

## Context

Surl already shares two stores. One `ContentStore` (`Foundation/Surl.Content.UnitLibrary`) is
composed in `Surl.Console` (`ComposeContentStore`) and handed to every original file-serving server -
HTTP, FTP, SCP and SFTP, TFTP, Gopher, SMB, RTSP - so a file uploaded over FTP is fetched over HTTP
today. One `MailboxStore` (ADR-0050, BL-207) is handed to SMTP, IMAP and POP3. One `AccountBook` and
`AuthenticationPolicy` (ADR-0032) judge every original server's logins.

Three gaps remain:

- **Messages.** The MQTT 3.1.1 server keeps its retained messages in its own `MqttRetainedMessages`
  (ADR-0014, persisted at `<path>/.surl/mqtt/retained-messages`, ADR-0031 decision 6). Six extended
  brokers are planned - Mqtt5 (ADR-0093), Amqp0, Amqp1, Kafka, Nats and Stomp - and ADR-0093 decision
  0 gave Mqtt5 a private store because the isolation rule forbids one server referencing another.
  Each broker would otherwise hold its own topics, and a message published over MQTT could never be
  consumed over AMQP.
- **Object storage.** ADR-0084 decision 5 put S3, Azure Storage, Google Cloud Storage and the OCI
  registry on the content store, but left metadata, ETags and refused names to each server's ADR,
  which would let four servers invent four sidecars.
- **Accounts.** ADR-0084 decision 4 put the extended servers' logins on the existing contracts but
  did not say which account field each login reads.

Constraints: protocol servers never reference each other (ADR-0002 decision 3,
`ProtocolIsolationTests`), so anything two servers share lives in a horizontal library; base class
library only; native AOT; every store bounded; nothing an original server sends upstream curl may
change; extended servers are judged by their specifications (ADR-0083 decision 1), and the Curl port
is a live client in `Integration` tests only, never evidence.

## Decision

### 1. One store per job, composed once per process

| Job | Store | Library | Servers handed it |
| --- | --- | --- | --- |
| Files | `ContentStore` over one `IContentFileSystem` | `Surl.Content` (exists) | HTTP (and its HTTP/2, HTTP/3), FTP, SCP, SFTP, TFTP, Gopher, SMB, RTSP; extended S3, AzureStorage, GoogleCloudStorage, OciRegistry, CoAP |
| Mail | `MailboxStore` | `Surl.MailStore` (exists, ADR-0050) | SMTP, IMAP, POP3 |
| Messages | `MessageBus` (decision 3) | `Surl.MessageBus` (new) | MQTT 3.1.1; extended Mqtt5, Amqp0, Amqp1, Kafka, Nats, Stomp, Redis pub/sub (decision 3.8) |
| Accounts | `AccountBook` behind `IAuthenticationPolicy`, `ISaslAuthenticationPolicy` and `IHttpAuthenticationSession` | `Surl.Authentication` (exists, ADR-0032) | every server that judges a login (decision 4) |

- **Once per `ComposedServing`.** Each store is created once by `Surl.Composition` (ADR-0099
  decision 1) for one `surl` run, and once per `Surl.Api` `SurlServer`, and the same instance is
  handed to every server of its job - original and extended, every listener and scheme of each.
  Extended servers receive theirs through `ExtendedProtocolServerServices`. Never a static, never a
  second instance for one job: two instances over one data directory would overwrite each other's
  index files, and the data-directory lock (ADR-0031 decision 7) already refuses two `surl`s (or two
  `SurlServer`s) sharing one.
- **On disk under `--directory`, in memory without.** Files live in the served directory as today;
  mail under `<path>/.surl/mail` (ADR-0050 decision 7); the bus under `<path>/.surl/bus` (decision
  3.7); object metadata under `<path>/.surl/objects` (decision 2.3). All `.surl` paths stay unserved
  (ADR-0031 decision 5). Without `--directory` the in-memory file system holds the files and each
  store holds its own state in memory, bounded.
- **Loaded in one order:** after the data-directory lock and before any listener binds, content
  first, then mail, then the bus, then object metadata; each malformed file refuses the start with
  `CouldNotReadFile` (37) and the store's own `<reason>`, as ADR-0031 decision 6 and ADR-0050
  decision 7 do. The order is fixed so the first malformed file reported is deterministic.
- **The mail store is the model.** ADR-0050 stands unchanged: one store, owners and mailboxes,
  bounds as constructor parameters, typed outcomes each server words in its own protocol, an index
  rewritten through a temporary name. The bus and the object metadata index follow the same shape.
  ADR-0050 decision 5 ("the domain is ignored") is layer 2's to change (BL-642, BL-648).

### 2. The file store and the object-storage servers

#### 2.1 The mapping

S3, AzureStorage, GoogleCloudStorage and OciRegistry serve the same `ContentStore` the original
servers serve, through its exposure checks (ADR-0015) and `IContentFileSystem`:

| Name in the protocol | In the content store | Reference |
| --- | --- | --- |
| S3 bucket, Azure container, GCS bucket | top-level folder of the same name | S3 User Guide "Bucket naming rules"; Azure "Naming and Referencing Containers, Blobs, and Metadata"; GCS "Bucket naming requirements" |
| OCI repository `a/b` | folder `a/b` holding an OCI Image Layout (2.4) | OCI Distribution Specification, "Pulling" (the `<name>` grammar) |
| object or blob key `x/y/z` | file `z` in folder `x/y` under the bucket's folder; each `/` in a key is a folder separator | S3 User Guide "Object key naming guidelines"; Azure blob names; GCS "Object naming requirements" |
| zero-byte object whose key ends in `/` | the folder itself (created if missing); a non-empty one is refused (2.2) | S3 console's "folder" convention; GCS "Simulated folders" |
| ListBuckets / List Containers / list buckets | the top-level folders only; a top-level file is not a bucket | S3 `ListBuckets`, Azure `List Containers`, GCS `buckets.list` |
| a listing with `prefix` and `delimiter=/` | the folder's entries; a sub-folder is a common prefix | S3 `ListObjectsV2`, Azure `List Blobs`, GCS `objects.list` |
| a listing without a delimiter | every file under the bucket's folder, recursively, in ordinal key order | the same three operations |

Every bucket and container name each service allows is a valid content-store segment, so a bucket
name is never refused for its characters; `.surl` is not a valid name in any of them, and
ADR-0031 decision 5 still hides it. A file uploaded over FTP to `/photos/2026/a.jpg` is object
`2026/a.jpg` in bucket `photos` over `s3://`, `az://` and `gs://`, and an object put over S3 is a file
FTP, SFTP, HTTP and SMB list and fetch.

#### 2.2 Keys the store refuses

The object services allow keys a file system cannot hold. A key is refused when splitting it at `/`
gives a segment `RequestPathSegments.CheckSegment` refuses - an empty segment (`a//b`, a leading
`/`), `.` or `..`, a `\`, a `:`, a control character, a trailing dot or space, a reserved device
name - or when its first segment under the bucket would shadow an existing entry of the other kind
(a key `a/b` where file `a` exists, a key `a` where folder `a` exists), or when it is a non-empty
object whose key ends in `/`. The same rules apply on every platform (ADR-0018's reason: one set of
tests). Each server answers in its service's own words; the server's own ADR pins the bytes against
its reference, and these are the planning answers:

| Server | Unmappable key | Key conflicts with a folder or file |
| --- | --- | --- |
| S3 (BL-417) | `400` `<Code>InvalidArgument</Code>` (S3 API "Error responses", "List of error codes") | `409` `<Code>InvalidArgument</Code>` |
| AzureStorage (BL-423) | `400` `InvalidResourceName` (Azure "Blob service error codes") | `409` `InvalidResourceName` |
| GoogleCloudStorage (BL-429) | `400` JSON error, reason `invalid` (GCS JSON API "HTTP status and error codes") | `409` reason `conflict` |
| OciRegistry (BL-435) | `400` `NAME_INVALID` (OCI Distribution Specification, "Error Codes") | `400` `NAME_INVALID` |

A read of an unmappable key is answered as a missing object (S3 `NoSuchKey`, Azure `BlobNotFound`,
GCS `404`, OCI `BLOB_UNKNOWN` or `MANIFEST_UNKNOWN`), because no such object can exist.

#### 2.3 Object metadata and ETags: a bounded sidecar index, never served

What a file system cannot hold - `Content-Type`, `Content-Encoding`, `Content-Disposition`,
`Cache-Control` and `Content-Language` set at upload, S3 `x-amz-meta-*`, Azure `x-ms-meta-*`, GCS
custom `metadata`, the ETag, a multipart upload's ETag, GCS's `generation` - lives in one
`ObjectMetadataIndex` (planning name) in `Surl.Content`, built by BL-636:

- **Where:** `<path>/.surl/objects/index`, rewritten whole through `.index-<guid>` and
  `MoveFileReplacing` after every change, as ADR-0050 decision 7's index is; in memory without
  `--directory`. Never served (ADR-0031 decision 5). Malformed at start: `CouldNotReadFile` (37),
  `<reason>` `not an object metadata index`. A failed write is noted with `IExchangeLog.Note` and
  never ends serving.
- **Keyed** by the file's path relative to the served root, ordinal, and **stamped** with the file's
  size and last-write time when the entry was written.
- **Stale is absent.** An entry whose stamp no longer matches the file - because FTP, SFTP, HTTP or
  SMB replaced it - is ignored and dropped on the next write: the file's bytes changed, so metadata
  set for the old bytes no longer describes them. An entry for a file that no longer exists is
  dropped the same way. Renames over other protocols lose metadata; that is the cost of not
  teaching seven original servers about object metadata.
- **A file with no entry** (written over FTP, SFTP, HTTP, SMB or TFTP, or whose entry is stale) is
  read over the object servers with: `Content-Type` named by `Surl.Content`'s media types from the
  extension (as HTTP names it, ADR-0008), else `application/octet-stream`; no user metadata; ETag
  the quoted lower-case hexadecimal MD5 of its bytes (S3's single-part ETag, S3 API "Common Response
  Headers"; Azure ETags are opaque, so the same value serves; GCS `md5Hash` is the base64 of the same
  digest), computed on first read and cached in the index with the file's stamp; GCS `generation`
  the last-write time in microseconds since the Unix epoch. An MD5 is computed by the BCL's
  `MD5` (`System.Security.Cryptography`).
- **Bounds** (constructor parameters, ADR-0006): `DefaultMaxEntries` 100000; `MaxMetadataBytes` 8192
  per object (Azure's limit, "Setting and retrieving properties and metadata"; S3's own 2 KiB limit
  on user metadata, S3 User Guide "Working with object metadata", is enforced by the S3 server before
  it reaches the index); past either, nothing is stored and the upload gets a typed outcome -
  `MetadataTooLarge` for the per-object bound, `MetadataIndexFull` for the entry bound - which each
  server words in its service's terms (planning: S3 `400 MetadataTooLarge` and `503
  ServiceUnavailable`; the server ADRs pin the bytes).
- **Multipart and block uploads** (S3 multipart, Azure Put Block, GCS resumable, OCI chunked) stage
  their parts under `<path>/.surl/objects/uploads/<upload id>/` and become one file on completion,
  through the content store's upload session, so a half-finished upload is never visible to FTP or
  HTTP. Their bounds are each server's ADR's.

##### 2.3.1 The index's byte format, and the choices BL-636 made building it

Added 2026-11-06 by BL-685. Decided by Claude under Stewart's delegation (root `CLAUDE.md`,
"Decisions") in BL-636, which could not edit this ADR at the time, and recorded here so the
object-storage servers' ADRs (BL-417, BL-423, BL-429, BL-435) cite one source. The code is
`Foundation/Surl.Content.UnitLibrary/ObjectMetadataIndex.cs` and `ObjectMetadataIndexFormat.cs`;
`ObjectMetadataIndexTests.StoreAsync_PersistingIndex_WritesThePinnedBytes` pins the bytes.

**Byte format** of `<path>/.surl/objects/index`. Every integer is big-endian. A string is its UTF-8
length (2 bytes, unsigned) then its UTF-8 bytes; an empty string means absent.

1. The header: the 20 ASCII bytes `SURL-OBJECT-INDEX-1` and a line feed.
2. The entry count (4 bytes, unsigned).
3. That many entries, in strictly ascending ordinal order of key. An entry is:
   - its key (a string: the path relative to the served root, segments joined by `/`);
   - the stamped file length (8 bytes, signed, not negative);
   - the stamped last-write time in UTC ticks (8 bytes, signed, a valid `DateTime`) - ticks rather
     than seconds, so the stamp matches the file system's time exactly;
   - the generation (8 bytes, signed);
   - the ETag (a string, not empty);
   - `Content-Type`, `Content-Encoding`, `Content-Disposition`, `Cache-Control` and
     `Content-Language` (five strings);
   - the user metadata count (2 bytes, unsigned), then that many name and value pairs (two strings
     each) in strictly ascending ordinal order of name.
4. Then the end of the file. Anything else - a wrong header, keys or names out of order, a bound
   passed, bytes left over, invalid UTF-8 - is malformed: `CouldNotReadFile` (37), `not an object
   metadata index`.

**Choices:**

- **What counts toward `MaxMetadataBytes`:** the UTF-8 bytes of the five header fields and of every
  user metadata name and value. The ETag does not count; it has its own bound. `MaxMetadataBytes`
  is capped at 65535, so every string fits its 2-byte length.
- **`MaxETagBytes` is 1024** UTF-8 bytes; an ETag that is empty or longer is refused.
- **`NoSuchFile`:** storing metadata for a key with no file behind it answers the
  `ObjectMetadataStoreOutcome` `NoSuchFile` and stores nothing.
- **Stale entries are dropped lazily**, not on every write as the "Stale is absent" bullet above
  first said: an entry is checked against its file when it is read, and every entry is checked at
  once before the index is called full (`MetadataIndexFull`). Stat-ing every one of up to 100000
  entries on every write would make each write slow; what a reader sees is the same, because a
  stale entry is never answered. A computed ETag that finds the index full is answered but not
  cached, and metadata stored without an ETag gets the MD5 computed at store time.
- **Default content type `application/octet-stream`** for every file stored with none, until BL-686
  gives `Surl.Content` the media-type table the "A file with no entry" bullet above names. HTTP
  names every file `application/octet-stream` today too, so the two still agree; when BL-686 lands,
  both switch together.

#### 2.4 The OCI repository folder

A repository's folder holds an **OCI Image Layout** (OCI Image Format Specification, "OCI Image
Layout Specification"): an `oci-layout` file (`{"imageLayoutVersion":"1.0.0"}`), `index.json` whose
manifests carry each tag as the `org.opencontainers.image.ref.name` annotation, and
`blobs/<algorithm>/<hex>` for every blob and manifest. A folder is a repository exactly when it holds
`oci-layout`; a repository name's `/` are folders, so `library/alpine` is `library/alpine/`. Blobs
are per repository (a cross-repository mount copies the file); a pushed blob is checked against its
digest before it is renamed into `blobs/`. The result is a directory any OCI tool reads and FTP, SFTP
and HTTP serve as plain files. A blob or manifest written into the folder over another protocol is
served by the registry only if its bytes match the digest its name claims; one that does not is
answered `BLOB_UNKNOWN` and noted. BL-435 decides the rest.

#### 2.5 CoAP

The CoAP server's resources are the content store already (ADR-0097); nothing changes.

#### 2.6 The seam for layer 2

`ContentStore` resolves the first segment of every path through one lookup, `IContentShareLookup`
(planning name), whose only implementation today maps a name to the top-level folder of the same
name. Layer 2's named shares and `--bucket` mapping (BL-655) replace the implementation; no server
changes when they do.

### 3. The message bus

#### 3.1 The library

`Protocols/Messaging/Surl.MessageBus.UnitLibrary`, namespace `Surl.MessageBus`, tested by
`Protocols/Messaging/Surl.MessageBus.UnitTests`, in `Surl.slnx` under `/Protocols/Messaging/`. It
sits beside the servers that share it, as `Surl.MailStore` sits in `Protocols/Mail/` (ADR-0079). Its
row in ADR-0002 decision 3's table:

| Horizontal library | May itself reference |
| --- | --- |
| `Surl.MessageBus.UnitLibrary` (ADR-0100) | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Content.UnitLibrary` (for `IContentFileSystem`) |

The main type is `MessageBus` (not `Bus`, and not a type named after the namespace, ADR-0050
decision 6's reason). It touches neither a socket nor the disk except through `IContentFileSystem`,
takes `TimeProvider` for expiry and timestamps, and is held to the quality gates.
`ProtocolIsolationTests.HorizontalLibraries` learns the row when BL-629 creates the project.

#### 3.2 The canonical topic name, and each protocol's names

**A canonical topic** is MQTT's topic name (MQTT 3.1.1 section 4.7, MQTT 5.0 section 4.7): a
non-empty UTF-8 string of at most 65535 bytes, without U+0000, without `+` or `#`, split into levels
at `/`; a level may be empty. Names compare ordinally. MQTT is the base because two of the seven
servers speak it, one is original and measured against upstream curl, and its names are the most
permissive of the six - every other protocol's names map into it.

**A canonical filter** is a canonical topic whose levels may also be `+` (exactly one level) or `#`
(zero or more levels). MQTT allows `#` only as the last level; the bus allows it anywhere, because
AMQP 0-9-1 binding keys do (below), and an MQTT filter is still exactly an MQTT filter. A filter whose
first level is a wildcard never matches a topic beginning with `$` (MQTT 3.1.1 and 5.0 section 4.7.2),
for every protocol.

| Protocol | Its name | Canonical form | Not mappable, and the answer | Reference |
| --- | --- | --- | --- | --- |
| MQTT 3.1.1, MQTT 5 | topic `a/b`, filter `a/+`, `a/#` | identical | none: every valid MQTT name is canonical | MQTT 3.1.1 4.7; MQTT 5.0 4.7 |
| MQTT 5 shared subscription | `$share/g/a/+` | filter `a/+`, subscription group `g` (3.4) | none | MQTT 5.0 4.8.2 |
| NATS | subject `a.b`, wildcards `*` and `>` | `.` becomes `/`, `*` becomes `+`, `>` (last only) becomes `+/#`, and a trailing `+/#` becomes `>` again (3.2.1) | an empty token, a token holding white space, `/`, `+` or `#`, or `>` before the last token: `-ERR 'Invalid Subject'` on SUB, `-ERR 'Invalid Publish Subject'` on PUB | NATS client protocol, "Protocol conventions", PUB, SUB, `-ERR` |
| AMQP 0-9-1 | `amq.topic` routing key `a.b`, binding key with `*` and `#` | `.` becomes `/`, `*` becomes `+`, `#` stays `#` | a word holding `/`, `+` or `#`: the publish is unroutable - dropped, or `basic.return` `312 NO_ROUTE` when `mandatory` is set | AMQP 0-9-1 section 3.1.3.3 (topic exchange); class `basic`, methods `publish` and `return` |
| AMQP 0-9-1 | a queue, through the default exchange or a binding | a bus queue of the same name (3.3) | none | AMQP 0-9-1 sections 3.1.3.1, 3.1.4 |
| AMQP 0-9-1 | other exchanges (`amq.direct`, `amq.fanout`, `amq.headers`, declared ones) | none: their routing stays inside the Amqp0 broker, onto bus queues | n/a | AMQP 0-9-1 section 3.1.3 |
| AMQP 1.0 | link address `/topic/<t>` (a source may hold `+` and `#`), `/queue/<q>` | topic `<t>` verbatim, queue `<q>` | any other address, or `<t>` not canonical: `attach` refused with `amqp:not-found` | AMQP 1.0 part 3 sections 3.5.3 and 3.5.4 (address is opaque to the specification), part 2 section 2.8.15 |
| STOMP 1.2 | destination `/topic/<t>` (a SUBSCRIBE may hold `+` and `#`), `/queue/<q>` | topic `<t>` verbatim, queue `<q>` | any other destination, or `<t>` not canonical: an `ERROR` frame, then close | STOMP 1.2 "SEND", "SUBSCRIBE", "ERROR" (destinations are opaque to the specification) |
| Kafka | topic `a.b`, partition 0 | `.` becomes `/`, so an empty level is named too (`a..b` is `a//b`, `.a` is `/a`; 3.2.1); one partition per topic (3.3) | a topic name outside `[A-Za-z0-9._-]{1,249}` is already Kafka's `INVALID_TOPIC_EXCEPTION` (17); a partition other than 0: `UNKNOWN_TOPIC_OR_PARTITION` (3); CreateTopics with more than one partition: `INVALID_PARTITIONS` (37) | Kafka protocol guide, "Error Codes"; Kafka documentation 5.3 "Messages" |
| Redis | channel `a/b` | identical (3.8) | a channel not valid UTF-8 or holding U+0000 stays inside the Redis server | Redis "Pub/Sub" |

**What does not survive a mapping.** NATS, AMQP 0-9-1 and Kafka use `.` as their separator, so a
canonical topic with a `.` inside a level (`a.b/c`) is unreachable from all three: no subscription
of theirs can match it, and a message published on it is not delivered to them. NATS and AMQP 0-9-1
cannot name an empty level, so a canonical topic with one (`/a`, `a//b`, `a/`) is unreachable from
them too. Kafka can: its rule allows `a..b`, `.a` and `a.`, which are `a//b`, `/a` and `a/`; only the
canonical topics `/` and `//`, which would be Kafka's refused `.` and `..`, are unreachable from it
(3.2.1). Kafka can name only levels made of `[A-Za-z0-9_-]`, at most 249 bytes in all. NATS cannot
name a level holding a space or tab, and a canonical `#` is reachable from NATS only as the last
level after a `+` (a NATS `>`); a filter with a `#` anywhere else has no NATS subject. AMQP 0-9-1
cannot name a routing key past 255 bytes (a `shortstr`). The publisher is never refused for these: the
topic is valid for its own protocol, and nobody on the others can have subscribed to it. The `/topic/`
and `/queue/` prefixes for AMQP 1.0 and STOMP are decided here because both specifications leave the
address opaque; one convention for both lets a STOMP producer and an AMQP 1.0 consumer meet.

##### 3.2.1 Amendment: NATS `>` is one or more levels, and Kafka names empty levels

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-06, in
BL-691, recording what BL-630's `NatsSubjectNames` and `KafkaTopicNames` in
`Surl.MessageBus.UnitLibrary` do. This ADR as first written said NATS's last `>` becomes `#`, and
that Kafka cannot name an empty level. Both read each protocol by its specification rather than this
ADR's literal text:

- **NATS `>` becomes `+/#`.** The NATS client protocol ("Protocol conventions") defines `>` as
  matching one or more tokens, while a canonical `#` matches zero or more levels. Mapping `a.>` to
  `a/#` would deliver a message published on `a` to a NATS subscriber of `a.>`, which a NATS server
  never does. `a.>` is therefore `a/+/#` (one or more levels), and `>` alone is `+/#`. Back the other
  way, a filter ending in `+/#` becomes a subject ending in `>`; a canonical `#` anywhere else is
  unreachable from NATS, because no NATS subject means "zero or more".
- **Kafka names empty levels.** Kafka's own rule, `[A-Za-z0-9._-]{1,249}` other than `.` and `..`,
  allows `a..b`, `.a` and `a.`, and this ADR already holds that a publisher is never refused for a
  name valid in its own protocol. So `a..b` maps to `a//b` and back, and only `/` and `//` (Kafka's
  `.` and `..`) are unreachable from Kafka.
- **AMQP 0-9-1 is unchanged.** Its topic exchange's words are letters and digits (section 3.1.3.3),
  so an empty word still makes a routing key unroutable, as the table says.

#### 3.3 What the bus holds

| Part | What it is | Used by | Reference |
| --- | --- | --- | --- |
| **Live fan-out** | Each publish is delivered, in publish order, to every live subscription whose filter matches, once per subscription even when several of one connection's filters match (each server then applies its own overlap rule, MQTT 5.0 3.3.4). A subscription may carry a **group** name: among a group's subscriptions with the same filter, each message goes to one, round robin. | all eight | MQTT 5.0 4.8.2 (shared), NATS "SUB" (queue groups) |
| **Retained message** | At most one per topic, replaced by the next retained publish, removed by a retained publish with an empty payload. Delivered to a new subscription only when its server asks (MQTT's subscribe). | MQTT 3.1.1, MQTT 5 | MQTT 3.1.1 3.3.1.3; MQTT 5.0 3.3.1.3 |
| **Named queue** | A FIFO of messages with competing consumers, each delivery unacknowledged until acked; a nack, a reject with requeue, or a consumer's end puts it back at its place with a redelivered mark. A queue may be **bound** to canonical filters, so a publish on a matching topic is copied into it. Flags: durable, exclusive to one connection, auto-delete after its last consumer. | Amqp0, Amqp1, Stomp | AMQP 0-9-1 3.1.4, class `queue`, `basic.ack`, `basic.reject`, `basic.nack`, `basic.recover`; AMQP 1.0 part 3 3.4 (delivery state); STOMP 1.2 "ACK", "NACK" |
| **Topic log** | An append-only sequence of every message published on the topic, with offsets from 0, contiguous; the oldest are dropped past the topic's byte bound and the log's start offset advances. | Kafka (read), every publisher (write) | Kafka protocol guide, Produce, Fetch, ListOffsets |

- **Every publish goes to all four** where they apply: fan-out to live subscriptions; the retained
  slot when the publish is marked retained; a copy into every queue bound by a matching filter; the
  topic's log. So `curl -d hi mqtt://host/t` is seen by an MQTT 5 subscriber, a NATS subscriber of
  `t`, an AMQP queue bound to `amq.topic` with key `t`, and a Kafka consumer of topic `t` at its next
  fetch.
- **A log for every topic,** not only declared ones, so a Kafka consumer can read what was published
  over MQTT before it connected. Kafka's consumer groups and committed offsets stay inside the Kafka
  broker: they are Kafka's bookkeeping, not messages.
- **Retained is MQTT's alone to read.** No other protocol has a retained message, so AMQP, NATS,
  STOMP and Redis subscribers never receive one on subscribing; they see only live fan-out or their
  queue. Only MQTT 3.1.1 (every publish, ADR-0014 decision 1) and MQTT 5 (when RETAIN is set) write
  the retained slot.
- **NATS queue groups and MQTT 5 shared subscriptions are fan-out groups**, not queues: neither
  protocol stores a message for an absent subscriber, so neither uses a named queue.

**The named queue's rules** (2026-11-06, BL-694, recording what BL-631 built into
`Surl.MessageBus`). The table above left these open:

- **A reject without requeue drops the message.** Decided by Claude under Stewart's delegation. The
  table names only requeue as putting a delivery back; the bus has no dead-letter queue, and AMQP
  0-9-1 `basic.reject` lets a server discard a message it is not asked to requeue. A dead-letter
  queue would be a second store a peer can fill, and no protocol on the bus can name one.
- **A consumer receives nothing until `BusQueueConsumer.Start`.** Decided by Claude under Stewart's
  delegation. `Consume` makes the consumer, and the server starts it once its own reply is written,
  so an AMQP 0-9-1 client reads `basic.consume-ok` before the first `basic.deliver` (the XML's
  consume method answers with consume-ok), and a message already in the queue cannot overtake it.
  A consumer counts against `MaxSubscriptionsPerConnection` (3.6), the bound on what one
  connection holds.
- **An exclusive queue belongs to the connection that declared it.** Decided by Claude under
  Stewart's delegation. Any connection may send to it (`MessageBus.PublishToQueue` takes no
  connection, as AMQP 0-9-1's default exchange routes to any queue by name), but only its owner may
  declare, bind, consume from or delete it; any other connection gets `QueueLocked` (AMQP 0-9-1
  `405 resource-locked`, rule `declare`/`exclusive`). It is deleted when its owner goes.
- **Declaring a queue again with other flags is `QueueFlagsDiffer`** and leaves the queue as it was
  (AMQP 0-9-1 `queue.declare`: an inequivalent redeclaration is `406 precondition-failed`). An empty
  queue name is `InvalidQueueName`; a queue that does not exist is `UnknownQueue`; a delivery tag the
  queue did not hand out, or one already settled, is `UnknownDeliveryTag`. Delivery tags are unique
  per queue and new on each redelivery, and `Redelivered` means the queue handed the message out
  before, even when the consumer ended before its callback ran.
- **A topic publish reports its queue copies.** `BusPublishResult.QueuedCount` is the number of bound
  queues that took a copy and `FullQueueCount` the number that refused one as full (3.6), so a
  server can tell a routed publish from an unroutable one (AMQP 0-9-1's `mandatory`, ADR-0112).

#### 3.4 The message, and what survives translation

A bus message holds: the canonical topic; the payload bytes, **always exact**; a `Retain` mark; the
publisher's quality of service (0, 1 or 2); an optional absolute expiry; an optional content type;
an optional response topic and correlation bytes; an optional key (bytes); an ordered list of string
headers (name, value, both UTF-8, names repeatable); and the publish time from the bus's
`TimeProvider`.

| Semantic | Kept | Dropped or approximated | Reference |
| --- | --- | --- | --- |
| Payload | always byte for byte | never | every protocol |
| QoS 0, 1, 2 | the publisher's QoS travels with the message: MQTT's own; AMQP 0-9-1 and AMQP 1.0 unsettled, STOMP and Kafka publish as 1; AMQP 1.0 pre-settled and NATS as 0 | Exactly-once is each MQTT server's handshake with its own client; across protocols delivery is at most once on fan-out and at least once from a queue. An MQTT 5 subscriber is granted `min(publish, subscription)` (MQTT 5.0 3.8.3.1). | MQTT 3.1.1 4.3; MQTT 5.0 4.3; AMQP 1.0 part 2 2.6.12 |
| Retained | as 3.3 | invisible to non-MQTT subscribers | MQTT 5.0 3.3.1.3 |
| Durable | durable queues and every log persist under `--directory` (3.7) | AMQP 0-9-1 `delivery-mode` 1 (transient) messages in a durable queue are persisted too: one rule is simpler than two, and a test server losing nothing is no harm | AMQP 0-9-1 class `basic` property `delivery-mode` |
| Acknowledgement | only for queue deliveries | fan-out deliveries are never acknowledged to the bus; each server acknowledges its own client as its protocol says | AMQP 0-9-1 `basic.ack`; STOMP 1.2 "ACK" |
| Ordering | per topic, publish order, for every subscription and the log; per queue FIFO | a redelivered queue message keeps its place, so a consumer can see it after later ones | MQTT 5.0 4.6; AMQP 0-9-1 3.1.4 |
| Expiry | MQTT 5 Message Expiry Interval, AMQP 0-9-1 `expiration` (milliseconds), AMQP 1.0 header `ttl` and `absolute-expiry-time` become one absolute expiry; an expired message is dropped from the retained slot and queues on read and never delivered; MQTT 5 sends the remaining interval | NATS, STOMP, Kafka and MQTT 3.1.1 have none: their messages never expire, and an expiry is dropped when delivered to them; a Kafka log keeps an expired message (Kafka retention is by bytes, 3.6) | MQTT 5.0 3.3.2.3.3; AMQP 0-9-1 property `expiration`; AMQP 1.0 part 3 3.2.1, 3.2.4 |
| Headers | MQTT 5 User Properties, AMQP 0-9-1 `headers` entries whose value is a `longstr` or `shortstr`, AMQP 1.0 `application-properties` whose value is a string, NATS headers (`HPUB`, `NATS/1.0` block), STOMP frame headers other than STOMP's own, Kafka record headers whose value is valid UTF-8 | header values of other types (numbers, tables, binary) are dropped, and a `-v` note says so; MQTT 3.1.1 carries none, so every header is dropped toward it | MQTT 5.0 3.3.2.3.7; AMQP 0-9-1 field tables; AMQP 1.0 part 3 3.2.5; NATS "HPUB"; STOMP 1.2 "Frames and Headers"; Kafka documentation 5.3.2 "Record" |
| Content type | MQTT 5 Content Type, AMQP 0-9-1 `content-type`, AMQP 1.0 `content-type`, STOMP `content-type` | NATS, Kafka and MQTT 3.1.1 drop it | MQTT 5.0 3.3.2.3.9 |
| Response topic and correlation | MQTT 5 Response Topic and Correlation Data, NATS reply subject (name-mapped, 3.2), AMQP 0-9-1 `reply-to` and `correlation-id`, AMQP 1.0 `reply-to` and `correlation-id` | a reply-to that is not mappable is dropped | MQTT 5.0 3.3.2.3.5, 3.3.2.3.6; NATS "PUB" |
| Key | Kafka's record key | other protocols drop it | Kafka documentation 5.3.2 |
| Payload Format Indicator | MQTT 5 only, carried as the header-free flag `Utf8Payload` | other protocols drop it | MQTT 5.0 3.3.2.3.2 |
| Kafka batching and compression | records are stored one message each, uncompressed | a Fetch serves new, uncompressed batches; the producer's batch boundaries and codec are not kept (the protocol allows any batching on fetch) | Kafka documentation 5.3.1 "Record Batch" |

#### 3.5 The MQTT 3.1.1 server moves onto the bus, unchanged on the wire

`MqttRetainedMessages` is replaced by the bus's retained slots (BL-635). Every byte of ADR-0014
stands: every publish is retained whatever its `RETAIN` flag, a subscribe answers `SUBACK`, each
matching retained message once in ordinal order of topic, then `DISCONNECT`; a store past its bound
closes with no reply. The MQTT 3.1.1 server now also feeds live fan-out, bound queues and logs, and
its one-shot subscribe reads retained messages other servers wrote (MQTT 5 RETAIN publishes), with
their headers dropped. Every `UpstreamCurl…SurlOverMqtt…Tests` stays green.

**An existing `<path>/.surl/mqtt/retained-messages`** (ADR-0031 decision 6) is still read at start
when `<path>/.surl/bus/retained` does not exist: each record becomes a retained message, QoS 0, no
headers, no expiry; the bus file is then written at once, and from then on the bus file is the
truth. The old file is left untouched (an older `surl` pointed at the directory still reads it), and
a malformed one still refuses the start with 37 and `not a retained-message file`. So a data
directory written by an older `surl` keeps its messages.

#### 3.6 Bounds and outcomes

Constructor parameters of `MessageBus`, with named defaults (ADR-0006); no option sets them in this
work:

| Constant | Default | Counts |
| --- | --- | --- |
| `DefaultMaxTopics` | 10000 | topics with a retained message or a non-empty log (ADR-0014 decision 7's number) |
| `DefaultMaxRetainedPayloadBytes` | 104857600 (100 MiB) | retained payload bytes, all topics (ADR-0014 decision 7's number) |
| `DefaultMaxQueues` | 1000 | named queues |
| `DefaultMaxMessagesPerQueue` | 100000 | messages in one queue, unacknowledged included |
| `DefaultMaxQueuedBytes` | 268435456 (256 MiB) | payload bytes in all queues (ADR-0050 decision 6's memory bound) |
| `DefaultMaxLogBytesPerTopic` | 16777216 (16 MiB) | one topic's log; past it the oldest messages are dropped and the start offset advances |
| `DefaultMaxLogBytes` | 268435456 (256 MiB) | every log together; past it a publish is refused |
| `DefaultMaxSubscriptionsPerConnection` | 1000 | live subscriptions one server connection holds |
| `MaxMessageBytes` | `--max-filesize` (`ExchangeLimits.MaxUploadBytes`), set by the composition | one payload |

Past a bound nothing changes and the publish or subscribe gets a typed outcome, which each server
words as its protocol says; the server's own ADR pins the bytes:

| Outcome | MQTT 3.1.1 | MQTT 5 | AMQP 0-9-1 | AMQP 1.0 | Kafka | NATS | STOMP |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `StoreFull`, `TooManyTopics`, `QueueFull`, `TooManyQueues` | close, no reply (ADR-0014 decision 7) | `0x97` Quota exceeded | channel close `506 resource-error` | `amqp:resource-limit-exceeded` | `POLICY_VIOLATION` (44) | `-ERR` then close | `ERROR` then close |
| `MessageTooLarge` | close, no reply (ADR-0006 section 5) | `0x95` Packet too large | channel close `311 content-too-large` | `amqp:link:message-size-exceeded` | `MESSAGE_TOO_LARGE` (10) | `-ERR 'Maximum Payload Violation'` | `ERROR` then close |
| `TooManySubscriptions` | `SUBACK` `0x80` | `SUBACK` `0x97` | channel close `506` | `amqp:resource-limit-exceeded` | n/a | `-ERR` | `ERROR` |
| `StorageFailed` (a persistence write) | noted with `IExchangeLog.Note`; the change stays in memory; serving goes on | same | same | same | same | same | same |

A queue past its message bound refuses the copy into it alone; the publish still reaches fan-out,
other queues and the log: one full queue must not starve every other subscriber (AMQP 0-9-1 leaves
a queue's overflow to the server). `QueueFull` answers both `MaxMessagesPerQueue` and
`MaxQueuedBytes`: the row gives both the same answer.

**The queue outcomes** (2026-11-06, BL-694, recording BL-631). Only the three servers that use named
queues meet them (3.3). Each answer is the one the server's own ADR pins (ADR-0112 for AMQP 0-9-1,
ADR-0107 for AMQP 1.0, ADR-0118 for STOMP); "does not arise" means the server never lets the bus
return that outcome to it:

| Outcome | AMQP 0-9-1 | AMQP 1.0 | STOMP |
| --- | --- | --- | --- |
| `UnknownQueue` | channel close `404 not-found`; a publish to the default exchange naming no queue is unroutable instead (`basic.return` `312` under `mandatory`) | a consume refused at attach, or a transfer `rejected`, with `amqp:not-found` (`no such node`) | ERROR `message:Resource limit exceeded`, then close (a queue deleted between SEND or SUBSCRIBE's declare and its use; the first SEND or SUBSCRIBE creates a missing queue) |
| `QueueLocked` | channel close `405 resource-locked` | attach refused with `amqp:unauthorized-access`, the same answer as a refused access | ERROR `message:Resource limit exceeded`, then close (a SUBSCRIBE to another connection's exclusive queue; a SEND to it succeeds) |
| `QueueFlagsDiffer` | channel close `406 precondition-failed` | does not arise: an existing queue is used with the flags it has | does not arise: an existing queue is used with the flags it has |
| `UnknownDeliveryTag` | channel close `406 precondition-failed` (`basic.ack`, `basic.reject`, `basic.nack`, rule `ack`/`exists`) | does not arise: a `disposition` for a delivery already settled finds nothing and changes nothing | does not arise: an ACK or NACK naming no outstanding message is ignored before the bus (ADR-0118 decision 5) |
| `InvalidQueueName` | does not arise: an empty `queue.declare` name makes a server-named queue, and other names are checked first (`406`) | does not arise: an empty address is refused first with `amqp:not-found` | does not arise: an empty `/queue/` is refused first with ERROR `message:Unknown destination`, then close |

#### 3.7 Persistence under `<path>/.surl/bus`

ADR-0031 decision 6's and ADR-0050 decision 7's rules: files under `<path>/.surl/bus/` - `retained`
(rewritten whole through a temporary name), `queues/index` with message files written once each
(ADR-0050's message-file pattern), and `logs/<topic number>` appended - loaded once at start after
the data-directory lock and before any listener binds; a malformed file refuses the start with
`CouldNotReadFile` (37) and `<reason>` `not a message bus file`; a failed write is noted and never
ends serving. Durable queues and logs persist; exclusive and auto-delete queues and live
subscriptions do not. Without `--directory`, everything is in memory. BL-634 fixes the byte formats
(ordinal order, so tests can pin the bytes).

#### 3.8 Redis pub/sub joins the topic space

Yes. Redis pub/sub is publish and subscribe, the bus's job, and Stewart's direction is that servers
doing the same job share one store. A Redis channel that is valid UTF-8 without U+0000 is the
canonical topic of the same text verbatim (no `.` mapping: Redis channel names have no separator);
any other channel stays inside the Redis server. `PSUBSCRIBE` patterns are Redis globs, which bus
filters cannot express, so the Redis server holds one bus subscription to `#` per connection with a
pattern and matches each delivered topic against its globs itself. `PUBLISH`'s integer reply is the
number of live subscriptions, of any protocol, the bus delivered to (Redis "PUBLISH": the number of
clients that received the message). Redis keys are not bus topics. Follow-up: **BL-678**.

#### 3.9 ADR-0093 decision 0, amended

`mqtt5` no longer keeps a private `Mqtt5RetainedMessages` or routes subscriptions itself: its
retained messages, live routing and shared subscriptions are the bus's (3.3), and its properties
travel as 3.4 says. `<path>/.surl/mqtt5/retained-messages` is never written (no build had written
it). Its bounds are the bus's, answered `0x97` as decision 0 said. The library references
`Surl.MessageBus` and keeps `Surl.Content` only if it still needs it for something else. Sessions,
the QoS flows with its own client, topic aliases, Receive Maximum and queued messages for an
offline session (ADR-0093 decision 3) stay in the broker: they are one connection's state, not the
topic space's.

#### 3.10 The seam for layer 2

`MessageBus` resolves every name a server maps (3.2) through one lookup, `ITopicNameLookup`
(planning name), the identity today. Layer 2's declared topics and queues, `--topic-map` aliases and
`--topic-access` checks (BL-664) replace it; no server changes when they do.

### 4. Accounts: one account set for every server

#### 4.1 Every login reads the same `AccountBook`

The `--user` and `--user-file` accounts (ADR-0032 sections 1 and 2) are the only accounts. An
account is a name and a password; an empty name is a Bearer token (ADR-0032 section 1). Every
extended server judges its login through the contracts ADR-0084 decision 4 names, and so reads:

| Server | Login | Contract | Account field read | Reference |
| --- | --- | --- | --- | --- |
| S3 | AWS Signature Version 4 (`aws-sigv4`, already an ADR-0032 method) | `IHttpAuthenticationSession` | name = access key ID, password = secret access key (the HMAC key derives from it) | AWS "Signature Version 4 signing process" |
| AzureStorage | Shared Key | `IHttpAuthenticationSession` | name = storage account name, password = the account key as its base64 text (decoded to the HMAC-SHA256 key) | Azure "Authorize with Shared Key" |
| AzureStorage | Bearer | `IHttpAuthenticationSession` | an empty-name account's password = the token | RFC 6750 |
| GoogleCloudStorage | V4 HMAC (`GOOG4-HMAC-SHA256`) | `IHttpAuthenticationSession` | name = HMAC access ID, password = secret | GCS "V4 signing process with HMAC keys" |
| GoogleCloudStorage | Bearer | `IHttpAuthenticationSession` | an empty-name account's password | RFC 6750 |
| OciRegistry | token endpoint: Basic to the endpoint, then a surl-issued Bearer token | `IHttpAuthenticationSession` | name and password at the endpoint; the token is the registry's own, valid for its lifetime only | Docker "Token Authentication Specification" (the OCI Distribution Specification defers to it) |
| Mqtt5 | user name and password; enhanced authentication | `IAuthenticationPolicy`; `ISaslAuthenticationPolicy` | name, password; SASL as below | MQTT 5.0 3.1.3.5, 3.1.3.6, 4.12 |
| Amqp0 | SASL `PLAIN` (and `AMQPLAIN`) in `Connection.Start-Ok` | `ISaslAuthenticationPolicy` | authcid = name, password | AMQP 0-9-1 class `connection`; RFC 4616 |
| Amqp1 | SASL layer (`PLAIN`, `ANONYMOUS` only under `--allow-anonymous`) | `ISaslAuthenticationPolicy` | name, password | AMQP 1.0 part 5 section 5.3 |
| Kafka | `SaslHandshake` and `SaslAuthenticate` (`PLAIN`, SCRAM-SHA-256 and -512) | `ISaslAuthenticationPolicy` | name, password (SCRAM derives from it, BL-416) | Kafka protocol guide; RFC 5802, RFC 7677 |
| Nats | `CONNECT` `user`/`pass`, or `auth_token` | `IAuthenticationPolicy` | name and password; a token is an empty-name account's password | NATS client protocol, "CONNECT" |
| Stomp | `CONNECT` `login`/`passcode` | `IAuthenticationPolicy` | name, password | STOMP 1.2 "CONNECT or STOMP Frame" |
| Xmpp | SASL (`PLAIN`, SCRAM-SHA-1, SCRAM-SHA-256) | `ISaslAuthenticationPolicy` | authcid = name, password | RFC 6120 section 6 |
| Redis | `AUTH [user] password`, `HELLO … AUTH` | `IAuthenticationPolicy` | name (Redis's `default` when none is given), password | Redis "AUTH" |
| Coap (coaps) | DTLS PSK identity | none: the composition builds the PSK lookup from the same account list (ADR-0097) | identity = name's UTF-8, key = password's UTF-8 | RFC 4279; RFC 7252 9.1.3.1 |
| DnsQuery, Dns | none | none | none | RFC 1035 |

The three HMAC-signed HTTP logins (SigV4, Shared Key, GOOG4) need the password itself, which the
clear-password `--user-file` format exists to allow (ADR-0032 section 2). They are added as methods
inside `Surl.Authentication`'s `IHttpAuthenticationSession`, as Digest is, by their servers' tasks
(BL-420, the Azure and GCS authentication tasks their ADRs file); no server reads an account.

#### 4.2 No private credential source

None of the extended servers' ADRs, finished or planned, reads a credential source of its own: Mqtt5
(ADR-0093 decision 2) and Coap (ADR-0097) read the shared accounts, and the planned decision tasks
(BL-417, BL-423, BL-429, BL-435, BL-487, BL-503, BL-509, BL-516, BL-524, BL-530, BL-535) are bound
by 4.1. No follow-up is needed. One watch item: Coap's PSK lookup is built from the account list,
not through `IAuthenticationPolicy`, so when BL-647 restricts an account's logins to named
protocols, BL-647 applies the same restriction to that lookup.

#### 4.3 The seam for layer 2

Account groups (BL-646), per-protocol restriction (BL-647) and LDAP as a source (BL-671, BL-672)
extend `AccountBook` and its policies; no server changes when they do.

### 5. Cross-protocol tests

All in `Conformance/Surl.Conformance.UnitTests`, `Integration` category, each against one `surl`
started with every listener the test needs, both with `--directory` and in memory unless noted.

| Class | Ends | What each end must show | Task |
| --- | --- | --- | --- |
| `UpstreamCurlSharesFilesAcrossSurlProtocolsTests` | pinned upstream curl at both ends: `-T` over `ftp` then GET over `http`; `-T` over `sftp` then fetch over `ftp`; `-T` over `http` (PUT) then fetch over `scp` | exit 0, stdout the uploaded bytes exactly | BL-638 |
| `UpstreamCurlSharesMailAcrossSurlProtocolsTests` | pinned upstream curl: `smtp` send, then `imap` fetch and `pop3` `RETR` | exit 0, the message bytes as delivered | BL-639 |
| `UpstreamCurlSharesTopicsAcrossSurlListenersTests` | pinned upstream curl: `-d` publish on one `mqtt` listener, subscribe on another `mqtt` and on `mqtts`; and publish, restart `surl` with the same `--directory`, subscribe | stdout `00 01 74 68 69` (topic length, topic, payload), exit 0 | BL-640 |
| `CurlPortClientReadsFilesSharedAcrossSurlProtocolsTests` | upstream curl uploads over `ftp`; the Curl port's pinned client (`CurlPortBuilds.json`) GETs over `s3`, `az`, `gs` | the S3, Azure and GCS specifications decide the answer; the port only completes the exchange | BL-669 |
| `CurlPortClientConsumesMessagesSharedAcrossSurlProtocolsTests` | upstream curl publishes over `mqtt`; the port consumes over `amqp`, `stomp`, `nats` (subscribed **before** the publish: those protocols deliver live only, 3.3), `kafka` and `mqtt5` (after the publish: the log and the retained slot hold it) | the payload bytes exact, per each protocol's specification | BL-670 |

The Curl port's output is never an expected value and never ADR evidence (ADR-0083 decision 1);
every expected byte on an original end comes from pinned upstream curl, and on an extended end from
its specification.

## Alternatives considered

- **A private store per broker, bridged by the composition.** Rejected: seven copies of retention,
  bounds and persistence, a bridge that loses whatever it forgets to copy, and Stewart's direction
  is one store per job.
- **AMQP 0-9-1's model (exchanges, bindings, queues) as the canonical one.** Rejected: MQTT 3.1.1
  is the original, curl-measured server, and its names are a superset of the dotted protocols';
  queues and bindings still exist on the bus where AMQP needs them.
- **Mapping `.` in MQTT names to something NATS, AMQP and Kafka can carry** (NATS's own MQTT bridge
  escapes it). Rejected: an escape is a convention no client of either side knows, and it makes the
  same topic two names; unreachable is honest and simple.
- **Retained messages delivered to AMQP, STOMP and NATS subscribers.** Rejected: none of their
  specifications has the concept, and a subscriber would receive a message it did not expect.
- **Metadata in extended attributes or alternate data streams.** Rejected: not portable across
  Windows, Linux and macOS, not available in memory, and not in the BCL for all three.
- **Metadata kept even when another protocol replaces the file.** Rejected: it would describe bytes
  that no longer exist.
- **Redis pub/sub kept private.** Rejected: it is the bus's job, and the glob patterns cost one `#`
  subscription, not a bus feature.

## Consequences

- **Build tasks** (filed 2026-11-04): BL-629 creates `Surl.MessageBus` with the topic space, fan-out
  and retained messages (3.1 to 3.3); BL-630 maps MQTT, AMQP, Kafka, NATS and STOMP names (3.2);
  BL-631 named queues (3.3); BL-633 the topic logs (3.3); BL-634 persistence (3.7); BL-635 moves the
  MQTT 3.1.1 server onto the bus (3.5); BL-636 object metadata and ETags beside the content store
  (2.3); BL-638, BL-639 and BL-640 the upstream curl cross-protocol tests; BL-669 and BL-670 the
  Curl port client tests (5). Filed by this ADR: BL-678 (Redis pub/sub on the bus, 3.8).
- **Existing extended-server tasks this changes:**
  - the object-storage decision tasks BL-417 (S3), BL-423 (Azure), BL-429 (GCS) and BL-435 (OCI)
    decide within decision 2 - the mapping, the refused keys, the sidecar index and the OCI layout
    are fixed here - and add BL-636 to the `depends-on` of their build tasks (BL-418, BL-419, BL-424,
    BL-425, BL-431, BL-436, BL-437) when they file or refine them;
  - the broker decision tasks BL-503 (AMQP 0-9-1), BL-509 (AMQP 1.0), BL-516 (Kafka), BL-524 (NATS)
    and BL-530 (STOMP), which already wait on this ADR, decide within decision 3: their topics,
    queues and logs are the bus's, and their build tasks (BL-506, BL-513, BL-519, BL-520, BL-521,
    BL-526, BL-532) route through it;
  - the MQTT 5 tasks BL-497 (retained messages) and BL-498 (subscriptions) build on the bus rather
    than a private `Mqtt5RetainedMessages` (3.9), and BL-501 composes no broker-owned store;
  - BL-491 (Redis pub/sub) is built first inside the server and moved onto the bus by BL-678;
  - BL-420 and the Azure and GCS authentication tasks add their signing methods inside
    `Surl.Authentication` (4.1).
- **Layer 2** (BL-642 and the tasks after it) fills the three seams: `IContentShareLookup` (2.6),
  `ITopicNameLookup` (3.10) and the account extensions (4.3).
- **Requirement:** FR-058 in `Documentation/Product/Requirements.md`.
- No byte an original server sends upstream curl changes; the MQTT 3.1.1 server's move is proved by
  its existing `UpstreamCurl…SurlOverMqtt…Tests` staying green, and its new reach by BL-640.
