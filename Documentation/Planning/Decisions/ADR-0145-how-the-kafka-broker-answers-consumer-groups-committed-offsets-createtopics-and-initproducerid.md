# ADR-0145 — How the Kafka broker answers consumer groups, committed offsets, CreateTopics and InitProducerId

- **Status:** Accepted
- **Date:** 2026-11-06
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-06,
  in BL-701. BL-929 adds the event contracts (decision 10), BL-930 keeps committed offsets and group
  access in `Surl.MessageBus` (decisions 4 and 6), BL-931 builds the group coordinator's membership
  APIs (decisions 1 to 3), BL-932 OffsetCommit, OffsetFetch, DescribeGroups and ListGroups (decisions 4
  and 5), BL-933 CreateTopics (decision 7), BL-934 InitProducerId and sequence checking (decision 8),
  and BL-935 composes them in `surl` (decision 0).
- **Builds on:** [ADR-0105](ADR-0105-how-the-kafka-broker-answers-kafka-and-kafkas.md) (the broker,
  its version rule, one partition a topic, no transactions),
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decisions 3.2, 3.3, 3.6 and 3.7 (the bus, its names, bounds and persistence, built by BL-633 and
  BL-634), [ADR-0102](ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md)
  decision 3.4 (topic access),
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) (secure by
  default), [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (every store a peer can fill is
  bounded) and [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) (events, seed, reset, snapshot,
  restore, faults, determinism).
- **Amends:** ADR-0100 decision 3.3's sentence "Kafka's consumer groups and committed offsets stay
  inside the Kafka broker": consumer groups do, committed offsets move into the bus (decision 0).
  ADR-0102 decision 3.4: access rows gain `$group/<name>` (decision 6). ADR-0105 decision 1's "Not
  served yet" bullet (decision 1 below) and decision 4's "its sequence is not checked" (decision 8).
  ADR-0124 decision 1's contract: a new event record and a new `StoreChange` member (decision 10).
  ADR-0105 decision 7's limits: a held JoinGroup or SyncGroup pauses the idle clock (decision 9).

## Context

ADR-0105 decision 1 left the consumer group coordinator, CreateTopics and InitProducerId unserved:
they close the connection like any unserved API, and ApiVersions does not list them. A consumer that
assigns its partition itself needs none of them, but a test suite using Surl as a stand-in Kafka
(root `CLAUDE.md`, "Surl is a stand-in for tests") usually runs a subscribing consumer in a group,
creates its topics through an admin client, and produces with the Java client's default idempotence.

As in ADR-0105, the broker is judged by its specification (ADR-0083 decision 1), never by the Curl
port. The specification read, and pinned, is **the Apache Kafka protocol guide for Apache Kafka 4.4**
(<https://kafka.apache.org/44/design/protocol>, read 2026-11-06; below "the guide"): its "Api Keys"
table, "Error Codes" table and the schemas and field descriptions in "The Messages". The guide gives
each field's meaning and each error's description but no prose on how a group coordinator runs a
rebalance; where it is silent, the decision below says so and gives its reason.

The guide's version ranges for 4.4, and the first flexible version of each:

| API (key) | Versions in the guide | First flexible version |
| --- | --- | --- |
| OffsetCommit (8) | 2 to 10 | 8 |
| OffsetFetch (9) | 1 to 10 | 6 |
| FindCoordinator (10) | 0 to 6 | 3 |
| JoinGroup (11) | 0 to 9 | 6 |
| Heartbeat (12) | 0 to 4 | 4 |
| LeaveGroup (13) | 0 to 5 | 4 |
| SyncGroup (14) | 0 to 5 | 4 |
| DescribeGroups (15) | 0 to 6 | 5 |
| ListGroups (16) | 0 to 5 | 3 |
| CreateTopics (19) | 2 to 7 | 5 |
| InitProducerId (22) | 0 to 6 | 2 |
| ConsumerGroupHeartbeat (68) | 0 to 1 | 0 |

What the broker does today (`Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary`):
`KafkaServedApis` lists Produce, Fetch, ListOffsets, Metadata, SaslHandshake, ApiVersions and
SaslAuthenticate only; `KafkaProduceAnswerer` answers a non-null `transactional_id` with
`UNSUPPORTED_FOR_MESSAGE_FORMAT` (43); `KafkaRecordBatchReader` rejects the transactional and control
attributes and checks no `producerId`, `producerEpoch` or `baseSequence`. The broker consults no
fault plan and publishes no event of its own; `Surl.MessageBus` publishes its store changes, and
`MessageBus.CreateLog` already gives a topic its empty log and publishes `StoreChange.Written`.

## Decision

### 0. Where the state lives

- **Group membership is the broker's**: a new `KafkaGroupCoordinator` in the Kafka library holds
  each group's members, generation, state, selected protocol, leader and assignments, in memory. It
  is live session state, like the bus's live subscriptions, so it is never persisted, snapshotted or
  reset. One coordinator serves one `surl` instance (one `Surl.Api` server), shared by its `kafka` and
  `kafkas` listeners as the bus is; BL-935 composes it.
- **Committed offsets are the bus's** (amends ADR-0100 decision 3.3). `Surl.MessageBus` keeps a
  table of committed offsets keyed by consumer group and canonical topic, which only the Kafka broker
  reads or writes; it is not a message and no other protocol sees it. **Why:** a committed offset
  points into a topic log, so it must reset, snapshot, restore and persist with that log. Kept in the
  broker, a bus reset between two tests would leave a group's offset past the end of an empty log,
  and the next test's consumer would start from wherever its `auto.offset.reset` sends it; ADR-0124
  decision 3's operations, the bus's fault rules and its persistence would each need a fifth store to
  cover what the bus already covers.
- **Producer ids and sequence state are the broker's**, in memory (decision 8): bookkeeping a
  restart may lose without harm, as decision 8's first-seen rule makes sure.

### 1. The APIs and versions served (the guide, "Api Keys", "The Messages")

ADR-0105 decision 1's rule holds: the non-flexible versions only, so every request is read with
request header v1 and answered with response header v0. **Why non-flexible is enough:** Kafka 4's
clients still talk to brokers back to Kafka 2.1 (KIP-896), and no API had a flexible version before
Kafka 2.4 (KIP-482), so every current Java client and librdkafka can negotiate a version in each
range below.

| API (key) | Served versions | Why this range |
| --- | --- | --- |
| FindCoordinator (10) | **0 to 2** | 0 is the guide's lowest; 2 the last non-flexible. v4's batched `coordinator_keys` is flexible. |
| JoinGroup (11) | **0 to 5** | 0 is the guide's lowest; 5 the last non-flexible, and the first with `group_instance_id` (static membership). v4 adds `MEMBER_ID_REQUIRED` (decision 3). |
| Heartbeat (12) | **0 to 3** | the whole non-flexible range; v3 adds `group_instance_id`. |
| LeaveGroup (13) | **0 to 3** | the whole non-flexible range; v3 leaves a batch of members. |
| SyncGroup (14) | **0 to 3** | the whole non-flexible range; v3 adds `group_instance_id`. |
| OffsetCommit (8) | **2 to 7** | 2 is the guide's lowest; 7 the last non-flexible. |
| OffsetFetch (9) | **1 to 5** | 1 is the guide's lowest; 5 the last non-flexible. v7's `require_stable` is flexible; a Java consumer that would set it falls back without it. |
| DescribeGroups (15) | **0 to 4** | the whole non-flexible range. |
| ListGroups (16) | **0 to 2** | the whole non-flexible range. |
| CreateTopics (19) | **2 to 4** | 2 is the guide's lowest; 4 the last non-flexible, and the version whose `num_partitions` and `replication_factor` may be -1 for the default. |
| InitProducerId (22) | **0 to 1** | the whole non-flexible range; v3's `producer_id` and `producer_epoch` (epoch bumps) are flexible and belong to transactions. |

- **ApiVersions lists each API once its task has built it**, and never before (ADR-0105 decision 1):
  each build task adds its rows to `KafkaServedApis`.
- **Not served: the KIP-848 consumer group protocol** (ConsumerGroupHeartbeat (68), and
  ConsumerGroupDescribe (69) with it), **share groups and streams groups**. Each closes the connection
  with no bytes, as any unserved API does, and ApiVersions does not list them, so a client knows not
  to send them. **Why:** every Kafka client still offers the classic protocol (the Java consumer's
  `group.protocol` is `classic` by default), the KIP-848 protocol is flexible from its first version,
  and it moves partition assignment into the broker, which a one-partition-a-topic broker gains
  nothing from. A later ADR may serve it.

### 2. FindCoordinator (the guide, FindCoordinator v0 to v2)

- **The coordinator is node 0**, the one broker: `node_id` 0, `host` and `port` the same advertised
  address Metadata gives (ADR-0105 decision 2's rule), `error_code` 0, `error_message` null.
- **`key_type`** (v1 and v2; v0 has none and means a group): 0 (group) is answered as above, after the
  group access check (decision 6). **1 (transaction), 2 (share) or any other value is
  `INVALID_REQUEST` (42)** with `node_id` -1, `host` empty and `port` -1. **Why 42:** there is no
  transaction coordinator (ADR-0105 decision 4) and no share group coordinator; the guide describes 42
  as a request "sent to an incompatible broker", which is exactly the case, and a client treats it as
  fatal at once. `COORDINATOR_NOT_AVAILABLE` (15) is retriable, so a transactional producer would
  retry until its own timeout and fail a test a minute late; `TRANSACTIONAL_ID_AUTHORIZATION_FAILED`
  (53) would claim an authorization rule that does not exist.

### 3. Group membership: JoinGroup, SyncGroup, Heartbeat, LeaveGroup (the guide's schemas and error descriptions)

The guide defines the fields and the errors but not the rebalance itself; the rules below are the
classic group protocol as its fields and errors describe it, made deterministic on one node.

- **States**, as DescribeGroups names them (decision 5): `Empty`, `PreparingRebalance`,
  `CompletingRebalance`, `Stable`, and `Dead` for a group that does not exist.
- **JoinGroup checks, in order:** an empty `group_id` is `INVALID_GROUP_ID` (24); no group access
  `GROUP_AUTHORIZATION_FAILED` (30, decision 6); `session_timeout_ms` outside **6000 to 1800000** is
  `INVALID_SESSION_TIMEOUT` (26) (the guide's description names `group.min.session.timeout.ms` and
  `group.max.session.timeout.ms`; these are their Kafka defaults); an empty `protocol_type` or
  `protocols` array is `INCONSISTENT_GROUP_PROTOCOL` (23); a non-empty `member_id` the group does not
  hold is `UNKNOWN_MEMBER_ID` (25); a static member's `member_id` that is not the one its
  `group_instance_id` holds is `FENCED_INSTANCE_ID` (82); a new member past the bounds is
  `GROUP_MAX_SIZE_REACHED` (81) or `POLICY_VIOLATION` (44) (decision 9); a `protocol_type` other than
  the group's, or a `protocols` list sharing no name with every other member's, is
  `INCONSISTENT_GROUP_PROTOCOL` (23).
- **`protocol_type`** is any non-empty string (`consumer` from every consumer, others from Kafka
  Connect and the like); the first member sets the group's, and it is cleared when the group empties.
- **Member ids.** A new dynamic member gets `<client_id>-<uuid>`, a static member
  `<group_instance_id>-<uuid>`, where `<uuid>` is 16 bytes from `ExchangeContext.Identifiers` written
  as a lower-case 8-4-4-4-12 hexadecimal UUID (ADR-0124 decision 6: an identifier that grants nothing,
  so seedable). A null `client_id` is written as empty.
- **`MEMBER_ID_REQUIRED` (79) on v4 and v5.** A dynamic member joining with an empty `member_id` is
  answered 79 with its new id in `member_id` and held as pending; it joins when it sends JoinGroup
  again with that id within its session timeout, and is forgotten otherwise (the guide: "The group
  member needs to have a valid member id before actually entering a consumer group"). On v0 to v3,
  and for a static member, the member joins at once with its new id.
- **Static membership (v5).** A JoinGroup naming a `group_instance_id` another member id holds gives
  the instance a new member id and fences the old one: a later request carrying the old id with that
  instance id is `FENCED_INSTANCE_ID` (82). The group then rebalances as for any join. **Why always
  rebalance:** a rebalance is always a correct answer for every client, and skipping it would need the
  broker to judge whether the member's subscription changed, which the guide does not describe.
- **The join phase.** A join to an `Empty` group, a new member, or a known member rejoining from
  `Stable` or `CompletingRebalance` moves the group to `PreparingRebalance`; each JoinGroup is held
  until the phase ends. The phase ends when every member of the group has a JoinGroup waiting, or when
  the **rebalance timeout** has passed since it began - the largest `rebalance_timeout_ms` among the
  members (v0, which has no such field, uses `session_timeout_ms`), a negative value read as 0, at most
  1800000. Members with no JoinGroup waiting then are removed. There is **no initial rebalance
  delay**: Kafka's `group.initial.rebalance.delay.ms` is a broker setting the guide does not name, and
  a stand-in group of one should be served at once; a member joining later triggers a rebalance,
  which every client handles.
- **When the phase ends**, `generation_id` grows by one (from 0, so the first generation is 1); the
  **leader** is the leader of the previous generation when it rejoined, otherwise the member that
  joined earliest; the **protocol** is the first name in the leader's `protocols` list that every
  member lists. Every waiting JoinGroup is answered `error_code` 0 with the generation, the protocol
  name, the leader and its own member id; the leader's answer lists every member in join order with
  its `group_instance_id` (v5) and its metadata for the chosen protocol, the others' an empty array.
  The group moves to `CompletingRebalance`. **Why not a vote:** Kafka's coordinator lets each member
  vote for its own first common choice; with one protocol list shared by every member (the usual
  case) both rules choose the same name, and the leader's order is deterministic where a tied vote is
  not.
- **SyncGroup.** Checks, in order: group access (30); unknown group or member (25); a fenced
  instance (82); a `generation_id` other than the group's `ILLEGAL_GENERATION` (22); a group in
  `PreparingRebalance` `REBALANCE_IN_PROGRESS` (27). In `CompletingRebalance` a member's SyncGroup is
  held until the leader's arrives. The **leader's `assignments` are relayed verbatim**: each member
  named gets exactly the bytes the leader gave it, never decoded or checked; a member the leader did
  not name gets empty bytes; a name that is not a member is ignored. Every held SyncGroup is answered
  and the group moves to `Stable`. In `Stable` a SyncGroup is answered at once with the member's
  assignment.
- **Heartbeat.** Checks: group access (30); unknown group or member (25); fenced (82); wrong
  generation (22); then `REBALANCE_IN_PROGRESS` (27) in `PreparingRebalance`, and 0 in
  `CompletingRebalance` and `Stable`.
- **Sessions.** Each member has a deadline `session_timeout_ms` after its last JoinGroup, SyncGroup,
  Heartbeat or OffsetCommit. A member past it is removed and the group rebalances; a member whose
  JoinGroup is waiting for the join phase is not expired (the rebalance timeout bounds that wait).
  Closing a connection removes no member: the session timeout does. Every deadline is an `ITimer` from
  the coordinator's injected `TimeProvider` (the instance's, ADR-0124 decision 6), never polling or
  `Thread.Sleep`.
- **LeaveGroup.** v0 to v2 remove `member_id` (`UNKNOWN_MEMBER_ID` (25) when the group does not hold
  it); v3 removes each of `members` by `member_id`, or by `group_instance_id` with an empty member id,
  answering each with 0, 25, or 82 when a member id does not match its instance's, and the top-level
  `error_code` 0. Group access is checked first (30, top-level). Removing a member of a group in
  `CompletingRebalance` or `Stable` rebalances it; the last member leaving moves it to `Empty`.
- **A group with no members and no committed offsets is forgotten**, and is `Dead` from then on.

### 4. Committed offsets: OffsetCommit and OffsetFetch (the guide, OffsetCommit v2 to v7, OffsetFetch v1 to v5)

- **Where.** In `Surl.MessageBus` (decision 0), keyed by group id and canonical topic, the Kafka name
  mapped through `MessageBus.Names` for the `kafka` scheme as Metadata maps it. Each entry holds
  `committed_offset` as given, `committed_leader_epoch` (v6 and v7; -1 before), `committed_metadata`
  (null kept null) and the commit time from the bus's `TimeProvider`.
- **No expiry by time.** `retention_time_ms` (v2 to v4) is read and ignored; an offset lives until it
  is replaced, the bus is reset or restored, or the bounds of decision 9 refuse a new one. **Why:**
  Kafka's own default keeps offsets for 7 days, longer than any test, and a time-based expiry would
  make a test depend on the clock; the bound keeps the table finite instead.
- **OffsetCommit, who may commit.** `generation_id_or_member_epoch` -1 with an empty `member_id` is a
  **simple commit** (a consumer that assigns its partitions itself): allowed when the group is
  `Empty` or does not exist, which creates it `Empty` with an empty `protocol_type`; when the group has
  members it is `UNKNOWN_MEMBER_ID` (25) for every partition. Otherwise the member must be known (25),
  not fenced (82), and of the current generation (22); a commit in `CompletingRebalance` is
  `REBALANCE_IN_PROGRESS` (27), and one in `PreparingRebalance` or `Stable` is taken. A member's commit
  counts as a heartbeat (decision 3). Group access (30) is checked first, for every partition.
- **OffsetCommit, each partition**, in order: an invalid topic name `INVALID_TOPIC_EXCEPTION` (17); a
  partition other than 0, or a topic with no log, `UNKNOWN_TOPIC_OR_PARTITION` (3); no subscribe access
  to the topic `TOPIC_AUTHORIZATION_FAILED` (29); `committed_metadata` longer than **4096** bytes in
  UTF-8 `OFFSET_METADATA_TOO_LARGE` (12) (Kafka's `offset.metadata.max.bytes` default); the bus's
  bounds or an out-of-space fault `POLICY_VIOLATION` (44) (ADR-0100 decision 3.6's Kafka column);
  otherwise stored, 0. Each partition stands alone: one failing does not undo another.
- **OffsetFetch.** Group access is checked first: in v2 to v5 a denied group is the top-level
  `error_code` 30 with no topics, in v1 every partition asked is 30. v1 asks for named topics; v2 to
  v5 may send null `topics` for every topic the group has committed, in ordinal order of Kafka name.
  Each partition: invalid name 17; no subscribe access 29; a partition other than 0 3; otherwise its
  committed offset, leader epoch (v5) and metadata, or `committed_offset` -1, leader epoch -1 and
  metadata empty when none is committed or the topic does not exist, with error 0. The top-level
  `error_code` (v2 to v5) is 0 otherwise. A group that does not exist answers every partition -1.
- **Persistence (ADR-0100 decision 3.7, beside BL-634's files).** Under `--directory <path>` the table
  is `<path>/.surl/bus/offsets`: the ASCII header `SURL-BUS-OFFSETS-1` and a line feed, the entry
  count (4 bytes), then per entry in ordinal order of group, then of topic: the group and the topic
  (strings), the offset (8 bytes), the leader epoch (4 bytes), a flags byte (`0x01` metadata follows,
  no other bit), the metadata (string) when flagged, and the commit time (8 bytes, UTC ticks); every
  integer big-endian and every string a 4-byte length and UTF-8, as the other bus files. It is
  rewritten whole through a temporary name and renamed over the old file, by `SaveChangesAsync` after
  a change; loaded once by `LoadAsync`, a file that does not parse or holds more than the bound is
  `not a message bus file` (`CouldNotReadFile`, 37). Without `--directory` it is in memory.
- **Reset, snapshot and restore (ADR-0124 decision 3).** `MessageBus.Reset` empties the table (no
  configuration declares an offset); `TakeSnapshot` copies it, counting its bytes against
  `MaxSnapshotBytes`; `Restore` puts it back. A bus on disk refuses all three, as today. **No seed
  method:** a test seeds an offset with a simple commit from its own client, which needs no group
  membership.

### 5. DescribeGroups and ListGroups (the guide, DescribeGroups v0 to v4, ListGroups v0 to v2)

- **DescribeGroups**, one row per group asked, in request order: a group without access is 30 with
  every other field empty (the same whether or not it exists); a group that does not exist is error 0,
  `group_state` `Dead`, other strings empty and no members; otherwise error 0, the state's name,
  `protocol_type`, `protocol_data` the chosen protocol name in `CompletingRebalance` and `Stable` and
  empty otherwise, and each member in join order with `group_instance_id` (v4), the `client_id` of its
  latest JoinGroup, `client_host` as `/<address>` of that request's remote address, its metadata for the
  chosen protocol, and its assignment in `Stable` (empty otherwise). `authorized_operations` (v3 and v4)
  is `-2147483648` unless `include_authorized_operations` asks, then READ (3) and DESCRIBE (8) set,
  the operations the broker serves on a group (ADR-0105 decision 2's bit set).
- **ListGroups** answers error 0 and every group the caller has access to - every group with a member
  or a committed offset - in ordinal order of group id, each with its `protocol_type` (empty for a
  group made by a simple commit or known only by its offsets).

### 6. Group access (amends ADR-0102 decision 3.4)

- **Groups are named in `--topic-access` rows as the filter `$group/<group id>`**, the group id one
  level whatever it holds, as ADR-0102 decision 3.5 rules for `$queue/<name>`; the `$` rule keeps a `#`
  grant from opening every group. **`sub` (or `pubsub`) on `$group/<id>` lets a caller use the group**:
  FindCoordinator for it, JoinGroup, SyncGroup, Heartbeat, LeaveGroup, OffsetCommit, OffsetFetch and
  DescribeGroups. `pub` alone grants nothing on a group.
- **With no `--topic-access`, every caller the broker lets in may use every group**, as for topics
  today. **Why this is secure by default:** the broker already lets no one in without a login unless
  `--allow-anonymous` is given (ADR-0032; ADR-0105 decision 5), and once any `--topic-access` row is
  given every group needs a grant, as every topic and queue does. Leaving groups open after a topic
  rule would let an account that may read nothing commit and fetch offsets for any group; requiring
  grants with no rule at all would break every test that gives no access rows.
- **A denied caller gets `GROUP_AUTHORIZATION_FAILED` (30)**, the same whether or not the group exists
  (ADR-0006 section 3), and ListGroups leaves the group out. OffsetCommit and OffsetFetch also need
  subscribe access to each topic (29); CreateTopics needs publish access (decision 7).
- `Surl.MessageBus` answers the check (`TopicAccessPolicy` gains a group check beside `MayUseQueue`);
  the `--topic-access` help and `--aihelp` text name `$group/<id>` (BL-935).

### 7. CreateTopics (the guide, CreateTopics v2 to v4; ADR-0100 decision 3.2)

- **Each topic, in request order, checked in order:** a name given twice in the request is
  `INVALID_REQUEST` (42) for each occurrence; an invalid Kafka name `INVALID_TOPIC_EXCEPTION` (17); no
  publish access to its canonical topic `TOPIC_AUTHORIZATION_FAILED` (29) (ADR-0102 has no "create"
  right, and Produce already creates a topic under publish access); a topic that has a log
  `TOPIC_ALREADY_EXISTS` (36); `num_partitions` other than 1 or -1 `INVALID_PARTITIONS` (37) (ADR-0100
  decision 3.2's row for more than one; the guide's description for below 1); `replication_factor`
  other than 1 or -1 `INVALID_REPLICATION_FACTOR` (38) (the guide: "below 1 or larger than the number
  of available brokers"); non-empty `assignments` other than exactly partition 0 on broker `[0]`
  `INVALID_REPLICA_ASSIGNMENT` (39); then the topic's empty log is made with `MessageBus.CreateLog`,
  whose `TooManyTopics` is `POLICY_VIOLATION` (44). -1 means the default, 1, in every served version.
- **`configs` are accepted and ignored**, and the exchange log notes the names ignored with `-v`.
  **Why:** a bus topic has no per-topic configuration (retention is the bus's, ADR-0105 decision 3),
  and admin clients and Kafka Streams send configurations such as `cleanup.policy` with every topic
  they create; refusing them with `INVALID_CONFIG` (40) would break those clients for no protection.
- **`validate_only`** makes every check above but `CreateLog`'s bound and creates nothing; the answers
  are those a real creation would give. **`timeout_ms`** is read and ignored: creation is immediate.
- The response echoes each name with its `error_code` and a null `error_message`.
  `CreateLog` already publishes `StoreChange.Written` for the new topic.

### 8. InitProducerId and idempotent producers (the guide, InitProducerId v0 to v1, Produce; amends ADR-0105 decision 4)

- **A null `transactional_id`** (an idempotent producer) is answered error 0, a new `producer_id` and
  `producer_epoch` 0; `transaction_timeout_ms` is ignored. Producer ids are unique on the instance and
  rise by one: the first is drawn from the first InitProducerId exchange's
  `ExchangeContext.Identifiers` (8 bytes, big-endian, masked to 47 bits), so a seeded instance hands
  the same ids every run (ADR-0124 decision 6) and an unseeded one does not reuse the ids of an
  earlier run whose producers may still be retrying.
- **A non-null `transactional_id`, empty included, is `INVALID_REQUEST` (42)** with `producer_id` -1
  and `producer_epoch` -1, for decision 2's reason: there are no transactions (ADR-0105 decision 4),
  and a fatal answer fails a transactional producer at once. A Produce with a `transactional_id` stays
  `UNSUPPORTED_FOR_MESSAGE_FORMAT` (43) as ADR-0105 decides.
- **Sequence numbers are checked** (replaces ADR-0105 decision 4's "its sequence is not checked").
  **Why:** a producer's retry after a lost response is exactly what idempotence exists for, and a
  stand-in that appended the retry twice would hide the duplicate a real broker removes. For a batch
  with `producerId` 0 or above, against the state held for that producer on that topic (partition 0):
  - **no state held** (a producer first seen, or its state dropped by decision 9's bound or a restart):
    any `baseSequence` is accepted and starts the state, so losing state never fails a producer;
  - `producerEpoch` below the state's is `INVALID_PRODUCER_EPOCH` (47); above it replaces the state as
    if none were held;
  - a batch whose first and last sequence equal one of the **last five batches** taken is a
    **duplicate**: answered error 0 with that batch's original `base_offset`, nothing appended;
  - a `baseSequence` one past the last taken (2147483647 wraps to 0) is taken;
  - anything else is `OUT_OF_ORDER_SEQUENCE_NUMBER` (45).

  A partition's batches are checked in order, each against the state the earlier ones would leave;
  the partition is a duplicate only when every batch is, and otherwise all or nothing as ADR-0105
  decision 4 says. The check comes after the record size check (10) and before the bus append (44).
  `DUPLICATE_SEQUENCE_NUMBER` (46) and `UNKNOWN_PRODUCER_ID` (59) are never sent: the duplicate is
  answered as the success it was, and an unknown producer is first seen. A batch with `producerId`
  -1 is not checked, as today.

### 9. Bounds (ADR-0006)

Constructor parameters with `Default…` constants, as the bus's (ADR-0100 decision 3.6); no option sets
them:

| Bound | Default | Where | Answer when hit |
| --- | --- | --- | --- |
| `DefaultMaxGroups` | 1000 | groups the coordinator holds (with members or pending) | `POLICY_VIOLATION` (44) to the JoinGroup that would make one more |
| `DefaultMaxMembersPerGroup` | 1000 | members of one group, pending ones included | `GROUP_MAX_SIZE_REACHED` (81) to the joining member |
| `DefaultMaxGroupStateBytes` | 268435456 (256 MiB, ADR-0050 decision 6's memory bound) | member metadata and assignments held, all groups | a JoinGroup that would pass it is 44; a leader's SyncGroup that would is 44 to the leader, and the group rebalances |
| `DefaultMaxCommittedOffsets` | 10000 | committed-offset entries in the bus, all groups | a commit making a new entry is 44 for that partition; replacing one is always taken |
| committed metadata | 4096 bytes | one entry | `OFFSET_METADATA_TOO_LARGE` (12) |
| `DefaultMaxProducerStates` | 10000 | producer-and-topic sequence states | none: the least recently used is dropped (decision 8's first-seen rule) |

ADR-0105 decision 7's limits (`--max-message` on every request, `--head-timeout`, `--idle-timeout`,
`--max-time`) apply unchanged, with one addition: **while a JoinGroup or SyncGroup is held** (decision
3), the broker pauses the exchange's idle clock (`ExchangeContext.IdleClock`, ADR-0141) and restarts it
when the answer is written. **Why:** the Java consumer's rebalance timeout is its
`max.poll.interval.ms`, 300000 by default, so a join phase may rightly outlast the 120-s
`--idle-timeout`; the hold is bounded by the rebalance timeout (at most 1800000) and `--max-time`
instead. A held Fetch keeps ADR-0105 decision 6's 60000 cap.

### 10. The stand-in: events, faults, determinism (ADR-0124)

- **Events** (amends ADR-0124 decision 1). `Surl.Protocol.Abstractions` gains
  `ConsumerGroupChangedEvent(long? ExchangeId, string Scheme, string Group, ConsumerGroupChange Change,
  string? MemberId, int GenerationId, string? Account)`, with `ConsumerGroupChange` `MemberJoined`,
  `MemberLeft` (LeaveGroup), `MemberRemoved` (session expired, or not rejoined by the rebalance
  timeout; no exchange), `MemberFenced`, `GenerationStarted` (the join phase ended; no member) and
  `AssignmentDistributed` (the leader's SyncGroup taken; no member). `StoreChange` gains `Committed`:
  the bus publishes one `StoreChangedEvent` for each partition a commit stores, named
  `$group/<group id>/<canonical topic>` with `%` and `/` in the group id percent-encoded, so the name
  splits one way. The control endpoint writes the new kind as `consumer_group_changed` (ADR-0136).
- **Faults.** Each API in decision 1 asks `CommandStageFaults.FindAndApplyAsync` (ADR-0141) after the
  login, with the API's name from the guide's "Api Keys" table as the command (`JoinGroup`) and as the
  path the group id (group APIs), the coordinator key (FindCoordinator), the Kafka topic name
  (CreateTopics, once per topic) or none (ListGroups, InitProducerId). A delay or drop applies there;
  an `error` rule's code (1 to 127, ADR-0124 decision 4's Kafka row) is written in the request's own
  response where its error lives - the top-level `error_code` (FindCoordinator, JoinGroup, SyncGroup,
  Heartbeat, LeaveGroup, OffsetFetch v2 to v5, ListGroups, InitProducerId), each partition's
  (OffsetCommit, OffsetFetch v1), each group row's (DescribeGroups) or each topic's (CreateTopics) -
  and nothing changes. The bus's out-of-space rule at the `StoreWrite` point applies to a commit with
  the event's name as its path. Accepting `kafka` `error` rules in `Surl.Cli` stays BL-834's; until it
  lands the build tasks test with the library's `ScriptedFaultPlan`.
- **Determinism.** Member ids and the first producer id come from `ExchangeContext.Identifiers`
  (decisions 3 and 8); every timer is the coordinator's injected `TimeProvider`; the leader, the
  protocol and every listing follow join order or ordinal order, never a hash order.

## Alternatives considered

- **Keep committed offsets in the broker** (ADR-0100 decision 3.3 as written). Rejected (decision 0):
  a bus reset or restore would leave offsets pointing into logs that no longer hold them, and reset,
  snapshot, restore, persistence and out-of-space would all have to be built a second time.
- **Serve the KIP-848 consumer protocol instead of, or beside, the classic one.** Rejected for now
  (decision 1): every client still speaks the classic protocol by default, and KIP-848's server-side
  assignors buy nothing with one partition a topic.
- **Answer a transactional id with `COORDINATOR_NOT_AVAILABLE` (15) or
  `TRANSACTIONAL_ID_AUTHORIZATION_FAILED` (53).** Rejected (decision 2): 15 makes the client retry
  until its timeout; 53 names a rule that does not exist.
- **Leave sequence numbers unchecked** (ADR-0105 decision 4). Rejected (decision 8): a retried batch
  would be appended twice, the one behaviour an idempotent producer relies on the broker to prevent.
- **Answer CreateTopics' `configs` with `INVALID_CONFIG` (40).** Rejected (decision 7): it breaks
  Kafka Streams and admin clients that set routine configurations.
- **A group access right of its own** (a new option, or a `group` right in `--topic-access`).
  Rejected (decision 6): `$group/<id>` reuses ADR-0102's grammar, its `$` rule and its "global once
  given" rule, as `$queue/<name>` does.
- **Kafka's 3-second initial rebalance delay.** Rejected (decision 3): it slows every test's first
  join and is a broker setting, not protocol.

## Consequences

- With BL-929 to BL-935 built, `surl kafka://127.0.0.1:0/` serves subscribing consumers in groups,
  committed offsets that reset, snapshot, restore and persist with the logs they point into, topic
  creation by admin clients, and idempotent producers whose retries are not appended twice.
- The tasks, in order:
  - **BL-929**: `ConsumerGroupChangedEvent`, `ConsumerGroupChange` and `StoreChange.Committed` in
    `Surl.Protocol.Abstractions`, and the control endpoint's `consumer_group_changed` kind (decision 10).
  - **BL-930**: the committed-offset table, `$group/<id>` access, the `offsets` file, reset, snapshot,
    restore and the `Committed` event in `Surl.MessageBus` (decisions 4, 6, 9 and 10).
  - **BL-931**: `KafkaGroupCoordinator` with FindCoordinator, JoinGroup, SyncGroup, Heartbeat and
    LeaveGroup (decisions 1, 2, 3, 9 and 10).
  - **BL-932**: OffsetCommit, OffsetFetch, DescribeGroups and ListGroups (decisions 1, 4, 5 and 10).
  - **BL-933**: CreateTopics (decisions 1, 7 and 10).
  - **BL-934**: InitProducerId and Produce's sequence checks (decisions 1, 8, 9 and 10).
  - **BL-935**: the coordinator and producer-id state composed once per instance, and the help and
    `--aihelp` text for groups and `$group/<id>` (decisions 0 and 6); it waits on BL-522.
- The requirement matrix [`Kafka.md`](../../Conformance/Extended/Kafka.md) traces each new
  requirement to these tasks; each task replaces its ID with the named test. Transactions and
  READ_COMMITTED filtering stay not required (ADR-0105 decision 4).
