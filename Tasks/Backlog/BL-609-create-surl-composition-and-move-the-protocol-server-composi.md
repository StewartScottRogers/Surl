---
id: BL-609
title: Create Surl.Composition and move the protocol server composition out of Surl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-611]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Surl.slnx, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-04
completed:
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

- [ ] `Application/Surl.Composition.UnitLibrary` and `Application/Surl.Composition.UnitTests` exist, are in
      `Surl.slnx`'s `/Application/` folder in ordinal order, and `Surl.Console` references the library.
- [ ] `ComposeProtocolServers` and `ExtendedProtocolServers.Compose` are in the library under those names, and no
      `Surl.Console` source file constructs a protocol server.
- [ ] Every test that existed in `Surl.Console.UnitTests` before the move still exists, in that project or moved
      to `Surl.Composition.UnitTests` under the same method name, and passes; `CommandLineRunnerAiHelpTests`'
      three tests named in root `CLAUDE.md` pass.
- [ ] `ProtocolIsolationTests` passes.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Composition.UnitLibrary` and `Surl.Console` have 100% line and branch coverage.
- [ ] `Application/Surl.Composition.UnitLibrary/CLAUDE.md` exists and says what the library composes and who
      references it; `Application/Surl.Console/CLAUDE.md` says that `ComposeProtocolServers` and
      `ExtendedProtocolServers.Compose` now live in `Surl.Composition`; `Documentation/Wiki/Glossary.md` names
      `Surl.Composition` wherever it named `Surl.Console` for a moved type (`ImplicitTlsSchemeServer`, the mail
      store's loading).

## Notes

## Log

- 2026-11-04: Created.
