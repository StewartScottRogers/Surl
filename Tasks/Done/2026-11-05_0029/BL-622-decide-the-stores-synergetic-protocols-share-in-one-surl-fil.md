---
id: BL-622
title: Decide the stores synergetic protocols share in one surl: files, mail, the message bus and accounts
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-622 — Decide the stores synergetic protocols share in one surl: files, mail, the message bus and accounts

## Goal

An ADR decides that, in one `surl` process, servers that do the same job share one store - one file store, one
mail store, one new message bus, one account set - on disk under `--directory` or in memory, and how each
original and extended server maps its own names onto that store, so that a file uploaded over FTP is read over
HTTP and S3, and a message published over MQTT is consumed over AMQP or STOMP.

## Context

- Approved by Stewart on 2026-11-04 ("Layer 1 - shared stores across synergetic protocols"). The approval is
  Stewart's and is not reopened; the design is decided by Claude under Stewart's delegation (root `CLAUDE.md`,
  "Decisions"). Layer 2 (the configuration options that name mailboxes, shares, topics and groups) is decided
  by two later ADR tasks that depend on this one; this ADR only leaves room for it.
- What exists, to read first:
  - **Files.** `Foundation/Surl.Content.UnitLibrary` (`ContentStore`, `IContentFileSystem`,
    `InMemoryContentFileSystem`, `DiskContentFileSystem`), ADR-0002 decision 2, ADR-0009, ADR-0015, ADR-0031.
    One `ContentStore` is composed in `Application/Surl.Console/CommandLineRunner.cs` (`ComposeContentStore`)
    and handed to every original file-serving server (HTTP, FTP, SCP/SFTP, TFTP, Gopher, SMB, RTSP).
    ADR-0084 (line 225 onward) already says an S3 bucket, Azure container or OCI repository is a directory
    under the content store; this ADR makes that binding for all four object-storage servers and says what the
    store cannot hold (object metadata such as `Content-Type` and `x-amz-meta-*` set at upload, ETags, key names
    that are not valid paths, OCI blobs, manifests and tags - the OCI Image Layout specification's
    `blobs/<alg>/<hex>` and `index.json` is the planning assumption for a repository's folder).
  - **Mail.** ADR-0050 and `Protocols/Mail/Surl.MailStore.UnitLibrary` (`MailboxStore`); BL-207 composed one
    store for SMTP, IMAP and POP3. It stays; this ADR records it as the model the other stores follow and leaves
    ADR-0050 decision 5 ("the domain is ignored") to the layer-2 ADR.
  - **Messages.** The original MQTT 3.1.1 server, `Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary`
    (ADR-0014: every publish is kept as its topic's retained message, a subscribe is one-shot;
    `MqttRetainedMessages`, persisted by `MqttRetainedMessageFile` at `<path>/.surl/mqtt/retained-messages`,
    ADR-0031 decision 6). The extended brokers are planned: Mqtt5 (ADR-0093, whose decision 0 says `mqtt5`'s
    retained messages are **not** shared with `mqtt` - this ADR amends that), Amqp0 (BL-503..508), Amqp1
    (BL-509..515), Kafka (BL-516..523), Nats (BL-524..529), Stomp (BL-530..534). Their decision tasks
    (BL-503, BL-509, BL-516, BL-524, BL-530) now wait on this ADR.
  - **Accounts.** ADR-0032 (`--user`, `--user-file`, one `AccountBook` and `AuthenticationPolicy` in
    `Foundation/Surl.Authentication.UnitLibrary`), ADR-0084 decision 4 (extended servers judge logins through
    the same contracts).
  - **Isolation.** ADR-0002 decision 3's horizontal-library table and
    `Foundation/Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`; ADR-0079 (category folders);
    ADR-0083 ("Isolation", decision 1's oracle exception for `Surl.Extended.Protocol.*`).
  - **Composition.** BL-611 decides, and BL-609 builds, `Application/Surl.Composition.UnitLibrary`, where
    protocol servers and their service state will be composed once for `surl`, `Surl.Api` and `Surl.Mcp`.
- Oracle. Original protocols: pinned upstream curl 8.21.0 only (ADR-0003); nothing in this ADR may change a byte
  an original server sends upstream curl today, and every existing `UpstreamCurl…SurlOver…Tests` stays green.
  Extended protocols: their specifications (ADR-0083 decision 1); the Curl port is a live client in
  `Integration` tests only, never evidence.

Decide and record:

1. **One store per job, composed once per process.** The file store, the mail store, the message bus and the
   account set each exist once per `surl` run (and once per `Surl.Api` server), are composed in
   `Surl.Composition`, and are handed to every server that does that job; never a static.
2. **The file store and the object-storage servers.** S3, AzureStorage, GoogleCloudStorage and OciRegistry
   serve the same `ContentStore` as the original servers: bucket, container or repository = top-level folder,
   object = file under it, `/` in a key = folder. Name the keys and bucket names the store refuses, and the
   error each server answers for one. Decide where object metadata and ETags live (planning assumption: a
   bounded sidecar index under `<path>/.surl/objects/`, never served, ADR-0031 decision 5), and what an object
   written over FTP (no metadata) reports when read over S3. Decide the OCI repository layout inside its folder.
   Leave a seam for layer 2's named shares and `--bucket` mapping (the store resolves a top-level name through
   one lookup the layer-2 ADR fills in).
3. **The message bus.** A new horizontal library. Name it, place it per ADR-0079 (planning assumption:
   `Protocols/Messaging/Surl.MessageBus.UnitLibrary` and `.UnitTests`, namespace `Surl.MessageBus`, referencing
   `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.Content.UnitLibrary` for `IContentFileSystem`), and amend
   ADR-0002 decision 3's table with its row, as ADR-0050 did. Decide:
   - the one canonical topic name and how each protocol's names map onto it: MQTT `a/b` with `+`/`#`, NATS
     `a.b` with `*`/`>`, AMQP 0-9-1 exchanges, routing keys and queues (`amq.topic` binding keys `a.b` with
     `*`/`#`), AMQP 1.0 link addresses, Kafka topic and partition, STOMP `/topic/…` and `/queue/…`
     destinations; which characters do not survive a mapping and what a server answers for one;
   - what the bus holds: live fan-out to subscriptions, one retained message per topic, named queues with
     competing consumers and acknowledgement and redelivery, and an append-only log per topic with offsets for
     Kafka (or say which of these stay inside one broker and why);
   - which semantics survive translation and which are dropped or approximated: QoS 0/1/2, retained, durable,
     acknowledgements, ordering per topic, message expiry, headers and properties (MQTT 5 user properties, AMQP
     application properties, NATS and STOMP headers, Kafka record headers), payload bytes always exact;
   - the original MQTT 3.1.1 server's retained messages move onto the bus with ADR-0014's wire behaviour
     unchanged, and what happens to an existing `<path>/.surl/mqtt/retained-messages` file (planning assumption:
     still read at start, so a data directory written by an older surl keeps its messages);
   - persistence under `<path>/.surl/bus/` (ADR-0031 decision 6 and ADR-0050 decision 7's rules: loaded once at
     start after the data-directory lock, malformed is `CouldNotReadFile` (37), a failed write is noted and
     never ends serving), and in memory without `--directory`;
   - bounds as constructor parameters with named defaults (ADR-0006: every store a peer can fill is bounded):
     topics, retained bytes, queues, queued messages and bytes per queue, log bytes per topic, subscriptions per
     connection; and the typed outcome a server maps onto its own protocol's refusal;
   - amend ADR-0093 decision 0: `mqtt5` retained messages and routing go through the bus;
   - whether Redis pub/sub channels (BL-491) join the topic space; if yes, name the follow-up.
4. **Accounts.** One account set for every server, original and extended: list each extended server's login
   (S3 SigV4 access key, Azure Shared Key and bearer, GCS HMAC and bearer, OCI token endpoint, MQTT 5 user name
   and password and enhanced auth, AMQP SASL, Kafka SASL, NATS user/password/token, STOMP login/passcode, XMPP
   SASL, Redis AUTH, CoAP's DTLS PSK identity, DNS none) and the account field it reads; any server whose ADR
   (finished or not) reads a private credential source gets a named follow-up. LDAP as an account source is
   layer 2's last item and is not decided here.
5. **Cross-protocol tests.** Which pairs prove the sharing and with which client:
   - pinned upstream curl at both ends where it speaks both: `smtp` send then `imap` and `pop3` fetch; `ftp`
     upload then `http` GET; `sftp` upload then `ftp` fetch; `mqtt` publish on one listener then `mqtt` subscribe
     on another (`mqtt` and `mqtts`), and on a restart with `--directory`;
   - the Curl port's pinned client (`CurlPortBuilds.json`, ADR-0083 decision 2) for an extended end, in
     `Integration` tests only: upload over `ftp` with upstream curl then GET over `s3`, `az`, `gs`; publish over
     `mqtt` with upstream curl then consume over `amqp`, `stomp`, `nats`, `kafka`, `mqtt5`. The specification
     decides what each end must show; the port only completes the exchange.
   Name the test classes (planning: `UpstreamCurlSharesFilesAcrossSurlProtocolsTests`,
   `UpstreamCurlSharesMailAcrossSurlProtocolsTests`, `UpstreamCurlSharesTopicsAcrossSurlListenersTests`,
   `CurlPortClientReadsFilesSharedAcrossSurlProtocolsTests`, `CurlPortClientConsumesMessagesSharedAcrossSurlProtocolsTests`
   in `Conformance/Surl.Conformance.UnitTests`).

## Acceptance criteria

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation" and citing Stewart's approval of 2026-11-04. It answers decisions 1 to 5,
      each mapping and each dropped semantic citing the specification section (MQTT 3.1.1 and 5.0, AMQP 0-9-1,
      AMQP 1.0, the Kafka protocol guide, the NATS client protocol, STOMP 1.2, the S3, Azure, GCS and OCI
      references) it rests on, and is listed in `Documentation/Planning/Decisions/README.md`.
- [x] `ADR-0002-mirror-the-curl-ports-project-map.md` carries an "Amended" line naming the new ADR, and decision
      3's table has the message bus library's row with what it may reference.
- [x] `ADR-0093-how-the-mqtt-5-broker-answers-mqtt5-mqtt5s-and-mqtt5-ws.md` carries an "Amended" line naming
      the new ADR for decision 0's retained-message sentence.
- [x] `Documentation/Product/Requirements.md` has one new functional requirement row (next free FR number) for
      the shared stores, citing the ADR, marked Draft, with "Not built yet" naming the build tasks.
- [x] The ADR's Consequences section lists the build tasks by ID (the shared-store tasks filed on 2026-11-04:
      find them on the board by the titles naming `Surl.MessageBus`, the content store's object metadata, and the
      cross-protocol tests) and says which existing extended-server tasks it changes.

## Notes

- ADR-0100 written. Main choices, each argued in the ADR: MQTT's topic names are the canonical
  topic space (the original, curl-measured server; every other protocol's names map into it);
  NATS, AMQP 0-9-1 and Kafka map `.` to `/`; AMQP 1.0 and STOMP use `/topic/` and `/queue/` (both
  specifications leave addresses opaque); a canonical topic a dotted protocol cannot name is simply
  unreachable from it, never escaped; every topic gets a bounded log so Kafka can read what MQTT
  published earlier; retained messages are MQTT-only to read; bus filters allow `#` anywhere, for
  AMQP 0-9-1 binding keys.
- Object metadata: one sidecar index under `.surl/objects`, stamped with size and last-write time,
  dropped as stale when another protocol replaces the file; ETag = MD5 hex for files without an entry.
- Redis pub/sub joins the topic space (decision 3.8); filed follow-up BL-678 (feature,
  depends on BL-491, BL-629, BL-630).
- Also amended ADR-0084 (decision 5's metadata question, decision 4's account fields): it sits in
  `Documentation/Planning/Decisions`, inside this task's `touches`.
- Per-server wire bytes for refused keys and bus outcomes are planning answers; each server's own
  decision task pins them against its reference.
- The ADR number 0100 was the next free one in this lane's checkout; if another lane lands an
  ADR-0100 first, the integration renumbers.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. ADR-0100 decides one file, mail, message-bus and account store per surl process, with FR-058 and ADR-0002, ADR-0084 and ADR-0093 amended
