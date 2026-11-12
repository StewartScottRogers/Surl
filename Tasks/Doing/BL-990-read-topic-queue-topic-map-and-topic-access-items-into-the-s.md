---
id: BL-990
title: Read --topic, --queue, --topic-map and --topic-access items into the Surl.Cli configuration model
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-645, BL-646, BL-1035]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-990 — Read --topic, --queue, --topic-map and --topic-access items into the Surl.Cli configuration model

## Goal

`Surl.Cli` can read a `--topic <filter>[;retain;qos=N]` item, a `--queue <name>[;durable]` item, a
`--topic-map <proto>:<name>=<proto>:<name>` item and a `--topic-access <name>=<user|@group>:<pub|sub>,...` item into
the configuration model (`SurlCommandLine` members, each empty by default), each read and refused as ADR-0101 says,
fully unit tested. No option row uses the readers yet; BL-992 adds the rows.

## Context

- First of the three tasks BL-666 ("Add --topic, --queue, --topic-map and --topic-access and compose them into
  the message bus") was split into on 2026-11-06 for the 60-minute size limit, in the order BL-653's split proved
  (BL-880, BL-882, BL-883): this (model and readers), BL-991 (composition), BL-992 (option rows and help text).
- Decisions: ADR-0101 (BL-641: names, item grammar read by BL-645's `ConfigurationItemReader`, refusals, bounds)
  and ADR-0102 (BL-642) decisions 3, 4 and 5. The ADRs win over this summary.
- A `--topic-map` naming a protocol no registered server speaks is accepted or refused as the ADR says (the extended
  brokers may be off under `SURL_EXTENSIONS=0`, ADR-0083 decision 6); if that check needs the registered schemes,
  it belongs to BL-991.
- Adding `SurlCommandLine` members makes `Surl.Mcp.UnitTests`'
  `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` fail until each is classified in
  `Application/Surl.Mcp.UnitLibrary` - a one-line entry per member.
- Stand-in: none in this task; it reads configuration only.

## Acceptance criteria

- [ ] Named tests in `Application/Surl.Cli.UnitTests` read each item form (`retain`, `qos=`, `durable`, a map, an
      `@group` access) and refuse each malformed or duplicate item with the ADR's text.
- [ ] `Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce` passes.
- [ ] `dotnet build Application/Surl.Cli.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 4): code written but unverified. `dotnet build` fails with "A compatible .NET SDK was
  not found": `global.json` pins 10.0.401 and the machine has only 10.0.100-rc.1, 11.0.100-rc.2 and
  11.0.100. Returned to Backlog behind BL-1035, as BL-987 was. The uncommitted work is left for the
  shift's stash:
  - Records `ConfiguredTopic`, `ConfiguredQueue`, `ConfiguredTopicMap` (with `Source`, `<scheme>:<name>`)
    and `ConfiguredTopicAccess`.
  - Readers `ConfiguredTopicReader`, `ConfiguredQueueReader`, `ConfiguredTopicMapReader` and
    `ConfiguredTopicAccessReader`, each with `Shape`, `Reading`, `Read` and the row 14 given-twice text.
  - `SurlCommandLine.Topics`, `Queues`, `TopicMaps` and `TopicAccess`, each empty by default, classified
    in `CommandLinePathMembers.MembersWithoutPaths`.
  - Tests in `Application/Surl.Cli.UnitTests/ConfiguredMessagingReadersTests.cs`. Split that file into one
    `<Reader>Tests` class per reader to meet `.claude/rules/testing.md`.
- Decisions (sensible defaults, ADR-0101 and ADR-0102 already cover them, so no new ADR):
  - `qos` takes exactly `0`, `1` or `2`; anything else is `is badly used here` (row 10).
  - A `--topic-map` side splits at its first `:` after the head is decoded. The scheme is lower-cased and
    checked against every scheme some surl server speaks (`ConfiguredMailboxReader.IsAnySurlScheme`),
    the same way `--mailbox protocols=` is. The narrower checks happen at start in BL-991: whether a
    registered messaging server speaks the scheme, and whether the right side is a valid name for
    `<scheme>`.
  - Row 14's noun list has no separate word for a repeated `--topic-access` filter, so it uses `topic`
    (`ConfiguredTopicReader.TopicGivenTwice`).
  - A member granted twice in one list is `member <m> is given twice`. An account and a group with the
    same name (`ops`, `@ops`) count as two different members.
  - `@everyone` is accepted here. Warning about it is BL-991's (ADR-0102 decision 3.4).
  - Duplicates across items are refused after the whole command line is read. That check needs the
    option rows, so BL-992 wires it with `RefuseRepeatedName`, using the given-twice texts above.
  - Topic filter syntax (wildcard placement) is not checked when the command line is read; the bus
    checks filters at composition (BL-991).

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: the pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; code is written and left uncommitted for the shift's stash
- 2026-11-11: Backlog -> Doing.
