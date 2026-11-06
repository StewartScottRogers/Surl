---
id: BL-421
title: Serve s3 from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-420, BL-405, BL-655]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-421 — Serve s3 from the surl executable

## Goal

`surl s3://127.0.0.1:0` starts the S3 server with the content store, accounts, `--allow-uploads`,
`--allow-anonymous` and TLS as BL-417's ADR decides. Its `--aihelp` topic and help category exist, and the
extension switch turns it off.

## Context

- Composition: the extended-server composition from BL-405 in `Application/Surl.Console/CommandLineRunner.cs`,
  with the default port from the extended port table. If the ADR serves `s3` over TLS, wrap the server as the
  original `https` is wrapped (`ImplicitTlsSchemeServer`, ADR-0010).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"; ADR-0046): a `HelpCategories` row with its scheme, an
  `AiHelpProse.TopicAbout` paragraph saying the server is judged by the S3 API Reference and not by upstream
  curl, an `AiHelpExamples` entry, and the topic list pinned in `AiHelpTextTests` growing by one.
- Any new option needs its `OptionArgumentType` and its categories (ADR-0034).
- Shared stores (Stewart, 2026-11-04; BL-622's ADR decision 1): `s3` is handed the **same** `ContentStore`
  instance every original file server gets (and its bucket names resolve through BL-655's lookup, which BL-663's
  `--bucket` fills), and the same account set. If BL-609 has moved the composition to
  `Application/Surl.Composition.UnitLibrary` (`ExtendedProtocolServers.Compose` keeps its name), register it there
  and record the changed touches in Notes.

## Acceptance criteria

- [x] A named `Surl.Console.UnitTests` test composes the servers and finds `s3` registered when the switch is on,
      and absent when it is off.
- [x] A named test shows the `s3` server and the `ftp` and `http` servers composed with one and the same
      `ContentStore` instance.
- [x] `CommandLineRunnerAiHelpTests.RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic`,
      `AiHelpTextTests.Topics_AreTheAdrsTopicsAndEachProtocolAddedInOrdinalOrder`,
      `AiHelpTextTests.Answer_EveryTopicAndTheOverview_HasItsAboutText`,
      `AiHelpTextTests.Answer_EveryTopicButExitCodesAndSecurity_HasAnExample` and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- **Touches widened (2026-11-05).** BL-609 moved the composition to `Application/Surl.Composition.UnitLibrary`, so
  the S3 server is registered there (its `UnitTests` with it). Serving `s3` over TLS from the first byte needed
  `s3` in `Surl.Protocol.Abstractions`' `TlsSchemes` and `http/1.1` as its ALPN offer in `Surl.Networking`'s
  `TlsApplicationProtocols` (each with its `UnitTests`). No task in Doing named any of these.
- **TLS.** `s3` is in `TlsSchemes`' implicit-TLS set, so the engine handshakes before the server sees the
  connection and the existing `(58) ... needs a certificate` refusal applies; the `S3ProtocolServer` is registered
  directly (its own scheme is `s3`), no `ImplicitTlsSchemeServer`.
- **`--s3-plaintext` left to BL-860.** ADR-0108 decision 5 assigns it here, but the engine, the TLS composition and
  ALPN decide TLS by scheme alone through a static set; making it per listener without static mutable state is a
  contract change to `Surl.Protocol.Abstractions` and `Surl.Core`, its own task. None of the acceptance criteria
  needs it.
- **Object metadata index.** The one `ObjectMetadataIndex` is service state (`ServiceState.ObjectMetadataIndex`),
  built by `ComposeObjectMetadataIndex` over the content store's own file system and served root (persisted in
  `<path>/.surl/objects/index` with `--directory`), loaded after the LDAP directory, `(37) Could not read <file>:
  <reason>` when it cannot be. `ExtendedProtocolServerServices` gained it and the `TimeProvider`.
- **Help.** New `s3` help category, "Amazon S3 protocol (extended)", listed by the options the S3 server reads
  (`--directory`, `--allow-uploads`, `--list-directories`, `--head-timeout`, `--max-request-head`, `--max-filesize`,
  `-u`, `--user-file`, `--allow-anonymous`); `--aihelp s3` paragraphs and an example; an `S3 OPTIONS` manual
  section; the `extended`, `tls` and `content` paragraphs mention S3. The `extended` topic's "no server answers"
  example and the fake extended server in `Surl.Console.UnitTests` now use `oci`, which no server answers yet.
- **ADR.** ADR-0108's amendment is filed as BL-861: `Documentation/Planning/Decisions` is in BL-822's `touches`
  (Doing), so this task did not edit it.
- **Tests for the criteria.** `CommandLineRunnerExtendedProtocolTests.ComposeExtendedSchemes_ThisBuildWithTheSwitchUnset_RegistersS3`
  and `...ComposeExtendedSchemes_ThisBuildWithTheSwitchAtZero_RegistersNoS3` (criterion 1);
  `SurlServerCompositionTests.ComposeAsync_S3BesideFtpAndHttp_HandsAllThreeOneAndTheSameContentStore` (criterion
  2, `Surl.Composition.UnitTests`, reading each server's one `ContentStore` field); the five named `--aihelp`
  tests pass (criterion 3); build `-warnaserror` clean, fast tests green, coverage 100/100 for `Surl.Console`,
  `Surl.Cli`, `Surl.Composition`, `Surl.Networking` and `Surl.Protocol.Abstractions` (criterion 4).
- **Filed:** BL-860 (`--s3-plaintext`), BL-861 (ADR-0108 amendment).

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-655; composed with the one shared content store.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. surl s3://127.0.0.1:0 --self-signed serves the S3 server over TLS on the shared content store, with its help category and --aihelp topic; SURL_EXTENSIONS=0 turns it off
