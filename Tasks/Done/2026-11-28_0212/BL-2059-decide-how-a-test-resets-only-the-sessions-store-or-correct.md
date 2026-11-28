---
id: BL-2059
title: Decide how a test resets only the sessions store, or correct ADR-0167 Amendment 3 point 3
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0167-what-a-reset-snapshot-and-restore-do-with-in-progress-object-uploads-and-the-oci-and-mcp-servers-tokens.md]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-2059 — Decide how a test resets only the sessions store, or correct ADR-0167 Amendment 3 point 3

## Goal

ADR-0167 says truly how a test takes a directory user's tokens away, and records whether a public surface resets only
the sessions store.

## Context

Found while working BL-2021, read from source on 2026-11-27. Amendment 3, point 3, says a test resets the sessions
store (`StoreKind.Sessions`) and that this works with `--directory`. `ComposedStores.ResetAsync(StoreKind, ...)` is
called only by `Surl.Composition.UnitTests`: `SurlServer.ResetAsync`, `POST /v1/reset` and the `surl-mcp` reset tool
all reset the whole instance, which a content store on disk (`--directory` or `--share`) refuses before any store is
reset. Decide by the stand-in rules (root `CLAUDE.md`, "Controllable"): either amend the sentence, or decide a public
per-store reset and file the tasks that build it.

## Acceptance criteria

- [x] A dated amendment to ADR-0167, marked "Decided by Claude under Stewart's delegation", states which public surfaces can end a directory user's tokens, with and without `--directory`.
- [x] If it decides a per-store reset, the tasks that build it, and update the `--user-ldap` help text, are in `Tasks/Backlog` and named in the amendment.

## Notes

- ADR-0167 Amendment 4 (2026-11-28) decides to correct the sentence only: no per-store reset is decided here, so the
  second box's condition does not arise and no task was filed.
- The Context holds, read from source, not run: no host calls `ComposedStores.ResetAsync(StoreKind, ...)`.
- A public per-user way exists with and without `--directory`: add, then remove, an account of the user's name
  (`LdapDirectoryUserTokenPolicyTests`), at the cost Amendment 4, point 3, lists.
- Whether the hosts act on one store at a time is BL-2076's question; Amendment 4, point 4, leaves it there and says
  it must weigh that with disk no public reset reaches any store. A pointer to that was added to BL-2076's Context.
- The `reset_server` and `restore_server` tool descriptions omit the token revocation; filed as BL-2079.
- The add-then-remove route was checked in source for `SurlServer.Accounts` and `ComposedControlTarget` (both call
  `ComposedStores.AddAccount`/`RemoveAccount`); the test that pins it covers the policy, not the hosts.

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. ADR-0167 Amendment 4 says truly which public surfaces end a directory user's tokens, with and without --directory; no sessions-only reset is decided (left to BL-2076)
