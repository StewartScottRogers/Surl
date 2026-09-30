---
id: BL-149
title: Create the Curve25519, Ed25519, ChaCha20 and Poly1305 primitive projects
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-148]
touches: [Surl.slnx, Surl.Cryptography.Curve25519.UnitLibrary, Surl.Cryptography.Curve25519.UnitTests, Surl.Cryptography.Ed25519.UnitLibrary, Surl.Cryptography.Ed25519.UnitTests, Surl.Cryptography.ChaCha20.UnitLibrary, Surl.Cryptography.ChaCha20.UnitTests, Surl.Cryptography.Poly1305.UnitLibrary, Surl.Cryptography.Poly1305.UnitTests, Surl.Protocol.Abstractions.UnitTests, Surl.Cryptography.UnitLibrary/CLAUDE.md]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-29
---
# BL-149 — Create the Curve25519, Ed25519, ChaCha20 and Poly1305 primitive projects

## Goal

The eight projects BL-148's ADR names exist, build, are listed in `Surl.slnx`, and
`ProtocolIsolationTests` knows their reference rows, so BL-150 to BL-153 can each start in
their own lane with no edit to `Surl.slnx`.

## Context

- Decision: BL-148's ADR (the table of four libraries and what each may reference).
- Projects: `Surl.Cryptography.ChaCha20.UnitLibrary`, `Surl.Cryptography.Curve25519.UnitLibrary`,
  `Surl.Cryptography.Ed25519.UnitLibrary`, `Surl.Cryptography.Poly1305.UnitLibrary`, each with
  its `.UnitTests` twin, each a folder at the repository root (root `CLAUDE.md`, "Repository
  layout").
- Shape to copy: `Surl.Cryptography.UnitLibrary/Surl.Cryptography.UnitLibrary.csproj` (only
  `InternalsVisibleTo` its tests; `Directory.Build.props` supplies the framework, nullable, AOT
  and analyzers) and `Surl.Cryptography.UnitTests/Surl.Cryptography.UnitTests.csproj` (the
  `MSTest` package with no `Version`, the global `Using`, the one `ProjectReference`).
  `Surl.Cryptography.Ed25519.UnitLibrary` also references
  `Surl.Cryptography.Curve25519.UnitLibrary`; the other three reference nothing.
- Each production project gets a `CLAUDE.md` in the shape of
  `Surl.Cryptography.UnitLibrary/CLAUDE.md`: phase, what it holds (as intent until its task
  lands), what it may reference, bytes in and bytes out (no socket, no file, no clock),
  vectors cited beside each test, copying from the Curl port allowed as code only (ADR-0003).
- `Surl.Cryptography.UnitLibrary/CLAUDE.md` is corrected so it no longer says the SSH
  primitives land there, and points to the four libraries.
- `Surl.slnx`: the eight `<Project>` lines in ordinal order within the flat run, each
  `.UnitTests` directly after its library: ChaCha20, Curve25519, Ed25519, Poly1305, all
  before `Surl.Cryptography.UnitLibrary` in ordinal order.
- `Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`: `HorizontalLibraries`
  gains the four rows the ADR states, and the `ForbiddenProtocolReferences_AllowedLibrary_IsNotForbidden`
  data rows gain them.
- An empty test project is fine; the Phase 0 shell had many (ADR-0002, "Consequences").

## Acceptance criteria

- [x] The eight folders exist with their csproj files; no production csproj has a
      `PackageReference`, and the references are exactly the ADR's.
- [x] `Surl.slnx` lists the eight projects in ordinal order, each `.UnitTests` directly after
      its library, with no solution folder around them.
- [x] Each of the four production projects has a `CLAUDE.md`, and
      `Surl.Cryptography.UnitLibrary/CLAUDE.md` no longer claims the SSH primitives.
- [x] `ProtocolIsolationTests` knows the four rows and passes, including
      `EveryHorizontalLibrary_ReferencesOnlyItsRow`.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"`
      is green.

## Notes

- Built with `dotnet build -warnaserror` (0 warnings) and the fast tests (all green); the four empty test projects report "No test matches", which `dotnet test` accepts with exit 0, as for the other Phase 0 shells (ADR-0002).
- `Surl.Cryptography.UnitLibrary/CLAUDE.md` now says Phase 1 (its `Sha512Slash256` and `Md4` serve FR-014, a Phase 1 requirement) and points the SSH primitives to the four libraries (ADR-0048 decision 5).
- `ProtocolIsolationTests` gained the four rows, their allowed data rows, `Ed25519 -> Curve25519` as an in-row reference, and four out-of-row references (Curve25519 -> Ed25519, ChaCha20 <-> Poly1305, Ed25519 -> Cryptography).
- `dotnet format --verify-no-changes` reports end-of-line errors only in files outside this task (e.g. `Surl.Output.UnitTests/VerboseLogEscapingTests.cs`); none in the files this task changed.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The four SSH primitive libraries and their test twins exist, build, are in Surl.slnx and are guarded by ProtocolIsolationTests
