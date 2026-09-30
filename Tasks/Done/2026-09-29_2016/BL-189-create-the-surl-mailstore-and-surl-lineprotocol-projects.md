---
id: BL-189
title: Create the Surl.MailStore and Surl.LineProtocol projects
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-184]
touches: [Surl.slnx, Surl.MailStore.UnitLibrary, Surl.MailStore.UnitTests, Surl.LineProtocol.UnitLibrary, Surl.LineProtocol.UnitTests, Surl.Protocol.Abstractions.UnitTests]
requirement: FR-047
created: 2026-09-29
completed: 2026-09-29
---
# BL-189 — Create the Surl.MailStore and Surl.LineProtocol projects

## Goal

`Surl.MailStore.UnitLibrary`, `Surl.LineProtocol.UnitLibrary` and their `.UnitTests` twins
exist, build, are listed in `Surl.slnx`, and `ProtocolIsolationTests` knows their reference rows,
so BL-190 and BL-192 can start in their own lanes with no edit to `Surl.slnx`.

## Context

- Decision: BL-184's ADR (what each library holds and may reference:
  `Surl.MailStore` -> Abstractions and `Surl.Content`; `Surl.LineProtocol` -> Abstractions).
- Shape to copy: `Surl.Content.UnitLibrary/Surl.Content.UnitLibrary.csproj` (a horizontal library
  referencing Abstractions, `InternalsVisibleTo` its tests) and
  `Surl.Content.UnitTests/Surl.Content.UnitTests.csproj`; `Directory.Build.props` supplies the
  rest. Each production project gets a `CLAUDE.md` in the shape of
  `Surl.Content.UnitLibrary/CLAUDE.md`: phase, what it holds (as intent until BL-190 and BL-192
  land), what it may reference, that it never touches the disk directly (only
  `IContentFileSystem`) or constructs a socket.
- `Surl.slnx`: the four `<Project>` lines in ordinal order in the flat run
  (`Surl.LineProtocol` between `Surl.Core` and `Surl.Networking`, `Surl.MailStore` after it), each
  `.UnitTests` directly after its library.
- `Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`: `HorizontalLibraries` gains
  the two rows, and the allowed-library data rows gain them.
- The protocol servers' `ProjectReference`s to these libraries are added by BL-198, BL-201 and
  BL-205, not here.

## Acceptance criteria

- [x] The four folders exist with their csproj files; no production csproj has a
      `PackageReference`; the references are exactly the ADR's.
- [x] `Surl.slnx` lists the four projects in ordinal order, each `.UnitTests` directly after its
      library, with no solution folder around them.
- [x] Each production project has a `CLAUDE.md`.
- [x] `ProtocolIsolationTests` know the two rows and pass.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"`
      is green.

## Notes

- 2026-09-29: `Surl.slnx` places the four projects after `Surl.Cryptography.UnitTests`, not directly after `Surl.Core`: ordinal order puts every `Surl.Cryptography.*` between `Surl.Core` and `Surl.LineProtocol`, and ordinal order is the rule.
- 2026-09-29: The libraries hold no code yet and the test projects no tests, as the Smtp, Imap and Pop3 scaffolds do; `dotnet test` reports "No test matches" for them and exits 0. Their `CLAUDE.md` files state the contents as intent until BL-190, BL-191 and BL-192 land.
- 2026-09-29: `ProtocolIsolationTests` gains the two rows plus in-row and out-of-row data rows (LineProtocol -> Content or MailStore, MailStore -> LineProtocol or Networking are forbidden). `dotnet format --verify-no-changes` reports end-of-line markers in `Surl.Cli.UnitLibrary/SchemeDefaultPorts.cs`, outside this task and pre-existing; none in the files this task changed.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Surl.LineProtocol and Surl.MailStore projects and their test twins build, sit in Surl.slnx, and ProtocolIsolationTests guard their ADR-0050 rows
