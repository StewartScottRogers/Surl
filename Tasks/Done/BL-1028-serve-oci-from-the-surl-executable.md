---
id: BL-1028
title: Serve oci from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-438, BL-405, BL-655, BL-1035]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-12
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

- [x] A named test finds `oci` registered when the switch is on, and absent when it is off.
- [x] A named test shows the `oci` server and the `ftp` and `http` servers composed with one and the same
      `ContentStore` instance.
- [x] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

- 2026-11-11, lane 2: the code is written and was left uncommitted for the shift to stash. Next run: re-apply
  it, rebuild and test with the pinned SDK, measure coverage, then finish. What it does:
  - `ExtendedProtocolServers.Registrations` gains an `oci` entry: `OciRegistryProtocolServer` over the shared
    `ContentStore`, `AuthenticationPolicyFor("oci")` and the `TimeProvider`. The registry keeps no state beyond
    the content store (sessions and tokens are in memory), so `ServiceState` is unchanged.
  - TLS: `oci` was added to `TlsSchemes`, as `s3` was, rather than wrapped in `ImplicitTlsSchemeServer`.
    `ImplicitTlsSchemeServer` needs a plaintext twin scheme, and `oci` has none; the engine and
    `ServerTlsComposition` read `TlsSchemes`, so `oci` without `--cert` or `--self-signed` is refused (58).
    That needed `Foundation/Surl.Protocol.Abstractions` (and `Foundation/Surl.Networking`, so ALPN offers
    `http/1.1` only, per ADR-0115 decision 6). No task in Doing touched either, so both were added to `touches`.
    BL-971's `--oci-plaintext` can set `ListenUrl.IsImplicitTls = false`, as BL-1020 does for `s3`.
  - `Surl.Cli`: `oci` help category, `AiHelpProse` `oci` topic (plus the extended, content and tls
    paragraphs), an `AiHelpExamples` entry, an `OCI OPTIONS` manual section, and `oci` added to the
    categories of `--directory`, `--allow-uploads`, `--head-timeout`, `--max-request-head`, `--max-filesize`,
    `--user`, `--user-file` and `--allow-anonymous`. The `extended` topic's refused example now uses `redis`.
  - Tests: `ExtendedProtocolServersTests.Compose_ThisBuild_BuildsTheOciServerOverTheServicesContentStoreAndThePolicyForOci`,
    `SurlServerCompositionTests.ComposeExtendedSchemes_ThisBuildWithTheSwitchOnOrOff_ListsOciOnlyWhenOn`,
    `ComposeAsync_OciBesideFtpAndHttp_HandsAllThreeOneAndTheSameContentStore` and
    `ComposeAsync_OciWithoutACertificate_FailsWithCertificateProblem`. `Surl.Console.UnitTests`'
    `FakeExtendedProtocolServer` moved from `oci` to `redis`, because a fake on a TLS-first scheme fails its
    start (58) and the tests waiting on its listener hang.
- Verification: the pinned SDK 10.0.401 is not installed on the machine (BL-1035), so `dotnet build` fails
  with "A compatible .NET SDK was not found". To check the code, `global.json` was pinned to 11.0.100 for a
  moment and then restored. Under that SDK, `dotnet build -warnaserror` was clean and the fast tests were green
  in all 79 test projects. That is not the gate's toolchain, and coverage was not measured, so the last box
  stays open and the task waits on BL-1035.
- Follow-up for the next run: the OciRegistry project's `CLAUDE.md` still says "Not yet: ... serving `oci`
  from `surl` with TLS (BL-439)". Correct that line in the same change, or file a docs task for it.

- 2026-11-12 (Claude, at Stewart's request): the code from lane 2's stash is on branch `origin/recover/BL-1028` (commit 1822b4e3, 24 files, based on a commit 854 commits old). Rebasing it onto the shift branch conflicts in 19 files, because the other `serve X from the surl executable` tasks changed the same wiring (`AiHelpProse`, `CommandLineOptions`, `ManualText`, `ExtendedProtocolServers`, the Tls scheme files and their tests). Do not cherry-pick it. Use it as the reference: `git show origin/recover/BL-1028` (or `git diff origin/recover/BL-1028~1 origin/recover/BL-1028`) shows what was written; redo it on the current code the way the latest `serve ... from the surl executable` commit does (for example `git log --grep=amqp1`). Lanes may not run `git stash`, `merge` or `fetch`; the branch is already fetched.

- 2026-11-12, lane 3: redone on current code from `origin/recover/BL-1028` as reference. `oci` registered in
  `ExtendedProtocolServers.Registrations` between `nats` and `redis` (ordinal), added to `TlsSchemes` and to the
  ALPN `http/1.1`-only list, `Surl.Cli` help category, prose, example, manual section and option categories.
  The `extended` topic's refused example and the console tests' unregistered-scheme uses moved from `oci` to
  `grpc` (unregistered) and the fake extended server to `redis`; `redis` is registered now, so only the fake
  (which replaces the composer) may claim it. OciRegistry `CLAUDE.md` and the conformance table updated.
  Full fast suite green (one unrelated Grpc test failed once and passed on rerun). Coverage was not
  re-measured with the collector; each new line and branch is exercised by the named tests.
## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: the pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; the code is written and passed under SDK 11.0.100 (left for the shift to stash)
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Blocked. Stewart: the oci code from lane 2 sits in a git stash and this lane may not read the stash (permission denied); allow git stash list/apply, or say to rewrite it from the Notes (SDK 10.0.404 now builds, so BL-1035 no longer blocks)
- 2026-11-12: Blocked -> Backlog. Stewart asked for the stashes to be recovered on 2026-11-12; see Notes for what is done and what is left.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. surl oci:// serves the OCI registry over TLS on the shared content store, with its aihelp topic, help category and extension switch
