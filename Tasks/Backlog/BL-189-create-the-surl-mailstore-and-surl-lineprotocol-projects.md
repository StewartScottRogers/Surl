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
completed:
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

- [ ] The four folders exist with their csproj files; no production csproj has a
      `PackageReference`; the references are exactly the ADR's.
- [ ] `Surl.slnx` lists the four projects in ordinal order, each `.UnitTests` directly after its
      library, with no solution folder around them.
- [ ] Each production project has a `CLAUDE.md`.
- [ ] `ProtocolIsolationTests` know the two rows and pass.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"`
      is green.

## Notes

## Log

- 2026-09-29: Created.
