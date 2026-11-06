---
id: BL-439
title: Serve oci from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-438, BL-405, BL-655]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-439 — Serve oci from the surl executable

## Goal

`surl oci://127.0.0.1:0` starts the OCI registry with its storage, token endpoint and TLS as BL-435's ADR
decides. Its `--aihelp` topic and help category exist, and the extension switch turns it off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`. If
  the registry keeps state across connections, load it after the data-directory lock, as the mail store is loaded
  (`ServiceState`).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"; ADR-0046): a `HelpCategories` row with `oci`, an
  `AiHelpProse.TopicAbout` paragraph naming the distribution spec as its oracle, an `AiHelpExamples` entry, and
  the topic list in `AiHelpTextTests` growing by one.
- Shared stores (Stewart, 2026-11-04; BL-622's ADR decision 1): the registry is handed the **same** `ContentStore`
  instance every original file server gets (repositories resolve through BL-655's lookup), and the same account
  set; its state is the content store's, so no separate load is needed unless BL-435's ADR keeps something else.
  If BL-609 has moved the composition to `Application/Surl.Composition.UnitLibrary`, register it there and record
  the changed touches in Notes.

## Acceptance criteria

- [ ] A named `Surl.Console.UnitTests` test finds `oci` registered when the switch is on, and absent when it is off.
- [ ] A named test shows the `oci` server and the `ftp` and `http` servers composed with one and the same
      `ContentStore` instance.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05 (lane 4): BL-609 moved the composition into `Application/Surl.Composition.UnitLibrary`;
  `ExtendedProtocolServers.Compose` and `SurlServerComposition` are where `oci` is registered over the shared
  content store (as BL-421 did for s3, commit efa2f1bc), so `touches` now adds
  `Application/Surl.Composition.UnitLibrary` and `Application/Surl.Composition.UnitTests`. BL-803, in Doing,
  touches both, so the task returned to Backlog until BL-803 is Done. No code was written.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-655; composed with the one shared content store.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and .UnitTests, which BL-803 in Doing touches
- 2026-11-06: Backlog -> Deferred. Split into BL-1028 and BL-971 (over the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06)
