---
id: BL-1842
title: Name every registration that the StoreKind.Sessions doc comment covers
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary/StoreKind.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1842 — Name every registration that the StoreKind.Sessions doc comment covers

## Goal

The doc comment of `StoreKind.Sessions` names everything registered under that member.

## Context

Found while running BL-1829 (checked against the code on 2026-11-27). A doc comment in the shared contracts
library; no contract or executable line changes.

- `Foundation/Surl.Protocol.Abstractions.UnitLibrary/StoreKind.cs`, `Sessions`, names only "The MQTT 5 brokers'
  sessions kept with no connection, with their subscriptions (ADR-0159)."
- `Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs`, `ControlledBrokerStores`, combines
  three registrations into the one `StoreKind.Sessions` entry: `Mqtt5SessionStoreRegistration` (sessions and
  pending Wills), `OciTokenStoreRegistration` (the OCI registries' tokens) and `McpSessionStoreRegistration` (the
  MCP servers' OAuth clients, codes and tokens and their HTTP sessions).
- The OCI and MCP entries copy nothing into a snapshot: reset and restore revoke or clear them (ADR-0167
  decisions 2 and 3).
- `StoreKind.Uploads`'s comment in the same file is BL-1831's; leave it to that task.
- Stand-in: documents only. No store, event or fault changes.

## Acceptance criteria

- [x] The `StoreKind.Sessions` comment names the MQTT 5 sessions, the OCI registry tokens, and the MCP OAuth state
      and HTTP sessions, and cites ADR-0159 and ADR-0167.
- [x] It says the OCI and MCP state is not copied into a snapshot, and what reset and restore do to it.
- [x] `dotnet build Foundation/Surl.Protocol.Abstractions.UnitTests -warnaserror` is clean, and the only changed
      lines are comments.

## Notes

- The comment also names the MQTT 5 pending Wills, which `Mqtt5SessionStoreRegistration` registers under the same
  member: a snapshot copies none (`Mqtt5BrokerState.TakeSnapshot`), reset and restore discard them (ADR-0159
  decision 2).
- Checked against `McpSessionStoreRegistration`, `OciTokenStoreRegistration`, `Mqtt5SessionStoreRegistration` and
  ADR-0167 decisions 2 and 3.
- The one-comment edit was made in the session, not delegated to `align-and-document`.
- Verify: `dotnet build -warnaserror` 0 warnings; `dotnet format --verify-no-changes` clean; fast tests 29875
  passed, 3 skipped, 0 failed (82 assemblies). Integration tests not run: no executable line changed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The StoreKind.Sessions doc comment names every registration it covers: MQTT 5 sessions and pending Wills, OCI tokens, MCP OAuth state and HTTP sessions.
