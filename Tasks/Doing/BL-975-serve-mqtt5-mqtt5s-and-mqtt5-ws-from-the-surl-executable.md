---
id: BL-975
title: Serve mqtt5, mqtt5s and mqtt5+ws from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-500, BL-405, BL-635, BL-1035]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-975 — Serve mqtt5, mqtt5s and mqtt5+ws from the surl executable

## Goal

`surl mqtt5://`, `surl mqtt5s://` (TLS) and `surl mqtt5+ws://` start the MQTT 5 broker on the shared message bus.
`surl mqtt://` still starts the original 3.1.1 server exactly as before. The `--aihelp` topic and help category
exist, and the extension switch turns all three off.

## Context

- First of the two tasks BL-501 ("Serve mqtt5, mqtt5s and mqtt5+ws from the surl executable") was split into on
  2026-11-06 for the 60-minute size limit: this, then BL-976 (`--mqtt5-disconnect-after-retained`).
- Decision: ADR-0093 (BL-494), decisions 0 and 7; BL-500 built the broker this registers.
- Composition: extended servers register in `ExtendedProtocolServers.Registrations`
  (`Application/Surl.Composition.UnitLibrary`); the shared bus is `ComposedProtocolServices.MessageBus`, and the
  one-bus test belongs in `Surl.Composition.UnitTests`. `mqtt5s` is wrapped for TLS (`ImplicitTlsSchemeServer`,
  ADR-0010).
- **Shared-stores decision (Stewart, 2026-11-04; BL-622's ADR amends ADR-0093 decision 0):** the broker is handed
  the **one** `Surl.MessageBus` BL-635 composes for `mqtt` and `mqtts`, not state of its own, so its retained
  messages persist where the bus persists them (`<path>/.surl/bus/`, BL-634), not in
  `<path>/.surl/mqtt5/retained-messages`. Only session state stays the broker's.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with the three schemes, an
  `AiHelpProse.TopicAbout` paragraph naming MQTT 5.0 as its oracle and saying `mqtt` stays 3.1.1, an
  `AiHelpExamples` entry, and the topic list in `AiHelpTextTests` growing by one.
- Stand-in: the shared message bus is the store; a test seeds and observes it through `Surl.Api`'s message seeding
  and the bus's events.

## Acceptance criteria

- [ ] A named test shows the `mqtt5` broker and the `mqtt` server composed with one and the same bus instance.
- [ ] A named test finds `mqtt5`, `mqtt5s` and `mqtt5+ws` registered when the switch is on, and absent when it is
      off, with `mqtt` and `mqtts` registered either way.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass. `surl --aihelp mqtt` is unchanged except for
      a cross-reference, if the ADR adds one.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

- 2026-11-11: Not started. `dotnet build` fails with "A compatible .NET SDK was not found": global.json pins 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed. Added BL-1035 (reinstall the pinned SDK) to depends-on, as BL-507 and BL-1023 did.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-11: Backlog -> Doing.
