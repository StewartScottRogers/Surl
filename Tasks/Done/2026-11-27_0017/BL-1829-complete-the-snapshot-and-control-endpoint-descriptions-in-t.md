---
id: BL-1829
title: Complete the snapshot and control endpoint descriptions in the glossary and the control library's docs
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Wiki/Glossary.md, Application/Surl.Control.UnitLibrary]
requirement: none
created: 2026-11-26
completed: 2026-11-27
---
# BL-1829 — Complete the snapshot and control endpoint descriptions in the glossary and the control library's docs

## Goal

The glossary's "snapshot" and "control endpoint" rows, `Application/Surl.Control.UnitLibrary/CLAUDE.md` and the
class summaries of `ControlEndpoint` and `ControlReadRequests` say everything those things hold and serve today.

## Context

Found while running BL-1822 (checked against the code on 2026-11-26). Prose and XML doc comments only; no
executable line changes. No store, event or fault changes.

- `Documentation/Wiki/Glossary.md`, row "snapshot", lists mail, content, message bus and accounts. An instance
  registers up to 13 stores (`StoreKind`; built in the `ComposedStores` constructor,
  `ComposedStores.JoinTlsSrpAccounts` and `SurlServerComposition.Start.cs`), in this order, each beyond the first
  four only when composed or served: content, mail, bus, accounts, proxy accounts, TLS-SRP accounts, Redis
  keyspace, LDAP directory, AMQP 0-9-1 exchanges, Kafka producer state, sessions (MQTT 5 sessions, OCI tokens,
  MCP OAuth state and HTTP sessions), DNS zones, uploads (S3, Azure, Google Cloud Storage, OCI).
- Not all of them are copied: the uploads entry and the OCI token and MCP session entries hold 0 bytes, and a
  restore aborts the open uploads, revokes the tokens or clears the MCP state (`*StagedUploadStoreRegistration.cs`,
  `*UploadStoreRegistration.cs`, `OciTokenStoreRegistration.cs`, `McpSessionStoreRegistration.cs`).
- "Never taken of a `--directory` store" stays true of content, mail, bus, AMQP 0-9-1 exchanges kept in files and
  uploads over a disk content store. The LDAP directory and DNS zones loaded from `--directory` are in-memory
  copies of files read once, and are snapshotted (`LdapDirectoryStoreRegistration.cs`; the Composition
  library's `CLAUDE.md`, DNS zones bullet).
- Same file, row "control endpoint", says it reads events and exchanges, adds fault rules and seeds, resets,
  snapshots and restores stores. `ControlEndpoint.Routes` also lists and removes fault rules, deletes files, reads
  stores back, adds and removes accounts, proxy accounts and TLS-SRP accounts, changes their passwords, and
  changes and removes the Bearer-token account.
- `Application/Surl.Control.UnitLibrary/CLAUDE.md` describes no `PUT /v1/accounts/{name}`,
  `PUT /v1/proxy-accounts/{name}` or `PUT /v1/tls-srp-accounts/{name}` (ADR-0132 amendment 1). Each takes JSON
  `{"password"}` and calls `SetAccountPasswordAsync`, `SetProxyAccountPasswordAsync` or
  `SetTlsSrpAccountPasswordAsync`; `204`, `404` for an unknown account, `400` for an empty password, and `409`
  for the proxy and TLS-SRP ones when that store is not configured (`ControlStoreRequests.cs`,
  `IControlTarget.cs`).
- In the same file's "Read-back routes" bullet, `(from_offset, limit)` stands after the zones text. It belongs to
  `/v1/topics/{name}/log` (`ControlReadRequests.LogKeys`).
- `ControlEndpoint.cs`, class summary, omits reading stores back and account changes. `ControlReadRequests.cs`,
  class summary, names only the mail store, content store and message bus; the class also reads Redis keys, the
  LDAP directory and DNS zones.
- BL-1822 also edits the glossary (the "MCP tool" row only).

## Acceptance criteria

- [x] The glossary's "snapshot" row names all 13 stores listed in Context, says the uploads, OCI token and MCP
      session entries copy nothing and what a restore does to them, and limits the `--directory` refusal as Context
      states.
- [x] The glossary's "control endpoint" row says the endpoint reads events and exchanges; lists, adds and removes
      fault rules; seeds stores and deletes files; reads stores back; adds and removes accounts and changes their
      passwords; and resets, snapshots and restores stores.
- [x] The Control library's `CLAUDE.md` names the three account `PUT` routes with their body,
      `IControlTarget` members and status codes as Context gives them.
- [x] In that file, `(from_offset, limit)` directly follows `/v1/topics/{name}/log` and appears nowhere after the
      zones routes.
- [x] The class summary of `ControlEndpoint` names reading stores back and account changes, and that of
      `ControlReadRequests` also names the Redis keyspace, the LDAP directory and the DNS zones.
- [x] `dotnet build Application/Surl.Control.UnitTests -warnaserror` is clean, and the only changed lines in `.cs`
      files are comments.

## Notes

- Delivered by align-and-document; build (-warnaserror, whole solution), format check and fast tests green:
  30145 passed, 0 failed, 3 skipped. Every changed .cs line is a /// comment. No Integration tests: comments only.
- The password PUT routes are written with {user}, not the Contexts {name}, to match the codes doc comments and
  the other account routes.
- Also corrected ControlStoreRequests class summary (same library, same omission).
- The glossary no longer says account snapshots hold password hashes only: AccountStore keeps the passwords UTF-8
  bytes in some derived credentials. The other documents that still say so are BL-1840 and BL-1841.
- Follow-ups filed: BL-1840, BL-1841, BL-1842, BL-1843. Composition store docs are already BL-1830, the
  StoreKind.Uploads comment BL-1831.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The glossary's snapshot and control endpoint rows and the Control library's docs name everything those things hold and serve
