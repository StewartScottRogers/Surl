---
id: BL-567
title: Serve HTTPS over TLS-SRP from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-566]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Cryptography/Surl.TlsSrp.UnitLibrary, Cryptography/Surl.TlsSrp.UnitTests, Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-567 — Serve HTTPS over TLS-SRP from the surl executable

## Goal

`surl` serves HTTPS over TLS-SRP from the command line BL-563's ADR decision 6 fixes (planning
assumption: `surl https://127.0.0.1:8443 --tlsauthtype SRP --tlsuser jsmith --tlspassword abc`),
composing `Surl.TlsSrp` in front of `Surl.Protocol.Http`, and `surl --aihelp` documents the new
options.

## Context

- Decision: BL-563's ADR, decisions 4 to 6.
- Code: `Application/Surl.Console/CommandLineRunner.cs` and `ServerTlsComposition.cs` (today's TLS
  composition through `Surl.Networking`'s `ServerTlsHandshake`), `ImplicitTlsSchemeServer.cs`;
  `Application/Surl.Cli.UnitLibrary` (`CommandLineOptions`, `CommandLineParser`,
  `OptionArgumentReading`, `HelpCategories`, `AiHelpProse`, `AiHelpExamples`,
  `ExitCodeGuidanceTable`).
- `--aihelp` completeness (root `CLAUDE.md`): each new option needs its `OptionArgumentType` and
  appears on each of its categories' pages
  (`AiHelpFactsTests.EveryOption_HasAnArgumentTypeAndAllowedValues`,
  `AiHelpTextTests.Answer_EveryOption_AppearsInAllAndInEveryTopicItsCategoriesName`); an
  `AiHelpExamples` entry shows TLS-SRP and
  `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` runs it.
- Upstream test cases this makes reachable by hand (tag `curl-8_21_0`), `httptls+srp`: 320, 321,
  322, 324, from the Linux and macOS reference pins (the Windows pin has no `TLS-SRP`).

## Acceptance criteria

- [x] `Surl.Cli.UnitTests` has named tests for each new option, a missing user or password, an
      unreadable verifier file if the ADR has one, and TLS-SRP combined with an option the ADR
      refuses beside it, each returning its `SurlExitCode`.
- [x] `Surl.Console.UnitTests` has a named test that runs `CommandLineRunner` with TLS-SRP and
      completes one HTTPS request with a client in the test that drives `Surl.TlsSrp`'s handshake
      from recorded bytes, without `TestCategory=Integration`.
- [x] `surl --help`, `surl --manual` and `surl --aihelp all` describe the options, and the
      completeness tests named in Context pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and
      `Surl.Cli.UnitLibrary` keep 100% line and branch coverage.

## Notes

- Planning assumption (BL-399): the option names mirror curl's; BL-563's ADR decides them.
- BL-563 decided it in ADR-0122: options `--tlsauthtype`, `--tlsuser`, `--tlspassword`,
  `--srp-verifiers`, `--srp-groups` (decision 6); the TLS-SRP handshake plugs in through
  `Surl.Networking` for every TLS listener and `TlsSession` gains `SrpUserName` (decision 5), so
  BL-563 added `Foundation/Surl.Networking.*` and `Foundation/Surl.Protocol.Abstractions.*` to
  `touches` on 2026-11-05.
- BL-566 (2026-11-05, ADR-0122 Amendment 1): draw the startup salt again until its first byte is
  not zero, because OpenSSL's client hashes the salt without its leading zero bytes. The
  handshake is `new TlsSrpServer(new TlsSrpAccounts(entries, secret), RandomNumberGenerator.Create())
  .AuthenticateAsync(stream, ct)`, which returns a `TlsSrpSession` whose asynchronous-only
  `TlsSrpStream` carries the plaintext.

- 2026-11-11 (lane 2): ServerTlsComposition, which builds the process's TLS settings, now lives in
  `Application/Surl.Composition.UnitLibrary` (not Surl.Console as Context says), and TLS-SRP needs it to
  build no-certificate settings and the SRP accounts. Added Composition to `touches`; BL-1003 holds it in
  Doing, so this task goes back to Backlog until they no longer overlap. Surl.Api's
  `SurlServerHostSeams` may also need a touch.
- 2026-11-11 (lane 1, BL-567): delivered. Decisions recorded in ADR-0122 Amendment 2.
  - `Surl.Cli`: the five options (`TlsAuthenticationType` enum, `SurlCommandLine.ServesTlsSrp`),
    `TlsSrpCombination` for the refusals, help rows, a `tls` `--aihelp` paragraph pair, two
    `AiHelpExamples` entries and a TLS-SRP OPTIONS manual section.
  - `Surl.Networking`: `ServerTlsSettings.ForTlsSrp`; `ServerTlsHandshake` runs `TlsSrpServer` when
    the settings carry accounts; `StreamConnection` sends close_notify through
    `TlsSrpStream.ShutdownAsync`. `Surl.Protocol.Abstractions`: `TlsSession.SrpUserName`.
  - `Surl.Composition`: `TlsSrpAccountsComposition` and `TlsSrpAccountsException`;
    `ServerTlsComposition.IsTlsUpgradeAvailable` so STARTTLS servers offer the upgrade under TLS-SRP.
  - Added `Cryptography/Surl.TlsSrp.*` to `touches`: `TlsSrpStream` had no way to send close_notify
    without closing the connection, which the half-close and lingering close need
    (`TlsSrpStream.ShutdownAsync`). Added `Application/Surl.Mcp.*`: `CommandLinePathMembersTests`
    fails on an unclassified `SurlCommandLine` member, and `--srp-verifiers`/`--srp-groups` are
    paths its root check must confine. No task in Doing named either.
  - Default taken: TLS-SRP is refused beside `--cert`/`--self-signed` (and `--tlsv1.3`) until BL-911
    serves the signed suites, rather than silently ignoring the certificate. BL-1051 lifts it.
  - The unreadable-verifier-file criterion: the command line only keeps the paths (as for every
    file option), so the file refusals are tested where the files are read -
    `CommandLineRunnerTlsSrpTests.RunAsync_SrpFileMissing_WritesCouldNotReadAndReturnsCouldNotReadFile`,
    `..._SrpVerifierFileWithAMalformedLine_NamesTheFileAndLine` (37) and
    `ServerTlsCompositionTests.Compose_TlsSrp*` - while `CommandLineParserTlsSrpTests` covers each
    option, the partner refusals and the refused combinations (2).
  - The Console test replays upstream curl 8.21.0's recorded `CurlTlsSrpJsmithAbc` bytes (linked,
    not copied, from `Cryptography/Surl.TlsSrp.UnitTests/Fixtures`) through the accounts the command
    line composed from `--srp-verifiers`/`--srp-groups`, and decrypts the reply (`HTTP/1.1 404 Not Found`)
    from the SRP math's server side. `StreamConnectionTlsSrpTests` replays the same recordings through
    the real `Surl.Networking` seam.
  - Coverage: every member this task added or changed is at 100% line and branch with complexity of
    at most 10. `Measure-CodeQuality.ps1` still flags members this task did not touch:
    `Surl.Console` `Program.cs:51` (the production listener-factory lambda), `Surl.Cli`'s
    `ConfigurationItemReader.ReadHeadValue` branch and several pre-existing complexity rows, and
    `Surl.Composition`'s coaps certificate refusal in `ComposeServing`.
  - Follow-ups filed: BL-1051 (signed suites from surl, after BL-911), BL-1052 (the verbose notes).

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. touches Application/Surl.Composition.UnitLibrary (ServerTlsComposition), which BL-1003 holds in Doing; retry once BL-1003 is Done
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. surl serves HTTPS (and every TLS listener) over TLS-SRP from --tlsauthtype/--tlsuser/--tlspassword or --srp-verifiers/--srp-groups, and --aihelp documents them
