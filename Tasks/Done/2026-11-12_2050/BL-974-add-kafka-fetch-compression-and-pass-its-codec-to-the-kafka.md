---
id: BL-974
title: Add --kafka-fetch-compression and pass its codec to the Kafka broker
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-973]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Mcp.UnitLibrary]
requirement: none
created: 2026-11-06
completed: 2026-11-12
---
# BL-974 — Add --kafka-fetch-compression and pass its codec to the Kafka broker

## Goal

`surl --kafka-fetch-compression zstd kafka://127.0.0.1:0` serves fetched record batches compressed with zstd, the
default is `none`, and any other value is refused as the parser refuses an unknown enumerated argument.

## Context

- Second of the two tasks BL-522 was split into (see BL-973).
- Decision: ADR-0105 decision 4 - `--kafka-fetch-compression <none|gzip|snappy|lz4|zstd>`, default `none`, help
  category `kafka`, passed to the broker as the codec of the batches it serves (BL-521 built the codecs; BL-699 the
  zstd encoder). It needs its `OptionArgumentType` with the five allowed values, help, `ManualText` line and
  `--aihelp` facts (ADR-0034, ADR-0046).
- Composition: where BL-973 registers the broker in `Application/Surl.Composition.UnitLibrary`.
- Stand-in: no store change; a test observes the codec on fetched batches.

## Acceptance criteria

- [x] A named `Surl.Cli.UnitTests` test reads each of the five values and refuses `brotli` with `FailedInit` (2)
      and the option-error text an unknown enumerated argument gets.
- [x] A named `Surl.Composition.UnitTests` test shows `--kafka-fetch-compression zstd` reaching the broker, and
      `none` when the option is absent.
- [x] The `--aihelp` completeness tests named in root `CLAUDE.md` pass, and `--aihelp kafka` lists the option.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

- Stored in `SurlCommandLine.KafkaFetchCompression` as the lower-cased word (Cli cannot reference the Kafka library); Composition parses it into `KafkaCompressionCodec` and hands it to `KafkaFetchAnswerer` through `ComposedProtocolServices.KafkaFetchCompression`. Unknown word: `is badly used here`, exit 2 (as `--cert-type`).
- `touches` gained `Application/Surl.Mcp.UnitLibrary` (its `CommandLinePathMembers` classifies every `SurlCommandLine` member); no task in Doing names it.
- Tests: `KafkaFetchCompressionOptionTests`, `ComposeAsync_KafkaFetchCompression_ReachesTheBrokersFetchAnswerer`; pinned help and manual tests updated (new KAFKA OPTIONS manual section).


## Log

- 2026-11-06: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. surl --kafka-fetch-compression <none|gzip|snappy|lz4|zstd> reaches the Kafka broker's Fetch; unknown words refused with exit 2
