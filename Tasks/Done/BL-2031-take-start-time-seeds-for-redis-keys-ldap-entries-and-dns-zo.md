---
id: BL-2031
title: Take start-time seeds for Redis keys, LDAP entries and DNS zones in SurlServerOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2030]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: FR-061
created: 2026-11-27
completed: 2026-11-27
---
# BL-2031 — Take start-time seeds for Redis keys, LDAP entries and DNS zones in SurlServerOptions

## Goal

A test sets `SurlServerOptions.RedisKeys`, `DirectoryLdif` and `DnsZones`, and the started server holds them before any listener binds and returns to them on reset.

## Context

- ADR-0158 decisions 3, 4 and 9, and Amendment 1 (A2). BL-1166 added `Keys`, `Directory` and `Zones` on `SurlServer` and left the start-time options out.
- `SurlServerOptions` gains `RedisKeys` (`IReadOnlyList<SurlRedisKey>`), `DirectoryLdif` (`string?`) and `DnsZones` (`IReadOnlyList<SurlDnsZone>`), defaults empty, empty and `null` as fits each type; assigning `null` to a list leaves it empty, as `AuthenticationMethods` does. New public types, one per file: `SurlRedisKey` (key bytes, value bytes, expiry `TimeSpan?`, database, default 0, with a second constructor taking key and value strings written as UTF-8) and `SurlDnsZone` (apex, zone-file text). None of this adds a `surl` word.
- Misuse is refused by `SurlServerArguments.ThrowIfMisused` with `ArgumentException` (or a subclass) before anything is composed: a null list item, an empty key, a database outside 0 to 15, an expiry outside 1 millisecond to 365 days, a null apex or zone text.
- In `SurlServer.StartAsync`, after `ComposeAsync` and after the `ExchangeRecordSubscriber` exists, and before `ServeUntilBoundAsync`, apply the seeds in the order keys, directory, zones, each through the `ComposedStores` overload of BL-2030 with `keepOnReset: true`.
- A refused seed (a bound, a duplicate DN or apex, unreadable LDIF or zone text, or no listen URL serving that store) refuses the start with `SurlSeedRefusedException` naming the `StoreKind`, worded as `SurlServerKeys`, `SurlServerDirectory` and `SurlServerZones` word it, after disposing the recording and the composed serving, so nothing is bound or locked. `StartAsync`'s XML comment lists the exception. With `SURL_EXTENSIONS=0` a Redis or DNS start-time seed is refused the same way, since there is no such store.
- `SurlServer.ResetAsync`'s XML comment says the Redis databases and the LDAP directory are empty and there is no DNS zone afterwards; correct it, `SurlServerZones.AddZoneAsync`'s comment if affected, and `Application/Surl.Api/CLAUDE.md`. The `README.md` is BL-2034.
- Stand-in: seeds the three shared stores before a test connects, publishes one `Seeded` event each on the instance's stream, never consults the fault plan, and holds no static state.

## Acceptance criteria

- [x] The three options and the two types exist with XML comments; named tests in `Surl.Api.UnitTests` show a start with no start-time seed behaves as before.
- [x] Named tests (in memory, port 0, fake listeners): each of the three seeds is readable through `Keys`, `Directory` or `Zones` once `StartAsync` returns, and with `RecordExchanges` on `FindEvents` shows its `Seeded` event.
- [x] Named tests: after a runtime seed, `ResetAsync` leaves exactly the start-time seeds in each of the three stores.
- [x] Named tests: a refused start-time seed of each store, and a seed of a store no listen URL serves, throws `SurlSeedRefusedException` with that `Store`, and no listener was bound (the data directory lock released where one was taken).
- [x] Named tests: each misuse listed in Context throws before any listener factory is created.
- [x] `dotnet build Application/Surl.Api.UnitTests -warnaserror` is clean, the fast tests pass without `TestCategory=Integration`, and `Surl.Api` keeps 100% line and branch coverage.

## Notes

- Plan, tests first (they failed to compile for want of `SurlRedisKey` and `SurlDnsZone`), implementation and
  documentation were done in the lane's own session, since the task and ADR-0158 settle the design; `code-reviewer`
  reviewed the diff and found nothing that had to be fixed. No conformance audit: no `surl` option, exit code or wire
  byte changes. No new ADR: ADR-0158 decides everything here.
- Choices taken: `StartTimeSeeds` applies the seeds through new internal `SeedAsync` methods on `SurlServerKeys`,
  `SurlServerDirectory` and `SurlServerZones`, so a refusal is worded in one place; a cancelled seeding disposes what a
  refused one does; the string constructor of `SurlRedisKey` throws `ArgumentNullException` naming `key` or `value`;
  the cleanup in `SurlServer.ApplyStartTimeSeedsAsync` awaits with `SuppressThrowing` rather than `try`/`catch`,
  because an `await` in a `catch` leaves a branch no test can cover.
- Evidence: `SurlServerTests.StartTimeSeeds.cs` and `SurlServerOptionsTests`; `Surl.Api.UnitTests` 449 passed; the
  solution's fast tests 30592 passed, 3 skipped; `Measure-CodeQuality.ps1 -Library Surl.Api` 100% line, 100% branch,
  0 failing members.
- `Application/Surl.Api/README.md` is BL-2034's.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. SurlServerOptions.RedisKeys, DirectoryLdif and DnsZones seed the three stores before any listener binds, ResetAsync returns to them, and a refused seed refuses the start
