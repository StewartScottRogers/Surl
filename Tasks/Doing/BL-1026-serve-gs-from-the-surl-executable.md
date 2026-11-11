---
id: BL-1026
title: Serve gs from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-432, BL-405, BL-655]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Conformance/Extended/GoogleCloudStorage.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-1026 — Serve gs from the surl executable

## Goal

`surl gs://127.0.0.1:0` starts the Google Cloud Storage server over TLS on the shared content store, its
`--aihelp` topic and help category exist, and the extension switch turns it off.

## Context

- First of the two tasks BL-433 ("Serve gs from the surl executable") was split into on 2026-11-06 for the
  60-minute size limit: this, then BL-1027 (`--gs-plaintext`).
- Decision: ADR-0110 decision 5 - `gs` is implicit TLS on 443, ALPN `http/1.1`, `--cert` or `--self-signed`
  required, `CertificateProblem` (58) without; decision 8 gives the topic's about text.
- Composition: extended servers register in `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs`
  (`Registrations`, beside `s3`, with its project reference, as BL-421 did); `gs` is wrapped for TLS
  (`ImplicitTlsSchemeServer`, ADR-0010).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"; ADR-0046): a `HelpCategories` row with `gs`, an
  `AiHelpProse.TopicAbout` paragraph naming the XML API as its oracle (true of what lands here; BL-1027 adds the
  `--gs-plaintext` sentence), an `AiHelpExamples` entry, and the topic list in `AiHelpTextTests` growing by one.
- Shared stores (Stewart, 2026-11-04; BL-622's ADR decision 1): `gs` is handed the **same** `ContentStore`
  instance every original file server gets (buckets resolve through BL-655's lookup), and the same account set.
- Replace the matrix's `GCS:Surl-Tls-1` and `-2` "n/a" entries in
  `Documentation/Conformance/Extended/GoogleCloudStorage.md` with this task's test names.
- Stand-in: the shared content store is the store; a test seeds it through `Surl.Api`'s file seeding and
  observes its write events.

## Acceptance criteria

- [ ] A named test finds `gs` registered when the switch is on, and absent when it is off.
- [ ] A named test shows the `gs` server and the `ftp` and `http` servers composed with one and the same
      `ContentStore` instance.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass, and the matrix rows `GCS:Surl-Tls-1` and
      `-2` name this task's tests.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
