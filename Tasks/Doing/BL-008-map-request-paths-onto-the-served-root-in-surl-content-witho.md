---
id: BL-008
title: Map request paths onto the served root in Surl.Content without escaping it
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-008 — Map request paths onto the served root in Surl.Content without escaping it

## Goal

`Surl.Content.UnitLibrary` maps a percent-encoded request path onto a location inside the
served root through an injected file-system seam. Every escape attempt is refused with a
result, not an exception, and a test proves each refusal.

## Context

- ADR-0002, decision 2: `Surl.Content` is the content store. It maps a request path onto
  the served directory without escaping it, through an injected file-system seam, so its
  tests need no disk.
- `Surl.Content.UnitLibrary/CLAUDE.md` gives the escapes that must each have a
  refusing test: `..`, its percent-encoded forms, absolute paths, drive letters, UNC
  paths, and symbolic links that point out.
- Upstream curl removes dot segments from a URL path before sending, unless given
  `--path-as-is` (https://curl.se/docs/manpage.html, checked 2026-09-28; the page then
  documented 8.23.0). So raw `..` reaches Surl only from `--path-as-is` or percent
  encoding, and both must be handled. The HTTP tasks measure the exact bytes. This task
  works on path strings.
- This task defines the file-system seam, an interface in `Surl.Content.UnitLibrary`
  named for what it does. It has the members this task and BL-009 need: at least "what
  is at this path (file, directory, nothing)" and "where does this path finally resolve,
  following symbolic links". BL-010 implements it over `System.IO`. Tests use a
  hand-written in-memory fake. No mocking library is permitted.
- Surl.Content references only `Surl.Protocol.Abstractions.UnitLibrary` (ADR-0002;
  enforced by `ProtocolIsolationTests.EveryHorizontalLibrary_ReferencesOnlyItsRow`).
- Tests pass on Windows, Linux and macOS (root `CLAUDE.md`). Drive-letter and UNC cases
  are refused on every platform, and the tests assert that without a Windows-only path
  outside an `[OSCondition(OperatingSystems.Windows)]` test.

## Acceptance criteria

- [ ] A content-store type in `Surl.Content.UnitLibrary`, constructed with the served
      root and the file-system seam, maps a request path to either a location inside the
      root or a refusal carrying a reason.
- [ ] Fast tests prove a refusal, and that the seam is never asked to open anything, for
      each of: `/../x`, `/a/../../x`, `/%2e%2e/x`, `/%2E%2E/x`, `/.%2e/x`, `/..%2fx`,
      `/..%2Fx`, `/..%5cx`, `/..\x`, `//server/share/x`, `/\\server\share\x`,
      `/C:/x`, `/C:%5cx`, `/%00x`, an invalid percent escape such as `/%zz`, and a path
      whose final symbolic-link target is outside the root.
- [ ] Fast tests prove a successful mapping for `/`, `/file.txt`, `/dir/file.txt`,
      `/with%20space.txt`, a UTF-8 percent-encoded name such as `/caf%C3%A9.txt`, and a
      symbolic link whose final target stays inside the root.
- [ ] Whether a `..` that stays inside the root (`/a/../b`) is served or refused is
      decided in the `/feature` plan, stated in the content-store type's XML doc, and
      pinned by a test.
- [ ] `dotnet build Surl.Content.UnitLibrary -warnaserror` is clean, and
      `dotnet test --filter "TestCategory!=Integration"` is green with no test in
      `Surl.Content.UnitTests` tagged `Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Content.UnitLibrary`.

## Notes

Directory listing, media types and upload placement are later tasks. Keep this one to
path mapping and the seam.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
