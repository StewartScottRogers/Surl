---
id: BL-611
title: Decide the shared server composition and Surl.Api's programming interface
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-610]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed:
---
# BL-611 — Decide the shared server composition and Surl.Api's programming interface

## Goal

An ADR decides how Surl's protocol servers are composed once for both `surl` and `Surl.Api`, and what `Surl.Api`'s
public surface is: how a .NET test suite starts a server in-process, reads its bound address, configures it the way
the command line does, sees what a client sent, and stops it. The tasks that build it then have one design to follow.

## Context

- BL-610's ADR records Stewart's decision that `Surl.Api` exists (purpose, `Application/` folder, same
  secure-by-default rules). This ADR decides the design and is marked "Decided by Claude under Stewart's delegation".
- Where composition lives today, all in `Application/Surl.Console` (read `Application/Surl.Console/CLAUDE.md`):
  - `CommandLineRunner.cs`: `ComposeProtocolServers` (every original server, the `ImplicitTlsSchemeServer`
    second registrations), `ComposeUnservedProtocolServers`/`ComposeRegisteredSchemes` (the schemes `--version` and
    the `--aihelp` completeness tests read), the ordered start checks (`FindOptionRefusal`, `DataDirectoryProbe`,
    `FindListenUrlRefusal`, `ComposeAuthentication`, the data-directory lock, `LoadServiceStateAsync` for MQTT
    retained messages, the mail store and the LDAP directory, `ServerTlsComposition.Compose`, `LogStreams`), and
    the `ServingEngine` built in `ServeLoggedAsync`.
  - `AuthenticationComposition`, `KeytabComposition`, `SshHostKeyComposition`, `SshAlgorithmComposition`,
    `ServerTlsComposition`, `ImplicitTlsSchemeServer`, `ExtendedProtocolServers`/`ExtendedProtocolServerServices`
    (ADR-0083 decisions 6 and 7, `SURL_EXTENSIONS`), `DataDirectoryLock`, `DataDirectoryProbe`,
    `RandomKerberosRandomSource`, `IEnvironmentVariableReader`, `ListenerStartReporter` (which already sees each
    listener's bound `ListenUrl`; `IListenerFactory`'s listeners carry the bound port).
  - Every byte a server reads or writes reaches `IExchangeLog` through `Surl.Core`'s `RecordingConnection`
    (ADR-0004 section 5); an `IExchangeLogFactory` is how a host could keep what a client sent.
- The port's design, copyable but never evidence (ADR-0003): `git -C Z:\repos\Curl show origin/master:<path>` for
  `Curl.Api/CLAUDE.md`, `Curl.Api/Curl.Api.csproj` (packs one package bundling its project references) and
  `Documentation/Planning/Decisions/ADR-0506-libcurl-and-curl-api-over-one-transfer-engine.md`. Surl's ADR gives its
  own reasons.
- Constraints: base class library only; every production project AOT-compatible (`Directory.Build.props`), so no
  reflection-based DI; the four quality gates; MSTest only; protocol servers never reference a host
  (`Foundation/Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`); tests pass on Windows, Linux and
  macOS; `TimeProvider` injected.
- Publishing a NuGet package to nuget.org is a release action that needs Stewart (root `CLAUDE.md`, "Git and
  GitHub"). This ADR may decide that `Surl.Api` is packable and dry-run packed; it must not plan the push.

Decide and record:

1. **The shared composition.** Planning assumption: a new `Application/Surl.Composition.UnitLibrary` (namespace
   `Surl.Composition`) with `Application/Surl.Composition.UnitTests`, referenced by `Surl.Console` and `Surl.Api`. Say
   exactly which of the types and methods above move, which stay in `Surl.Console` (parsing, help, `--aihelp`,
   `--manual`, `--version`, `LogStreams` and writing `surl: ` messages and exit codes are the expected stay-behinds),
   and that `ComposeProtocolServers` and `ExtendedProtocolServers.Compose` keep their names, so the 26 Backlog
   tasks that name them still find them.
2. **The composition's input and output.** Planning assumption: input is `Surl.Cli`'s `SurlCommandLine` (so the API
   and the command line validate identically), plus the seams `CommandLineRunner` takes today; output is either a
   start failure (`SurlExitCode` and the message text `surl` writes after `surl: `) or a composed serving whose
   `ServeAsync` runs the `ServingEngine` and which reports the bound `ListenUrl`s once every listener has bound.
3. **`Surl.Api`'s project.** Planning assumption: `Application/Surl.Api` (a naming exception like `Surl.Console`,
   because it is the product's name, as `Curl.Api` is the port's), namespace `Surl.Api`, tests in
   `Application/Surl.Api.UnitTests`; no `Surl.Api.UnitLibrary`, because `Surl.Composition` is the shared core.
4. **The public surface.** Planning assumption: a `SurlServer` (`IAsyncDisposable`) started by
   `SurlServer.StartAsync(SurlServerOptions, CancellationToken)`, returning once every listener has bound, with
   `BoundUrls` (each listen URL with its bound port) and `StopAsync`; `SurlServerOptions` holds the listen URLs and
   typed properties for the options a test needs - content (a data directory or in-memory files the caller
   supplies), accounts and `--auth` methods, certificates, connection and size limits, the loosening switches - each
   mapped onto a `SurlCommandLine`. Decide whether a `string[]` overload taking `surl`'s own arguments exists too.
   Name every public type and member.
5. **Failures.** Planning assumption: one exception type (planning `SurlServerStartException`) carrying the
   `SurlExitCode` and the same text `surl` writes, for every refusal `surl` refuses with before serving; argument
   misuse stays `ArgumentException`.
6. **Secure by default.** The API's defaults equal the command line's (ADR-0032, ADR-0006): no anonymous login, no
   plaintext auth, no uploads, the same limits, loosened only by properties named after the command-line options.
   Where the start-up warnings go (planning: an optional `TextWriter` log, silent by default; ADR-0033's levels).
7. **What a client sent.** Planning assumption: an opt-in, bounded, in-memory record of each exchange (listen URL,
   peer, start time, bytes received and sent, in order) kept through an `IExchangeLogFactory`; state the bounds
   (exchanges kept, bytes per exchange) and what happens past them.
8. **Process-wide state.** Several `SurlServer`s in one process; the data-directory lock (ADR-0031 decision 7)
   still refuses two on one path; `SURL_EXTENSIONS` read from the process unless the options say otherwise;
   `TimeProvider` injectable.
9. **Agent-facing help for `Surl.Api`.** Whether its XML documentation and a package `README.md` are enough, and
   whether `surl --aihelp`'s overview points at `Surl.Api` (it documents the command line, ADR-0046).
10. **Packaging.** Whether `Surl.Api` is packed as one NuGet package bundling its project references (as the port's
    `Curl.Api` is), its package ID and version, and that the release workflow only dry-run packs it until Stewart
    approves a push.
11. **The reference graph.** `Surl.Composition`, `Surl.Api` and the `Surl.Mcp` projects are composition roots:
    no protocol server or horizontal library may reference them, and `ProtocolIsolationTests` says so.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by Claude
      under Stewart's delegation", citing BL-610's ADR. It answers decisions 1 to 11 above, names every project,
      folder, public type and member it decides, and lists the follow-up tasks named in this task's Notes.
- [ ] The ADR is listed in `Documentation/Planning/Decisions/README.md`'s index.
- [ ] Where the ADR departs from a planning assumption above, its Consequences section names each follow-up task
      whose Goal or touches it changes.

## Notes

Follow-up tasks filed with this one (2026-11-04), which the ADR lists: BL-609 (create `Surl.Composition`, move
the server composition), BL-613 (move the authentication, host key and TLS composition), BL-614 (the start sequence
and bound URLs), BL-615 (create `Surl.Api`, start and stop), BL-616 (accounts, auth, TLS and limits options),
BL-617 (the exchange record), BL-618 (proof against pinned upstream curl), BL-628 (`surl --aihelp`'s pointer),
BL-643 (NuGet packing, dry run only) and BL-649 (documents). BL-641, filed by another planning run, decides `surl`'s
configuration surface (the `-K` config file and one configuration model) after this ADR; keep decision 4's options
mappable onto a `SurlCommandLine` so they stay compatible with whatever it decides.

## Log

- 2026-11-04: Created.
