---
id: BL-1494
title: Add SetPassword to the TLS-SRP and proxy account stores per ADR-0132 amendment 1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1482]
touches: [Cryptography/Surl.TlsSrp.UnitLibrary, Cryptography/Surl.TlsSrp.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1494 — Add SetPassword to the TLS-SRP and proxy account stores per ADR-0132 amendment 1

## Goal

The TLS-SRP account store gains `SetPassword`, and the proxy account store (find it from `Application/Surl.Api/SurlServerProxyAccounts.cs`; split into a further task if it is a third project) the same, as ADR-0132 amendment 1 says.

## Context

- ADR-0132 amendment 1 (`Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md`) states the rule, the answers and the events; follow it exactly.

## Acceptance criteria

- [x] `SetPassword` on the TLS-SRP store re-derives the verifier, publishes `AccountRemoved` then `AccountAdded`, and refuses an unknown user or empty password; unit tests prove each at the coverage gates.
- [x] If the proxy store sits in another project, a follow-up task is filed for it and its ID is in Notes.

## Notes

- Proxy store is `Surl.Api`'s `SurlServerProxyAccounts`, another project: filed as BL-1495.
- `TlsSrpAccounts` has no event publisher (it is a Cryptography library with no event seam), so, like `Add` and `Remove`, `SetPassword` swaps the entry whole and the host publishes `AccountRemoved` then `AccountAdded`; that wiring belongs to the host tasks (Surl.Composition / Surl.Api). Decision recorded here, no ADR needed as it follows ADR-0132 amendment 1 rule 3.
- Group is kept from the existing entry; a fresh salt is drawn as `Add` draws it.

## Log
- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. TlsSrpAccounts.SetPassword re-derives verifier, refuses unknown user and empty password; proxy store filed as BL-1495
