---
id: BL-1028
title: Serve oci from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-438, BL-405, BL-655]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-1028 — Serve oci from the surl executable

## Goal

`surl oci://127.0.0.1:0` starts the OCI registry over TLS with its storage and token endpoint as ADR-0115 decides,
its `--aihelp` topic and help category exist, and the extension switch turns it off.

## Context

- First of the two tasks BL-439 ("Serve oci from the surl executable") was split into on 2026-11-06 for the
  60-minute size limit: this, then BL-971 (`--oci-plaintext`).
- Decision: ADR-0115 (BL-435) - HTTPS by default; the token endpoint's realm is
  `<scheme>://<authority>/token`; storage is the content store's (ADR-0130 for upload staging).
- Composition: extended servers register in `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs`
  and `SurlServerComposition` over the shared content store (as BL-421 did for `s3`, commit efa2f1bc); `oci` is
  wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010). If the registry keeps state beyond the content store's,
  load it after the data-directory lock, as the mail store is loaded (`ServiceState`).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"; ADR-0046): a `HelpCategories` row with `oci`, an
  `AiHelpProse.TopicAbout` paragraph naming the distribution spec as its oracle (true of what lands here; BL-971 adds
  the `--oci-plaintext` sentence), an `AiHelpExamples` entry, and the topic list in `AiHelpTextTests` growing by one.
- Shared stores (Stewart, 2026-11-04; BL-622's ADR decision 1): the registry is handed the **same** `ContentStore`
  instance every original file server gets (repositories resolve through BL-655's lookup), and the same account set.
- Stand-in: the shared content store is the store; a test seeds blobs through `Surl.Api`'s file seeding and
  observes its write events.

## Acceptance criteria

- [ ] A named test finds `oci` registered when the switch is on, and absent when it is off.
- [ ] A named test shows the `oci` server and the `ftp` and `http` servers composed with one and the same
      `ContentStore` instance.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
