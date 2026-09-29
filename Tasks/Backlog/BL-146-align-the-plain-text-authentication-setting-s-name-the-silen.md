---
id: BL-146
title: Align the plain-text authentication setting's name, the silent exchange log's doc comment and ADR-0028's factory name
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-145]
touches: [Surl.Output.UnitLibrary, Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Console, Surl.Console.UnitTests, Documentation/Wiki, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed:
---
# BL-146 — Align the plain-text authentication setting's name, the silent exchange log's doc comment and ADR-0028's factory name

## Goal

One concept, one name: the `--allow-plaintext-auth` flag is `AllowPlaintextAuthentication` on both
`SurlCommandLine` and `AuthenticationSettings`, `SilentExchangeLog`'s summary names the log level it
serves, and ADR-0028 tells a reader that the factory it names is now `LevelledExchangeLogFactory`.
No behaviour changes.

## Context

Found during BL-124. Three misalignments, which the solution-wide rule "Say what it does, do what it
says" counts as defects:

1. `Surl.Output.UnitLibrary/SilentExchangeLog.cs`, the class `<summary>`: "The exchange log without
   `-v`: every call writes nothing." Since ADR-0033 and BL-126,
   `Surl.Output.UnitLibrary/LevelledExchangeLogFactory.cs` hands out `SilentExchangeLog.Instance`
   only when the level is `LogLevel.None` (about line 59). Without `-v` the level is `info` or
   whatever `--log-level` names, and exchange notes are still written there, so "without -v" is
   false.
2. One concept, two names. `Surl.Cli.UnitLibrary/SurlCommandLine.cs` has
   `AllowPlaintextAuthentication` (about line 164); `Surl.Authentication.UnitLibrary/
   AuthenticationSettings.cs` has the positional parameter `AllowPlaintextAuth` (line 16, and its
   `<param>` on line 9). `Documentation/Wiki/Glossary.md`, row "loosening option", names
   `SurlCommandLine.AllowPlaintextAuthentication`, so that is the glossary name: rename
   `AuthenticationSettings.AllowPlaintextAuth` to `AllowPlaintextAuthentication`. Uses to update:
   `Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs` (`settings.AllowPlaintextAuth`, about
   line 82) and `Surl.Console.UnitTests/CommandLineRunnerAuthenticationTests.cs` (about lines 136 and
   147); `Surl.Console/AuthenticationComposition.cs` builds the record positionally, so check it
   compiles and change it only if it names the parameter. Test method names such as
   `..._AllowPlaintextAuth_...` in `Surl.Authentication.UnitTests` and `Surl.Conformance.UnitTests`
   name the `--allow-plaintext-auth` option, not the property, and may stay; the private constant
   `AllowPlaintextAuthExplanation` in `Surl.Cli.UnitLibrary/CommandLineOptions.cs` and
   `AllowPlaintextAuthExplanationLines` in `Surl.Cli.UnitTests/HelpTextTests.cs` likewise name the
   option's help paragraph and may stay. `Surl.Cli.*` is in `touches` only in case the glossary row
   needs the Cli side re-checked; the rename itself does not change it.
3. `Documentation/Planning/Decisions/ADR-0028-a-connection-refused-past-a-limit-is-noted-outside-any-exchange.md`,
   Decision item 2 (line 27), names `Surl.Output`'s `VerboseExchangeLogFactory`, which BL-126 removed
   (`Tasks/Done/2026-09-29_1228/BL-126-remove-the-unused-verboseexchangelogfactory-from-surl-output.md`);
   `LevelledExchangeLogFactory.NoteOutsideExchange` now writes the `#- * <text>` line. An ADR's
   decision is history, so do not rewrite it: add a dated note. ADR-0033 line 26 also names
   `VerboseExchangeLogFactory`, but in its Context, describing the code as it was then; leave it.

This is a docs task: a rename and doc comments, no behaviour change. BL-145 changes
`Surl.Cli.UnitLibrary` first, so this task waits on it.

## Acceptance criteria

- [ ] The `<summary>` of `SilentExchangeLog` in `Surl.Output.UnitLibrary/SilentExchangeLog.cs` no
      longer says "without -v"; it says it is the exchange log at the `none` log level
      (`LogLevel.None`), where every call writes nothing.
- [ ] `grep -rnw "AllowPlaintextAuth" --include=*.cs .` (whole word, outside `bin/` and `obj/`)
      finds nothing: `AuthenticationSettings`'s parameter and its `<param>` are
      `AllowPlaintextAuthentication`, and `AuthenticationPolicy.cs` and
      `CommandLineRunnerAuthenticationTests.cs` use that name.
- [ ] `Documentation/Wiki/Glossary.md` names the property the same way wherever it names it
      (`SurlCommandLine.AllowPlaintextAuthentication`, and `AuthenticationSettings.AllowPlaintextAuthentication`
      if a row names the settings), and no row names `AllowPlaintextAuth` as a member.
- [ ] ADR-0028's Decision item 2 is unchanged, and the ADR carries a note dated 2026-09-29 (or the
      day the task runs) saying `VerboseExchangeLogFactory` was removed in BL-126 and the
      `#- * <text>` line is now written by `LevelledExchangeLogFactory.NoteOutsideExchange`.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"`
      passes; no test is added or removed, and no new test needs `TestCategory=Integration`.

## Notes

## Log

- 2026-09-29: Created.
