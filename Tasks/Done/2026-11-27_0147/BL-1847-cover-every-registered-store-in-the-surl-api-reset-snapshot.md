---
id: BL-1847
title: Cover every registered store in the Surl.Api reset, snapshot and restore docs and the StoreKind.Sessions comment
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1831]
touches: [Application/Surl.Api/SurlServer.cs, Application/Surl.Api/README.md, Foundation/Surl.Protocol.Abstractions.UnitLibrary/StoreKind.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1847 — Cover every registered store in the Surl.Api reset, snapshot and restore docs and the StoreKind.Sessions comment

## Goal

The doc comments of `SurlServer.ResetAsync`, `SnapshotAsync` and `RestoreAsync`, the "Seed, reset, snapshot"
section of `Application/Surl.Api/README.md` and the `StoreKind.Sessions` comment cover every store the code
resets, snapshots and restores.

## Context

Found while running BL-1831 (checked against the code on 2026-11-27). Prose and XML doc comments only; no
executable line changes. No store, event or fault changes. Paths below under `Composition` are in
`Application/Surl.Composition.UnitLibrary`.

- The registered order, each store beyond the first four only when composed or served: content, mail, bus,
  accounts, proxy accounts, TLS-SRP accounts, Redis keyspace, LDAP directory, AMQP 0-9-1 exchanges, Kafka
  producers, sessions, DNS zones, uploads (the `ComposedStores` constructor, `ComposedStores.JoinTlsSrpAccounts`
  and `SurlServerComposition.Start.cs`). All three `SurlServer` methods walk every one.
- The three doc comments name content, mail, bus and accounts only.
- Uploads (S3, Azure Storage, Google Cloud Storage, OCI) are not copied: reset and restore discard them, or
  refuse when the content store is on disk. Azure also drops its remembered committed block lists.
- Under `StoreKind.Sessions`: MQTT 5 sessions are copied; OCI registry tokens (`OciTokenStoreRegistration`) and
  MCP OAuth state and HTTP sessions (`McpSessionStoreRegistration`) are not, so a restore revokes or ends them.
- A restore also drops every Kafka consumer group, and the MQTT 5 brokers' pending Wills, queued deliveries and
  unacknowledged publishes.
- Events: it is not one `StoreChangedEvent` per store for `Uploads` or `Sessions`. Each composed upload kind
  publishes its own, named for its server; under `Sessions` the MQTT 5 entry publishes one with an empty name,
  and the OCI and MCP entries one each, named `oci` and `mcp`.
- `StoreKind.cs`, `Sessions` comment: names only the MQTT 5 brokers' sessions.
- `README.md`, the paragraph beginning "`ResetAsync` returns files, mail, messages and accounts": omits proxy
  accounts, TLS-SRP accounts, Kafka producer state and consumer groups, OCI tokens, MCP state and uploads. It
  also says a snapshot holds "accounts as password hashes only", which is false: a named account (proxy accounts
  included) is kept with the password's UTF-8 bytes (`AccountStore.DeriveNamed` in
  `Foundation/Surl.Authentication.UnitLibrary`); TLS-SRP accounts keep verifiers.
- BL-1831 puts the same facts in the class summary of `Application/Surl.Api/SurlSnapshot.cs`. BL-1830 corrects
  the Composition library's docs and BL-1846 the authentication library's, the glossary's and the ADRs'.

## Acceptance criteria

- [x] The `ResetAsync` summary covers every store in the order Context gives, saying the later ones exist only
      when composed or served, and that uploads, OCI tokens and MCP state are discarded, revoked or ended.
- [x] The `RestoreAsync` summary says only what the snapshot copied is put back, and what happens to uploads, OCI
      tokens and MCP state.
- [x] The `SnapshotAsync` summary says which of those stores it copies nothing from.
- [x] No summary in `SurlServer.cs` says reset or restore publishes exactly one event per store; they say each
      server publishes its own for `Uploads` and `Sessions`.
- [x] The README paragraph names every store Context lists and no longer says "password hashes only"; it says a
      snapshot holds secrets, including a named account's password bytes.
- [x] The `StoreKind.Sessions` comment names MQTT 5 sessions, OCI registry tokens and MCP OAuth state and HTTP
      sessions; `dotnet build Application/Surl.Api.UnitTests -warnaserror` is clean and the only changed lines in
      `.cs` files are comments.

## Notes

- Changed: `SurlServer.cs` XML doc comments and the README's "Seed, reset, snapshot" section (the paragraph
  became the store order, a reset / snapshot / restore table and a shorter paragraph).
- `StoreKind.cs` needed no change: BL-1842 already made the `Sessions` comment name all three kinds of state.
  The README's "password hashes only" was already gone and its "A snapshot holds secrets" paragraph already
  matches `AccountStore.DeriveNamed` (BL-1841), so that paragraph is unchanged.
- Where the code is narrower than Context, the docs follow the code: only MQTT 5 sessions kept with no connection
  are copied, without queued deliveries; through `SurlServer` a content store on disk is first and refuses, so
  reset leaves every store as it was.
- Follow-ups: the combiner's uploads-only name is BL-1848 (already filed); the `ComposedStores.cs`
  `cancellationToken` comments are BL-1872 (filed by this run).
- Verified 2026-11-27: `dotnet build -warnaserror` of the solution 0 warnings, `dotnet format
  --verify-no-changes` clean, fast tests all passed (3 skipped, in Surl.Content and Surl.Networking).
  No Integration run: comments and prose only.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The Surl.Api reset, snapshot and restore comments and README cover every registered store and what is copied, discarded or revoked
