---
id: BL-1194
title: Decide how a test reads back the mail store, the content store and the message bus and finds a store change by name
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-061
created: 2026-11-15
completed: 2026-11-15
---
# BL-1194 — Decide how a test reads back the mail store, the content store and the message bus and finds a store change by name

## Goal

An ADR, decided by Claude under Stewart's delegation, fixes how a stand-in test reads back what a client left in the mail store, the content store and the message bus, and how it finds a store change by the name it touched, through `Surl.Api`, `Surl.Mcp` and the control endpoint.

## Context

- The gap (stand-in audit of 2026-11-15): a test can seed, reset, snapshot and restore every store (ADR-0124 decision 3, ADR-0158, ADR-0159), but it cannot read one. To check that the code under test sent mail to `bob`, uploaded `/reports/a.csv` or published to queue `orders`, it must open its own IMAP, HTTP or AMQP client, or pick bytes out of `server.Exchanges`. `ComposedStores` (`Application/Surl.Composition.UnitLibrary/ComposedStores.cs`) has seed, delete, reset, snapshot and restore members and no read member. `SurlServerMail`, `SurlServerFiles` and `SurlServerMessages` in `Application/Surl.Api` only write. `Surl.Mcp.UnitLibrary` has no read tool (`put_file`, `seed_mail` and `publish_message` only). The control endpoint (`Application/Surl.Control.UnitLibrary/IControlTarget.cs`) has `PUT`/`DELETE /v1/files/{path}` and no `GET`.
- Event queries cannot name what changed. `StoreChangedEvent.Name` holds the path, mailbox, topic, queue or account (ADR-0124 decision 1), but `SurlEventQuery` (`Application/Surl.Api/SurlEventQuery.cs`) filters by `Store` and not by name. `Surl.Mcp`'s `get_events` and `wait_for_event` (`EventToolFilter.cs`) and the control endpoint's `GET /v1/events` (`ControlEventQuery.cs`) filter by neither store nor name. So a test cannot wait for "`/uploads/report.csv` was written" and must filter every store event itself.
- What the stores already offer: `MailboxStore` has `ListMailboxes`, `ReadMailbox` and `FetchMessage` (all through a `MailView`). `ContentStore` has `GetFileStatus` and `ListDirectory`, plus a read path the HTTP server uses. `MessageBus` has `ReadLog` and `GetLogOffsets` for topic logs and `InspectQueue`, which gives counts only. It has no non-consuming browse of a queue's ready messages and no direct lookup of a topic's retained message.
- Questions this ADR answers:
  1. Which reads exist for each store, and what each returns: mail as mailboxes, then messages with UID, size, flags and bytes; content as a file's bytes and status and a folder's listing; the bus as a queue's ready messages, a topic's retained message and a topic log's records. A bus read never consumes or acknowledges.
  2. The bounds. For example, 16 MiB for one item as seeding has, and a cap on items per call.
  3. Reads publish nothing, never consult the fault plan, and are allowed on a `--directory` store (reading empties nothing).
  4. Whether reads see hidden or confined names: the content store's `.surl` state folder, and paths outside the served root.
  5. The member names in `Surl.Api` (e.g. `server.Mail.ListMessagesAsync`, `ReadMessageAsync`; `server.Files.ReadAsync`, `ListAsync`; `server.Messages.BrowseQueueAsync`, `ReadRetainedAsync`, `ReadLogAsync`), the `Surl.Mcp` tool names and JSON shapes (bytes by ADR-0103 decision 3's text-or-base64 rule), and the control endpoint's routes, e.g. `GET /v1/mail/{mailbox}`, `GET /v1/files/{path}`, `GET /v1/queues/{name}/messages`.
  6. The name filter: its spelling (`Name` / `name`), and whether it matches exactly or as a segment prefix, as a fault rule's `path` does (ADR-0124 decision 4). Also the `store` spelling for `Surl.Mcp` and the control endpoint, snake_case as `RecordedEventWriter` already writes `store`.
  7. Whether the Redis keyspace, the LDAP directory and the DNS zones are also readable. If so, this task files their tasks.
- Standing rules: base class library only, secure by default (a read never returns a password or account secret), configuration as data (one shape across the three hosts). The ADR amends ADR-0124 decisions 1, 3, 7 and 8 and says so in its header.
- Tasks already filed that wait on this one, and how they are split: BL-1195 (bus browse in `Surl.MessageBus`), BL-1196, BL-1197 and BL-1198 (mail, content and bus reads in `ComposedStores`), BL-1199 and BL-1200 (`Surl.Api`), BL-1201 and BL-1202 (`Surl.Mcp`), BL-1203 and BL-1204 (control endpoint), BL-1205 (docs), BL-1206 (recipe). The ADR's "Acted on by" names them.

## Acceptance criteria

- [x] A new ADR under `Documentation/Planning/Decisions/`, with the next free number, marked "Decided by Claude under Stewart's delegation", answers questions 1 to 7 above, each in its own numbered decision.
- [x] The ADR names the exact `Surl.Api` members, `Surl.Mcp` tool names with their input properties, and control endpoint routes with their answers and error codes, so BL-1199, BL-1201 and BL-1203 can be built without another question.
- [x] ADR-0124's header gains an "Amended by" line naming the new ADR.
- [x] If any of BL-1195 to BL-1206 needs different scope after the decision, its `Context` is updated to match, and its `Log` gets a line saying so.
- [x] If the ADR makes the Redis keyspace, the LDAP directory or the DNS zones readable, one task per layer is filed for them with `task-board.ps1 new`, depending on this task, and listed in Notes.

## Notes

- ADR-0164 written and ADR-0124 amended. BL-1195 to BL-1206 keep their scope: each already matches decisions 1 to 8, so no Context changed.
- Decision 9 makes the Redis keyspace, LDAP directory and DNS zones readable; filed BL-1218 (Redis), BL-1219 (LDAP) and BL-1220 (DNS), each depending on this task.
- Defaults taken: 16 MiB per item, limit 100 (1 to 1000), 64 MiB per reply; name filter is exact-or-segment-prefix, case-sensitive.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. ADR-0164 written; ADR-0124 amended; BL-1218 to BL-1220 filed
