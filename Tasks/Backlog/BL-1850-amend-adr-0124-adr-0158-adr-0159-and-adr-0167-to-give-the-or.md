---
id: BL-1850
title: Amend ADR-0124, ADR-0158, ADR-0159 and ADR-0167 to give the order across stores that the code runs
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md, Documentation/Planning/Decisions/ADR-0158-how-the-redis-keyspace-the-ldap-directory-and-the-dns-zones-are-seeded-reset-snapshotted-restored-and-observed.md, Documentation/Planning/Decisions/ADR-0159-which-broker-held-state-a-reset-snapshot-and-restore-of-the-message-bus-cover.md, Documentation/Planning/Decisions/ADR-0167-what-a-reset-snapshot-and-restore-do-with-in-progress-object-uploads-and-the-oci-and-mcp-servers-tokens.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1850 — Amend ADR-0124, ADR-0158, ADR-0159 and ADR-0167 to give the order across stores that the code runs

## Goal

ADR-0124, ADR-0158, ADR-0159 and ADR-0167 give, by dated amendment, the order the code runs a whole-instance
reset, snapshot and restore in, and none leaves a different order standing as current.

## Context

Found while running BL-1830 (checked against the code on 2026-11-27). ADR text only; no code, behaviour, store or
event changes. The design question is delegated to Claude (root `CLAUDE.md`, "Decisions"): the order the code runs
and the tests pin stands, and the records are amended to it. No upstream curl behaviour is involved.

The code's order, each store beyond the first four only when composed or served: content, mail, bus, accounts,
proxy accounts, TLS-SRP accounts, Redis keyspace, LDAP directory, AMQP 0-9-1 exchanges, Kafka producers,
sessions, DNS zones, uploads. Where it comes from, in `Application/Surl.Composition.UnitLibrary`:

- the `ComposedStores` constructor, and `JoinTlsSrpAccounts` in `ComposedStores.AccountStores.cs`;
- `ComposeStores`, `ControlledBrokerStores` and `ControlledUploads` in `SurlServerComposition.Start.cs`. The one
  `Sessions` entry runs MQTT 5, OCI tokens, then MCP; the one `Uploads` entry runs S3, Azure Storage, Google
  Cloud Storage, then OCI.
- Pinned by `Amqp0ExchangeStoreTests`, `DnsZoneStoreTests`, `AccountStoresTests` and `S3UploadStoreTests` in
  `Application/Surl.Composition.UnitTests`.

What the records say, under `Documentation/Planning/Decisions`:

- ADR-0124 decision 3, "Order across stores": content, mail, bus, "then accounts", "accounts last". Its header
  names ADR-0158, ADR-0159, ADR-0167 and ADR-0170 as amending it, but none of its amendments gives the order as
  it is now.
- ADR-0158 decision 5: "content, mail, bus, accounts, keyspace, directory, zones", with decision 8 saying Redis,
  LDAP and DNS follow in that order. Other stores now sit before, between and after those three.
- ADR-0159 decision 3: the broker registrations come after the bus and before the accounts. They follow the
  accounts. Decision 4 says "the order puts the bus first"; content is first, and it refuses first on disk.
- ADR-0167 decision 3: "content, mail, bus, then the server registrations in ordinal order of scheme (uploads
  before tokens within a scheme), then accounts". Accounts come before every server registration, tokens are in
  the sessions entry, which runs before the one uploads entry (last), and neither entry runs in ordinal order.

BL-1840 and BL-1846 also amend ADR-0124, for what an account snapshot holds; leave that to them.
`ADR-0158`, `ADR-0159` and `ADR-0167` also name an `IControllableStore` seam that no `.cs` file declares; that is
not this task's.

## Acceptance criteria

- [ ] ADR-0124 has a dated amendment, "Decided by Claude under Stewart's delegation", named in its header, that
      lists the thirteen stores in the order above, says which exist only when composed or served, and says it
      supersedes "accounts last" in decision 3.
- [ ] ADR-0158 carries a dated amendment, named in its header, saying its decision 5 and decision 8 order is
      superseded and pointing to the ADR-0124 amendment.
- [ ] ADR-0159 carries a dated amendment, named in its header, saying the broker registrations follow the accounts,
      in the order AMQP 0-9-1 exchanges, Kafka producers, sessions, and that content comes before the bus.
- [ ] ADR-0167 carries a dated amendment, named in its header, giving the order within the sessions and uploads
      entries, saying both follow the accounts and that the uploads entry is last.
- [ ] No original decision sentence is rewritten, and only the four ADR files change.

## Notes

## Log

- 2026-11-27: Created.
