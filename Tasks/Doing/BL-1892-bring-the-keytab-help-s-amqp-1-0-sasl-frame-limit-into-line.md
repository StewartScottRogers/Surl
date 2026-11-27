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
completed:
---
# BL-1892 — Bring the --keytab help's AMQP 1.0 SASL frame limit into line with BL-1884

## Goal

The `--keytab` manual and AI help text state the AMQP 1.0 SASL frame limit that BL-1884's ADR-0107 amendment decides.

## Context

- BL-1883 wrote "512 for an AMQP 1.0 SASL frame, so a larger one closes the connection unchecked" into the second Kerberos paragraph of `ManualText.cs` and the `--keytab` paragraph of `AiHelpProse.cs`, true while BL-1884 was not Done.
- Tests that pin it: `ManualTextTests.Text_SaysTheAmqpServersUseTheKeytabForTheServiceAmqpWithinTheirFrameLimits` (and `Text_IsTheWholeManual`), and `AiHelpTextTests.Answer_Auth_SaysTheAmqpServersUseTheKeytabForTheServiceAmqpWithinTheirFrameLimits`.
- If BL-1884 keeps 512 bytes, nothing changes: record that in Notes and finish.

## Acceptance criteria

- [ ] The manual and the AI help state the same AMQP 1.0 SASL frame limit as ADR-0107's latest amendment.
- [ ] A named test in `Surl.Cli.UnitTests` pins that sentence.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
