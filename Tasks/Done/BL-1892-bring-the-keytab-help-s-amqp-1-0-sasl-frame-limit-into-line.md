---
id: BL-1892
title: Bring the --keytab help's AMQP 1.0 SASL frame limit into line with BL-1884
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1884]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1892 — Bring the --keytab help's AMQP 1.0 SASL frame limit into line with BL-1884

## Goal

The `--keytab` manual and AI help text state the AMQP 1.0 SASL frame limit that BL-1884's ADR-0107 amendment decides.

## Context

- BL-1883 wrote "512 for an AMQP 1.0 SASL frame, so a larger one closes the connection unchecked" into the second Kerberos paragraph of `ManualText.cs` and the `--keytab` paragraph of `AiHelpProse.cs`, true while BL-1884 was not Done.
- Tests that pin it: `ManualTextTests.Text_SaysTheAmqpServersUseTheKeytabForTheServiceAmqpWithinTheirFrameLimits` (and `Text_IsTheWholeManual`), and `AiHelpTextTests.Answer_Auth_SaysTheAmqpServersUseTheKeytabForTheServiceAmqpWithinTheirFrameLimits`.
- If BL-1884 keeps 512 bytes, nothing changes: record that in Notes and finish.

## Acceptance criteria

- [x] The manual and the AI help state the same AMQP 1.0 SASL frame limit as ADR-0107's latest amendment.
- [x] A named test in `Surl.Cli.UnitTests` pins that sentence.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- ADR-0107's BL-1884 amendment reads a SASL frame up to 65536 bytes, or `--max-message` when smaller, never below 512, and `--max-message 0` leaves 65536 (`Amqp1FrameLimits.LargestSaslFrameSizeRead`). `ManualText.cs` and the `auth` paragraph in `AiHelpProse.cs` now say that. The AMQP 0-9-1 limit of 4096 bytes is unchanged.
- Pinned by `ManualTextTests.Text_SaysTheAmqpServersUseTheKeytabForTheServiceAmqpWithinTheirFrameLimits` and `AiHelpTextTests.Answer_Auth_SaysTheAmqpServersUseTheKeytabForTheServiceAmqpWithinTheirFrameLimits`.
- Verified 2026-11-27: build 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests no failures (`Surl.Cli.UnitTests` 1989 passed). No ADR: no new decision. Integration tests not run: help text only.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The --keytab manual and AI help state the AMQP 1.0 SASL frame limit of ADR-0107's BL-1884 amendment: 65536 bytes, or --max-message when smaller, never below 512
