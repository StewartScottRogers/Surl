---
id: BL-1015
title: Accept --fault error replies for amqp, amqp1, kafka, mqtt5 and nats in Surl.Cli
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1011, BL-1012, BL-1013, BL-1014, BL-1031]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: FR-062
created: 2026-11-06
completed:
---
# BL-1015 — Accept --fault error replies for amqp, amqp1, kafka, mqtt5 and nats in Surl.Cli

## Goal

`surl --fault "error ... reply=<reply>"` naming `amqp`, `amqps`, `amqp1`, `amqp1s`, `kafka`, `kafkas`, `mqtt5`,
`mqtt5s`, `mqtt5+ws`, `nats` or `natss` is accepted when the reply fits that protocol's error shape and refused
otherwise, instead of every such rule being refused at configuration.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md)
  decision 9 and its extended reply table. Shapes, each matching what its broker's own reader accepts (read the
  broker code BL-1011 to BL-1031 landed, so a reply `Surl.Cli` accepts is one the broker applies):
  AMQP 0-9-1 a reply code from the specification's exception list, a space and text (BL-1011's
  `TryReadException`, text at most 255 bytes); AMQP 1.0 an error condition symbol and an optional description
  (BL-1012's `ReadChosenError`); Kafka an error code 1 to 127 (BL-1013); MQTT 5 the existing
  `IsMqttReasonCode` (128 to 255, no text); NATS non-empty text (BL-1031).
- Where: `Application/Surl.Cli.UnitLibrary/FaultReplyShapes.cs` (`FitsByScheme`, ordinal-sorted) and
  `Application/Surl.Cli.UnitTests/CommandLineParserFaultTests.cs`, beside the `coap`, `mcp` and `signalr`
  rows ADR-0144 added. Update the class's summary, which still says BL-834 has no rows.
- Split from BL-834 (timed out after 120 min, 2026-11-06). Its stash (`darkfactory BL-834 20261106-033059`,
  commit `0980dd737c0c07b309c7c1f49872a4a4719d51d5`, called `S`) has only the eleven `FitsByScheme` entries in
  `git diff "S^" "S" -- Application/Surl.Cli.UnitLibrary/FaultReplyShapes.cs`, none of the predicates and no
  tests. Re-make the entries by hand (the file has since changed), and cite ADR-0124 decision 9, not the stash's
  mistaken "ADR-0142".
- Stand-in: this is the configuration gate in front of BL-1011 to BL-1031's faults; it publishes nothing itself.
  If the `--aihelp` fault topic or an `AiHelpExamples` entry lists which schemes take `error` replies, update it
  in the same diff (`CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`).

## Acceptance criteria

- [ ] `Parse_ErrorReplyFittingItsScheme_IsServed` has a data row accepting a fitting `reply` for each of the eleven
      schemes, including `amqps`, `amqp1s`, `kafkas`, `mqtt5s`, `mqtt5+ws` and `natss`.
- [ ] `Parse_FaultRefused_IsRefusedWithItsReason` has, per family (amqp, amqp1, kafka, mqtt5, nats), at least one
      data row refusing a misfit `reply` with `option --fault: key reply does not fit the <scheme> error shape`.
- [ ] Every existing `Surl.Cli.UnitTests` test passes.
- [ ] `dotnet build Application/Surl.Cli.UnitTests -warnaserror` is clean, the fast tests are green, and
      `Surl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-834 (timed out after 120 min); its stash `darkfactory BL-834 20261106-033059` holds only the scheme rows.
