---
id: BL-987
title: Read --share, --share-access and --bucket items into the Surl.Cli configuration model
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-645, BL-646, BL-1035]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-11
---
# BL-987 — Read --share, --share-access and --bucket items into the Surl.Cli configuration model

## Goal

`Surl.Cli` can read a `--share <name>=<path>[;readonly;protocols=…]` item, a
`--share-access <name>=<user|@group>:<rw|r>,...` item and a `--bucket <name>=<path>` item into the configuration
model (`SurlCommandLine` members, each empty by default), each read and refused as ADR-0101 says, fully unit
tested. No option row uses the readers yet; BL-989 adds the rows.

## Context

- First of the three tasks BL-663 ("Add --share, --share-access and --bucket and compose them into the content
  store") was split into on 2026-11-06 for the 60-minute size limit, in the order BL-653's split proved (BL-880,
  BL-882, BL-883): this (model and readers), BL-988 (composition), BL-989 (option rows, help, manual and `--aihelp`),
  so every help sentence describes behaviour that already exists.
- Decisions: ADR-0101 (BL-641: names, item grammar read by BL-645's `ConfigurationItemReader`, refusals, bounds,
  a refusal never echoing a secret) and ADR-0102 (BL-642) decisions 2, 4 and 5. The ADRs win over this summary.
- ADR-0102 decision 2.1: a scheme and its TLS twin are one entry, so `protocols=gopher` also admits `gophers` and
  `ftp` admits `ftps` (`Account.AreTlsTwinsOrEqual` already does this for accounts; `ContentShare.IsVisibleTo`
  matches `Schemes` exactly, so the model or BL-988 must expand the twins - say which in the XML docs).
- Adding `SurlCommandLine` members makes `Surl.Mcp.UnitTests`'
  `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` fail until each is classified in
  `Application/Surl.Mcp.UnitLibrary` - a one-line entry per member.
- Stand-in: none in this task; it reads configuration only.

## Acceptance criteria

- [x] Named tests in `Application/Surl.Cli.UnitTests` read each item form (one, many, `readonly`, `protocols=`,
      `@group` access) and refuse each malformed or duplicate item with the ADR's text.
- [x] `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` passes.
- [x] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 2): restored lane 4's work from its darkfactory stash (`5dd519c3`) by patch, without
  popping the shared stash. `global.json`'s `rollForward: latestFeature` resolves the pin 10.0.401 to the
  installed SDK 10.0.404, so the gates ran on the pinned SDK line: `dotnet build -warnaserror` clean, every
  fast test green (`Surl.Cli.UnitTests` 1482/1482, `Surl.Mcp.UnitTests` 245/245, including
  `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce`), and `Surl.Cli.UnitLibrary` at 100% line and
  100% branch coverage.

- 2026-11-11 (lane 4): the code is written and was verified, but **left uncommitted** (the shift stashes it)
  because the pinned .NET SDK 10.0.401 is not installed (BL-1035), so the gate `dotnet build` cannot run
  with the pinned toolchain. Verified with a throwaway `global.json` pointing at SDK 11.0.100, restored
  straight after: build clean (warnings are errors), `Surl.Cli.UnitTests` 1300/1300 and
  `Surl.Mcp.UnitTests` 242/242 green, and every new or changed `Surl.Cli` class at 100% line and branch.
  The next run only needs to restore the stash, rerun the gates with the pinned SDK, tick the boxes and commit.
- What was built: records `ConfiguredShare`, `ConfiguredShareAccess`, `ConfiguredBucket`; readers
  `ConfiguredShareReader`, `ConfiguredShareAccessReader`, `ConfiguredBucketReader` (each a
  `ConfigurationItemShape` over BL-645's `ConfigurationItemReader`); `ConfiguredContentConflicts.Find` for the
  whole-line rows 14 and 15; `SurlCommandLine.Shares`, `ShareAccess`, `Buckets` (empty by default);
  `ConfiguredMailboxReader.CheckProtocols` made public so `--share`'s `protocols=` is checked exactly as
  `--mailbox`'s. Tests: `ConfiguredShareReaderTests`, `ConfiguredShareAccessReaderTests`,
  `ConfiguredBucketReaderTests`, `ConfiguredContentConflictsTests`.
- Choices (sensible defaults, decided here):
  - TLS twins are expanded by BL-988 in `Surl.Composition`, not here: ADR-0101 decision 4 says the records hold
    what was written; `ConfiguredShare.Protocols`' XML docs say so.
  - The share name's path-segment rules (`RequestPathSegments.CheckSegment`, not `.surl`) are left to BL-988:
    `RequestPathSegments` is internal to `Surl.Content`, which `Surl.Cli` does not reference.
  - `@everyone` is read as an ordinary group member; its warning and meaning are composition's (BL-988).
  - A bucket named as a share is refused at the `--bucket` (`bucket <name> is also a --share`), whichever came
    first; the duplicate checks run before the conflicts.
  - An empty grant (`team=`, a trailing `,`) is refused `expected <share>=<member>:<r|rw>,...`, as BL-645's
    reader answers a grant with no `:`.
  - `--share`'s paths are disk paths, so `Shares` is a `CommandLinePathMembers` path member (`--share`) and
    `surl-mcp` confines them to its root; `--bucket` paths are content-store paths, so `Buckets` and
    `ShareAccess` are members without paths.
  - The whole-line checks are a standalone `ConfiguredContentConflicts.Find`, tested directly; the parser has
    no rows to feed it until BL-989, which calls it from `RefuseCombination`.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so the build gate cannot run; code is written, verified on SDK 11 and left for the shift to stash
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Surl.Cli reads --share, --share-access and --bucket items into SurlCommandLine.Shares, ShareAccess and Buckets, refusing malformed, duplicate and conflicting items as ADR-0101 says
