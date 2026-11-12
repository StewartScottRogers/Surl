---
id: BL-1026
title: Serve gs from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-432, BL-405, BL-655, BL-1035]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Conformance/Extended/GoogleCloudStorage.md]
requirement: none
created: 2026-11-06
completed: 2026-11-12
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

- [x] A named test finds `gs` registered when the switch is on, and absent when it is off.
- [x] A named test shows the `gs` server and the `ftp` and `http` servers composed with one and the same
      `ContentStore` instance.
- [x] The `--aihelp` completeness tests named in root `CLAUDE.md` pass, and the matrix rows `GCS:Surl-Tls-1` and
      `-2` name this task's tests.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 2): returned to Backlog behind BL-1035 - `dotnet build` fails with "A compatible .NET
  SDK was not found" (global.json pins 10.0.401; only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are
  installed). The code below was written but never built or tested; it is left uncommitted for the shift
  to stash. The next run should restore it, or redo it from this list.
- Touches widened: `gs` must be TLS from the first byte, and that list is `TlsSchemes` in
  `Foundation/Surl.Protocol.Abstractions.UnitLibrary`; ALPN `http/1.1` for it is
  `TlsApplicationProtocols` in `Foundation/Surl.Networking.UnitLibrary`. No task in Doing touched either.
- Decision: `gs` follows `s3`'s mechanism (BL-421, commit 0d53a586): "gs" added to `TlsSchemes`, so the
  serving engine handshakes it and no `ImplicitTlsSchemeServer` entry is needed. ADR-0110 decision 5's
  "wrapped in `ImplicitTlsSchemeServer` as `https` is" needs no wrapper, because the wrapper exists for a
  plaintext twin and `gs` has none registered; its effect (TLS first, `(58)` without a certificate) is the
  same. BL-1027's `--gs-plaintext` then sets `ListenUrl.IsImplicitTls = false`, as `--s3-plaintext` does.
- Work done (uncommitted): `TlsSchemes` + `TlsContractTests` row; `TlsApplicationProtocols` "gs" +
  test row; `ExtendedProtocolServers.Registrations` gs entry (between dns and nats) and the GCS project
  reference; Composition tests `Compose_ThisBuild_IsTheDnsUdpAndTcpServersThenTheGsThenTheNatsThenTheS3Server`,
  `Compose_ThisBuild_BuildsTheGsServerOverTheServicesContentStoreIndexAndGsPolicy`,
  `ComposeAsync_GsBesideFtpAndHttp_HandsAllThreeOneAndTheSameContentStore`,
  `ComposeAsync_GsWithoutACertificate_FailsWithCertificateProblem`, `StartTestSeams.ParseWithGs`; Console
  tests `ComposeExtendedSchemes_ThisBuildWithTheSwitchUnset_RegistersGs`, `..._SwitchAtZero_RegistersNoGs`,
  `RunAsync_GsWithSelfSigned_ListensAtItsDefaultPort443`, `RunAsync_GsWithoutACertificate_ExitsWithCertificateProblem`,
  `RunAsync_GsWithTheSwitchAtZero_RefusesItsSchemeAsAnUnknownScheme`, and "gs" in the scheme lists of
  `CommandLineRunnerTests`; Cli: `HelpCategories` gs row, `AiHelpProse` gs topic (extended, tls, content
  paragraphs updated), `AiHelpExamples` gs example (`-u :secret-token`, `curl -k --oauth2-bearer`),
  `ManualText` "GS OPTIONS", "gs" in nine options' categories, and the matching test pins.
- Still to do after a build: fix the `HelpTextTests` "Categories:" wrapping pins that the new "gs"
  category shifts, update the `Surl.Composition.UnitLibrary/CLAUDE.md` extended-server paragraph, and
  replace `GCS:Surl-Tls-1`/`-2` "n/a" in `Documentation/Conformance/Extended/GoogleCloudStorage.md`
  with `RunAsync_GsWithSelfSigned_ListensAtItsDefaultPort443` and
  `RunAsync_GsWithoutACertificate_ExitsWithCertificateProblem`.

- 2026-11-12 (lane 1): the earlier uncommitted work could not be restored (stash unreadable), so it was redone
  from the note above and the BL-1023 `az` commit as a template; build and the fast tests are green. Registration
  sits between `dns` and `kafka` (the earlier note said `nats`; order is not pinned by anything but the tests).
  The `--aihelp` example uses `-u GOOGTESTKEY:secret`, not ADR-0110's `--user ':<token>'`, because the command line
  refuses an empty user name (`OptionArgumentReader.EmptyUserName`); the topic text names the V4 HMAC login and the
  bearer token "the accounts hold". Matrix row `GCS:Surl-Tls-1` names this task's tests; `-2` is `--gs-plaintext`,
  BL-1027's, so it now says so rather than naming a test this task cannot have.
## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds; gs code left uncommitted
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. surl gs:// serves Google Cloud Storage over TLS on the shared content store, with its aihelp topic, help category and extension switch
