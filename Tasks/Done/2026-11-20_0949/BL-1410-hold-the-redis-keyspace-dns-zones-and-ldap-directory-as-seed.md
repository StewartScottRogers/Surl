---
id: BL-1410
title: Hold the Redis keyspace, DNS zones and LDAP directory as seedable stores only when a listen URL serves them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1404]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Api.UnitTests, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-1410 — Hold the Redis keyspace, DNS zones and LDAP directory as seedable stores only when a listen URL serves them

## Goal

On an instance with no `redis`/`rediss`, no `dns`, or no `ldap`/`ldaps` listen URL, `ComposedStores` holds no
keyspace, zones or directory store respectively, so seeding one is refused as BL-1404's amendment to ADR-0158
states, through every host alike.

## Context

- Surly cookbook bug report 2026-11-19 09:30, finding 7: `surl-mcp`'s `set_key` and `add_dns_zone` succeed on an
  HTTP-only server. BL-1404 decides the rule; the recommendation it weighs is this task.
- Cause: `Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs:469-472` gives `ComposedStores`
  the keyspace when `servers.OfType<RedisServer>().Any()` and the zones when `IsDnsComposed(servers)` (lines
  480-499), and always registers `LdapDirectoryStoreRegistration.For(loaded.State.LdapServer)`. Every extended
  server, and the LDAP server, is composed whichever listen URLs are given, so those checks are always true while
  extensions are on. `ComposedStores.SeedKeyAsync` and `SeedZoneAsync` (`ComposedStores.cs:519-543`) already throw
  `InvalidOperationException` ("This instance holds no Redis keyspace." / "...no DNS zones.") when given none, and
  `Surl.Api`'s `SurlServerKeys.SetAsync` and `SurlServerZones.AddZoneAsync` turn that into
  `SurlSeedRefusedException`; only the condition is wrong.
- The served listen URLs are in scope there: `served.ListenUrls` (`SurlServerComposition.Start.cs:458-461`), which
  includes `Surl.Api`'s additional servers. Decide by scheme (`redis`/`rediss`, `dns`, `ldap`/`ldaps`), as BL-1404
  states.
- Tests live beside `RedisKeyspaceStoreTests`, `DnsZoneStoreTests`, `ComposedStoresKeyReadTests` and the start tests
  in `SurlServerCompositionTests.Start.cs` (over `FakeListenerFactory` and `StartTestSeams`).
- Stand-in: a test that seeds a store its server does not serve fails at the seed, with the store named; a
  whole-instance reset and snapshot walk only the stores served; nothing new is published.

## Acceptance criteria

- [x] A failing test first: a start with only `http://127.0.0.1:0/` and extensions on gets `ComposedStores` whose
      `SeedKeyAsync` throws `InvalidOperationException`, and the same start with `redis://127.0.0.1:0/` added seeds.
- [x] The same pair of tests for `SeedZoneAsync` (with and without `dns://127.0.0.1:0/`).
- [x] The directory is held as BL-1404 states for an instance with and without an `ldap` listen URL, pinned by a
      test.
- [x] A test pins `ComposedStores.StoreOrder` (or the whole-instance reset's stores) for an HTTP-only instance and
      for one serving `redis` and `dns`.
- [x] Existing tests that seed a key, zone or directory on an instance with no such listen URL get the listen URL
      added; `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary`
      stays at 100% line and branch coverage.

## Notes

- Checked when filing: the `Surl.Api` and `surl-mcp` seed tests already start the store's listen URL
  (`Application/Surl.Api.UnitTests/SurlServerTests.Lookups.cs:10-12`, `Application/Surl.Mcp.UnitTests/LookupToolsTests.cs:42-153`),
  so neither project should need a change; the Composition seed and read tests (`RedisKeyspaceStoreTests`,
  `DnsZoneStoreTests`, `LdapDirectoryStoreTests`, `ComposedStores*ReadTests`, `ComposedControlTarget*Tests`) are the
  ones to check.
- Done 2026-11-19: SurlServerComposition.ComposeStores (extracted to keep ComposeUnderTheLockAsync under complexity 10) holds the keyspace, zones and directory, and registers their stores, only when served.ListenUrls has edis/ediss, dns, ldap/ldaps. New ServedStoresTests pins seed refusal, seed success and RegisteredStoreOrder. Nine existing store-order assertions lost the unserved stores.
- 	ouches grew by Surl.Api.UnitTests and Surl.Mcp.UnitTests (no task in Doing names them): one test each asserted that an instance with no LDAP listener reads an empty directory; ADR-0158 Amendment 1 says it answers NotFound, so both now pin that. No decision beyond the ADR, so no new ADR.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
- 2026-11-19: Doing -> Done. Stores held only when a listen URL serves them
