---
id: BL-609
title: Create Surl.Composition and move the protocol server composition out of Surl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-611]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Surl.slnx, Documentation/Wiki/Glossary.md, Foundation/Surl.Protocol.Abstractions.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Ws.UnitLibrary, Documentation/Product/Product-Overview.md, CLAUDE.md]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-609 — Create Surl.Composition and move the protocol server composition out of Surl.Console

## Goal

`Application/Surl.Composition.UnitLibrary` exists and builds every protocol server `surl` registers - the original
servers, their `ImplicitTlsSchemeServer` second registrations and the extended servers behind `SURL_EXTENSIONS` -
together with the service state they keep across connections. `Surl.Console` calls it instead of composing them
itself, and `surl`'s behaviour and output are unchanged byte for byte.

## Context

- Decision: BL-611's ADR, decisions 1, 2 and 11 (the library's name, folder and namespace, which types move, the
  reference rules). If the ADR names the library differently, use its name and record the difference in Notes.
- What moves, from `Application/Surl.Console` (the ADR's list is authoritative; this is the planning list):
  - from `CommandLineRunner.cs`: `ComposeProtocolServers`, `ComposeUnservedProtocolServers`,
    `ComposeRegisteredSchemes`, the `ServiceState` record and the loading behind it (`LoadServiceStateAsync`,
    `LoadRetainedMessagesAsync`, `LoadMailStoreAsync`, `LoadLdapServerAsync`, `ComposeRetainedMessageFile`,
    `ComposeMailStoreFiles`, `ComposeLdapDirectoryFile`);
  - `ImplicitTlsSchemeServer.cs`, `ExtendedProtocolServers.cs`, `ExtendedProtocolServerServices.cs`,
    `IEnvironmentVariableReader.cs`, `ProcessEnvironmentVariableReader.cs`;
  - their tests from `Application/Surl.Console.UnitTests` (`ImplicitTlsSchemeServerTests`,
    `ExtendedProtocolServersTests`, `ProcessEnvironmentVariableReaderTests`, and the fakes they need) into
    `Application/Surl.Composition.UnitTests`.
- Keep the names `ComposeProtocolServers` and `ExtendedProtocolServers.Compose`: 26 Backlog tasks (BL-383, BL-384,
  BL-421 and the other "Serve ... from the surl executable" tasks) tell their lane to register a server there.
- The protocol server and service project references move from `Surl.Console.csproj` to the new library's csproj;
  `Surl.Console` references the library. The new projects go in `Surl.slnx`'s `/Application/` folder in ordinal
  order, and `Directory.Build.props` makes the library AOT-compatible with no csproj setting.
- `--aihelp` completeness (root `CLAUDE.md`, "`surl --aihelp`"; ADR-0046 decision 9): the tests in
  `Application/Surl.Console.UnitTests/CommandLineRunnerAiHelpTests.cs` read the registered schemes and must keep
  passing under the same names. They may call the library's scheme listing instead of the runner's.
- `Application/Surl.Console/CLAUDE.md` describes all of this as living in `Surl.Console` today.
- Code style: `.claude/rules/csharp-style.md` (XML documentation on every public member of a library) and
  `.claude/rules/testing.md`.

## Acceptance criteria

- [x] `Application/Surl.Composition.UnitLibrary` and `Application/Surl.Composition.UnitTests` exist, are in
      `Surl.slnx`'s `/Application/` folder in ordinal order, and `Surl.Console` references the library.
- [x] `ComposeProtocolServers` and `ExtendedProtocolServers.Compose` are in the library under those names, and no
      `Surl.Console` source file constructs a protocol server.
- [x] Every test that existed in `Surl.Console.UnitTests` before the move still exists, in that project or moved
      to `Surl.Composition.UnitTests` under the same method name, and passes; `CommandLineRunnerAiHelpTests`'
      three tests named in root `CLAUDE.md` pass.
- [x] `ProtocolIsolationTests` passes.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Composition.UnitLibrary` and `Surl.Console` have 100% line and branch coverage.
- [x] `Application/Surl.Composition.UnitLibrary/CLAUDE.md` exists and says what the library composes and who
      references it; `Application/Surl.Console/CLAUDE.md` says that `ComposeProtocolServers` and
      `ExtendedProtocolServers.Compose` now live in `Surl.Composition`; `Documentation/Wiki/Glossary.md` names
      `Surl.Composition` wherever it named `Surl.Console` for a moved type (`ImplicitTlsSchemeServer`, the mail
      store's loading).

## Notes

- Done as ADR-0099 decisions 1 and 11 say. The library is `Surl.Composition.UnitLibrary`, namespace `Surl.Composition`.
  The moved composition is one public static class, `SurlServerComposition` (the name ADR-0099 decision 2 gives the
  scheme listing). It holds `ComposeProtocolServers`, `ComposeUnservedProtocolServers`, `ComposeRegisteredSchemes`,
  `ComposeExtendedSchemes`, `ComposeExtendedProtocolServers`, `ComposeContentStore`, `ComposeContentFileSystem`,
  `ComposeConnectionLimits`, `LoadServiceStateAsync` and the retained-message, mail-store and LDAP loaders.
  `ServiceState` is now a public record. `ImplicitTlsSchemeServer`, `ExtendedProtocolServers`,
  `ExtendedProtocolServerServices`, `IEnvironmentVariableReader` and `ProcessEnvironmentVariableReader` moved
  unchanged except that they are now `public`.
- Choice: `SurlServerComposition.ComposeExtendedSchemes` and `ComposeExtendedProtocolServers` take the switch and
  the composer seam as arguments. This is because BL-614 moves the reading of `SURL_EXTENSIONS` to the host, and
  until then `CommandLineRunner` still reads it. `ComposeRegisteredSchemes()` and `ComposeExtendedSchemes()` stay on
  `CommandLineRunner` as one-line delegations, so `CommandLineRunnerAiHelpTests` and the extended-protocol tests
  keep their names and calls.
- Choice: the servers built only to list their schemes now use `SurlServerComposition.ComposePolicyWithoutAccounts`.
  It is a policy with no accounts and no HTTP method. They no longer use `AuthenticationComposition.ComposeWithoutAccounts`,
  because that type stays in `Surl.Console` until BL-613 and the library may not reference a host. Nothing is
  served through those servers, so the schemes they list are the same (`ComposeRegisteredSchemes_ThisBuild_...`
  pins the list). `AuthenticationComposition.ComposeWithoutAccounts` stays for BL-613, because an LDAP test uses it.
- Tests that called `CommandLineRunner.ComposeX` and `LoadXAsync` now call `SurlServerComposition` and stay in
  `Surl.Console.UnitTests`, under the same method names. `Measure-CodeQuality.ps1` merges coverage from every test
  project, so they still count toward the library's coverage. `FakeEnvironmentVariableReader` is copied into
  `Surl.Composition.UnitTests`, because `Surl.Console.UnitTests` still uses its own copy.
- Coverage (`Measure-CodeQuality.ps1 -Library Surl.Composition.UnitLibrary,Surl.Console`):
  `Surl.Composition.UnitLibrary` 100/100 (worst CRAP 6), `Surl.Console` 100/100 (worst CRAP 10). One measuring run
  had a single failing test elsewhere in the solution. Two reruns, and every fast run, passed, so that failure
  was flaky (memory: the KEXINIT test).
- Touches widened, and no task in Doing named any of these:
  - `Foundation/Surl.Protocol.Abstractions.UnitTests`, for ADR-0099 decision 11's reference-graph tests
    (`EveryProjectOutsideTheHostsAndConformance_ReferencesNoCompositionRoot`, `Composition_ReferencesNoHost`, the
    `Surl.Composition.UnitLibrary` and `Surl.Api` rows), which ADR-0099 assigns to BL-609.
  - `Protocols/Http/Surl.Protocol.Http.UnitLibrary` (a doc comment), `Protocols/Http/Surl.Protocol.Ws.UnitLibrary/CLAUDE.md`,
    `Documentation/Product/Product-Overview.md` and root `CLAUDE.md`, so that they no longer name `Surl.Console` as
    where `ComposeProtocolServers` and `ImplicitTlsSchemeServer` live.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.Composition composes every protocol server surl registers; Surl.Console calls it; reference-graph tests added
