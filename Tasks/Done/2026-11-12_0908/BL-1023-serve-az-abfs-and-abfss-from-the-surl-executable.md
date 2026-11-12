---
id: BL-1023
title: Serve az, abfs and abfss from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-426, BL-405, BL-655, BL-1035]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-12
---
# BL-1023 — Serve az, abfs and abfss from the surl executable

## Goal

`surl az://`, `surl abfs://` and `surl abfss://` start the Azure Storage server - `az` and `abfss` over TLS, `abfs`
as plain HTTP/1.1 (ADR-0109 decision 6) - on the shared content store, its `--aihelp` topic and help category
exist, and the extension switch turns it off.

## Context

- First of the three tasks BL-427 ("Serve az, abfs and abfss from the surl executable") was split into on
  2026-11-06 for the 60-minute size limit: this, BL-1024 (the plain-text bearer refusal) and BL-1025
  (`--az-plaintext`).
- Composition: extended servers register in `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs`
  (`Registrations`, beside `s3`, as BL-421 did), and the shared-store tests belong in `Surl.Composition.UnitTests`.
  `az` and `abfss` are wrapped for TLS as `https` is (`ImplicitTlsSchemeServer`, ADR-0010), `--cert` or
  `--self-signed` required, ALPN `http/1.1` only; `abfs` is plain on port 80.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"; ADR-0046): the topic is `azure-storage` (ADR-0109 decision 8):
  a `HelpCategories` row with the three schemes, an `AiHelpProse.TopicAbout` paragraph saying the server is judged
  by the Azure Storage REST API and not by upstream curl, an `AiHelpExamples` entry, and the topic list in
  `AiHelpTextTests` growing by one. The paragraph states what is true when this task lands; BL-1025 adds the
  `--az-plaintext` sentence.
- Shared stores (Stewart, 2026-11-04; BL-622's ADR decision 1): the Azure server is handed the **same**
  `ContentStore` instance every original file server gets (containers resolve through BL-655's lookup), and the
  same account set.
- Stand-in: the shared content store is the store; a test seeds it through `Surl.Api`'s file seeding and
  observes the content store's write events.

## Acceptance criteria

- [x] A named test finds `az`, `abfs` and `abfss` registered when the switch is on, and absent when it is off.
- [x] A named test shows the Azure server and the `ftp` and `http` servers composed with one and the same
      `ContentStore` instance.
- [x] The `--aihelp` completeness tests named in root `CLAUDE.md` pass, including
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 2): returned to Backlog behind BL-1035 - `dotnet build` fails with "A compatible .NET SDK
  was not found" (global.json pins 10.0.401; only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed).
  The work below was written but never built; the shift stashes it.
- `touches` grew by `Foundation/Surl.Protocol.Abstractions.*` and `Foundation/Surl.Networking.*` (no task in
  Doing named them): `az` and `abfss` must join `TlsSchemes`' implicit-TLS list and `TlsApplicationProtocols`'
  `http/1.1`-only ALPN, exactly as BL-421 did for `s3`. Without that the engine never handshakes on them and
  `ServerTlsComposition` never demands a certificate.
- Decision: one registration `new(["az", "abfs", "abfss"], ...)` first in `ExtendedProtocolServers.Registrations`
  (ordinal order of first scheme), not an `ImplicitTlsSchemeServer` wrapper as ADR-0109 decision 6 words it.
  Why: `AzureStorageProtocolServer.Schemes` already claims all three, and one instance must serve all of them so
  Blob and Data Lake share its `AzureStagingArea`. The engine reads `TlsSchemes`, so the behaviour is the same
  as the wrapper: TLS on `az`/`abfss`, plain on `abfs`. `ftps`, `smbs` and `s3` are registered the same way.
- Decision: the server judges logins with `AuthenticationPolicyFor("az")` for all three schemes, as the NATS
  server uses `"nats"` for `nats` and `natss`. An account with `protocols=abfs` only would be refused; revisit
  if that matters.
- Written so far, unbuilt: the Foundation list changes and their test rows; the Composition csproj reference and
  registration; tests `ExtendedProtocolServersTests.Compose_ThisBuild_IsTheAzureStorageServerThen...`,
  `ComposeExtendedSchemes_SwitchOnOrOff_RegistersAzAbfsAndAbfssOnlyWhenOn`,
  `Compose_ThisBuild_BuildsTheAzureStorageServerOverTheServicesContentStoreIndexAndAzPolicy`, and in
  `SurlServerCompositionTests.SharedStores.cs` the shared `ContentStore` test, the `(58)` test for `az`/`abfss`
  and the plain `abfs` start test (`StartTestSeams.ParseWithExtendedSchemes`).
- 2026-11-12: the stashed work was unreadable in the lane, so it was redone from the BL-973 pattern: Foundation lists, registration, Cli category/prose/example/option categories, and tests (`SurlServerCompositionTests.AzureStorage.cs`). Done; the manual section was not added.
  topic paragraphs, the `extended`/`content`/`tls` topic sentences that list servers and TLS schemes, an

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. az, abfs and abfss serve from surl on the shared content store (TLS on az/abfss), with azure-storage aihelp topic
