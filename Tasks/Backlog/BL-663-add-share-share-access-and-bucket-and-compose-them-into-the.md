---
id: BL-663
title: Add --share, --share-access and --bucket and compose them into the content store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-645, BL-646, BL-657, BL-658, BL-659, BL-660, BL-661, BL-662]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-663 — Add --share, --share-access and --bucket and compose them into the content store

## Goal

`surl --share <name>=<path>[;readonly;protocols=…]`, `--share-access <name>=<user|@group>:<rw|r>,...` and
`--bucket <name>=<path>` configure the one content store every file server and object-storage server shares,
with each widening option warned at start.

## Context

- Decisions: BL-641's ADR (names, grammar, refusals, bounds, help category, which option widens access) and
  BL-642's ADR decisions 2, 4 and 5.
- Builds on: BL-655 (shares and buckets in the store), BL-656 (access checks), BL-657 to BL-662 (every original
  file server passes its caller), BL-646 (group expansion), BL-645 (item reader).
- Where: option rows and parsing in `Application/Surl.Cli.UnitLibrary`; composition in
  `Application/Surl.Composition.UnitLibrary` (`ComposeContentStore` after BL-609/BL-614): each share's path checked at
  start (missing or not a directory refused with the ADR's exit code and text, as `--directory` is by
  `DataDirectoryProbe`, ADR-0031 decision 3), the share table and access table passed to `ContentStore`.
- The extended object-storage servers (S3, Azure, Cloud Storage, OCI) receive the same store when their "Serve …
  from the surl executable" tasks register them; nothing here waits on them.
- `--aihelp` completeness (root `CLAUDE.md`); the example's precondition creates the share folder.

## Acceptance criteria

- [ ] Named tests in `Application/Surl.Cli.UnitTests` parse each option and refuse each malformed, duplicate or
      conflicting item with the ADR's exit code and text.
- [ ] Named tests in `Application/Surl.Composition.UnitTests` show shares, buckets and access (with an `@group`)
      reaching the composed `ContentStore`, one instance handed to every file server, a missing share path refused
      at start, and each widening option's warning written.
- [ ] Each option passes `AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`,
      `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName` and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and both
      libraries keep 100% line and branch coverage.

## Notes

- From BL-662 (2026-11-05): `ContentShare.IsVisibleTo` matches `Schemes` exactly (ignoring case),
  so ADR-0102 decision 2.1's "a scheme and its TLS twin are one entry" must be applied here, when
  `protocols=` is composed into `ContentShare.Schemes`: `protocols=gopher` must also admit
  `gophers`, `ftp` admit `ftps`, and so on (`Account.AreTlsTwinsOrEqual` already does this for
  accounts).

## Log

- 2026-11-04: Created.
