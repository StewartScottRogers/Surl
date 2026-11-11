---
id: BL-990
title: Read --topic, --queue, --topic-map and --topic-access items into the Surl.Cli configuration model
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-645, BL-646]
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

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
