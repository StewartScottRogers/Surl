---
id: BL-2097
title: Correct the remaining Surl.Api and Surl.Composition statements about the seeded stores' resets, absence and refusals
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2074]
touches: [Application/Surl.Api, Application/Surl.Composition.UnitLibrary/CLAUDE.md]
requirement: FR-061
created: 2026-11-28
completed:
---
# BL-2097 — Correct the remaining Surl.Api and Surl.Composition statements about the seeded stores' resets, absence and refusals

## Goal

The `Surl.Composition` notes and the `Surl.Api` XML docs are true about when the Redis keyspace, the LDAP
directory and the DNS zones are held, which resets are refused, and what an empty seeded key throws.

## Context

Noticed while working BL-2074 (2026-11-28) and read from source the same day. BL-2074 edits `///` lines in some
of the same files; start from its landed wording.

- `Application/Surl.Composition.UnitLibrary/CLAUDE.md`, the three "Reading ... back" entries (BL-1218, BL-1219,
  BL-1220; about lines 353 to 367) say "No Redis server", "no LDAP server" and "without a DNS server" or "no DNS
  server". The store is absent when no listen URL serves it (`redis` or `rediss`, `ldap` or `ldaps`, `dns`;
  `ComposeStores` in `SurlServerComposition.Start.cs`; ADR-0158 Amendment 1). The entry listing the whole-instance
  order (about line 169) says those stores follow "each only when composed or served".
- `Application/Surl.Api/SurlServerOptions.cs`, the `DirectoryLdif` and `DnsZones` summaries say the seeds are held
  beside what is read from `DataDirectory` and returned to on a reset. Both are held, but a server with a data
  directory or a share on disk refuses the reset (the content store, first in the order, refuses). The
  `RedisKeys` summary says "returns to on a reset" with the same limit. `Application/Surl.Api/README.md`,
  "Start-time seeds", already says a `--directory` server refuses `ResetAsync`.
- `Application/Surl.Api/SurlServerKeys.cs`: the `SetAsync(string, string, ...)` overload says the key has at least
  one character and has no `ArgumentException` doc. It passes to the byte overload, where an empty key is an
  `ArgumentException` (`ComposedStores.SeedKeyAsync`; `SeedAsync` translates only `InvalidOperationException`).

Documentation only: no store, event, fault or behaviour changes, so there is nothing new for a test to seed,
reset, observe or inject.

## Acceptance criteria

- [ ] In `Application/Surl.Composition.UnitLibrary/CLAUDE.md`, the three "Reading ... back" entries say the
      `NotFound`, empty page or empty apex list results when no listen URL serves the store, naming the schemes,
      and the whole-instance order entry says the Redis keyspace, the LDAP directory and the DNS zones are held
      only when a listen URL serves them.
- [ ] The `RedisKeys`, `DirectoryLdif` and `DnsZones` summaries in `SurlServerOptions.cs` say that a server with a
      data directory or a share on disk refuses the reset, and none says such a server returns to the seeds.
- [ ] The `SetAsync(string, string, ...)` overload in `SurlServerKeys.cs` has an `ArgumentException` element saying
      the key is empty.
- [ ] The diff changes only `///` lines in `.cs` files besides the `CLAUDE.md`, and
      `dotnet build Application/Surl.Api.UnitTests -warnaserror` is clean.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
