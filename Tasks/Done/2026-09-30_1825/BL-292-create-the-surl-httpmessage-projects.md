---
id: BL-292
title: Create the Surl.HttpMessage projects
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-281]
touches: [Surl.slnx, Surl.HttpMessage.UnitLibrary, Surl.HttpMessage.UnitTests, Surl.Protocol.Abstractions.UnitTests]
requirement: FR-048
created: 2026-09-30
completed: 2026-09-30
---
# BL-292 — Create the Surl.HttpMessage projects

## Goal

`Surl.HttpMessage.UnitLibrary` and `Surl.HttpMessage.UnitTests` exist, build, are listed in
`Surl.slnx`, and `ProtocolIsolationTests` knows the library's reference row, so BL-293 can move code
into it with no edit to `Surl.slnx`.

## Context

- Decision: BL-281's ADR (the library's name, contents and what it may reference -
  `Surl.Protocol.Abstractions` only, on the planning assumption). If that ADR named the library
  differently, use its name everywhere below; if it decided against a shared library, this task
  goes to `Deferred` with the ADR as the reason.
- Shape to copy: BL-189's change for `Surl.LineProtocol` (`Surl.LineProtocol.UnitLibrary/*.csproj`
  referencing Abstractions with `InternalsVisibleTo` its tests, the test csproj referencing MSTest
  through central package management with no `Version`, and a `CLAUDE.md` in the shape of
  `Surl.LineProtocol.UnitLibrary/CLAUDE.md`: the phase, what it holds as intent until BL-293 lands,
  what may reference it (`Surl.Protocol.Http`, `Surl.Protocol.Ws`, `Surl.Protocol.Rtsp`), that it
  never constructs a socket). `InternalsVisibleTo` alone will not do for three consumers: the types
  the servers use are `public`, as `Surl.LineProtocol`'s are.
- `Surl.slnx`: two `<Project>` lines in ordinal order in the flat run (between
  `Surl.Cryptography.UnitTests` and `Surl.Kerberos.TestKdc.UnitLibrary`), the `.UnitTests` directly
  after the library, no solution folder around them.
- `Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`: `HorizontalLibraries` gains the
  row, and the allowed and forbidden data rows gain it (it may reference Abstractions; it may not
  reference `Surl.Content`, a protocol server or `Surl.Networking`).
- No protocol server references it yet; BL-293, BL-301 and BL-313 add those references.

## Acceptance criteria

- [x] Both folders exist with their csproj files; the production csproj has no `PackageReference`
      and references exactly what the ADR allows.
- [x] `Surl.slnx` lists both projects in ordinal order, the tests directly after the library.
- [x] `Surl.HttpMessage.UnitLibrary/CLAUDE.md` exists.
- [x] `ProtocolIsolationTests` know the row and pass.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green.

## Notes

- ADR-0070 kept the planning name `Surl.HttpMessage` and its row (Abstractions only), so the
  projects follow it as filed.
- The production csproj copies `Surl.LineProtocol`: one `ProjectReference` to Abstractions and
  `InternalsVisibleTo` its tests; no code yet, so `CLAUDE.md` states the contents as intent until
  BL-293.
- The test project is empty for now, as `Surl.Protocol.Ws.UnitTests` and `Surl.Protocol.Rtsp.UnitTests`
  are; `dotnet test` accepts an empty project (exit 0).
- `ProtocolIsolationTests`: row `[HttpMessage] = [Abstractions]`, allowed as a protocol reference,
  `HttpMessage -> Abstractions` allowed, and `-> Content`, `-> LineProtocol`, `-> Protocol.Http`,
  `-> Networking` forbidden (ADR-0070 decision 1). 267 Abstractions tests pass.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.HttpMessage.UnitLibrary and .UnitTests exist, build, sit in Surl.slnx, and ProtocolIsolationTests hold their ADR-0070 row
