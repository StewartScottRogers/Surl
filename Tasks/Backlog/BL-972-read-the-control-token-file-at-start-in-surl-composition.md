---
id: BL-972
title: Read the --control-token-file at start in Surl.Composition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1022, BL-815]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-972 — Read the --control-token-file at start in Surl.Composition

## Goal

With `--control` given, the composition reads `--control-token-file` through the `ReadStartFile` seam, last of the
start files and before the data-directory lock and any listener: an unreadable file refuses the start with
`CouldNotReadFile` (37), a token outside 32 to 1024 visible ASCII bytes with `FailedInit` (2) without echoing it,
and an accepted token (the first line without its line end) is carried on `ComposedAuthentication.ControlToken`.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md)
  decision 7, "Options" and "Start": `(37) Could not read control token file <file>` and
  `(2) Control token file <file> must hold 32 to 1024 visible ASCII bytes on its first line`
  (bounds from `ControlToken.MinLength`/`MaxLength`).
- Built on: BL-815 (`SurlCommandLine.ControlListenUrl`, `.ControlTokenFile`; the parser already refuses
  `--control` without `--control-token-file`), BL-814 (`ControlToken`), BL-1022 (the `Surl.Control` reference).
- Split from BL-816 (timed out after 120 min, 2026-11-06). Reuse from the stash whose message is
  `darkfactory BL-816 20261106-033059` (commit `0b6c48207fcfb912c5b903ddc55ed2a06d93e6da`, called `S`; find its
  index with `git stash list`):
  - `Application/Surl.Composition.UnitLibrary/ControlComposition.cs` from `git show "S^3:<path>"`, keeping only
    `ReadToken` and `FirstLine` (Latin-1, so a byte past ASCII reaches `ControlToken` and is refused there);
    `MaxConnections`, `WriteWarning` and `FormatStatusLine` belong to the listener task that follows this one.
  - tracked, by `git diff "S^" "S" -- <file> | git apply` (unchanged on the shared branch since):
    `ComposedAuthentication.cs` (`ControlToken` init property) and `SurlServerComposition.Start.cs` (the
    `ReadToken` call after the SSH host keys).
  - From `Application/Surl.Composition.UnitTests/SurlServerCompositionTests.Control.cs` in `S^3`, the three
    `ComposeAsync_.../ServeAsync_AControlTokenFile...` token tests below; the served-endpoint tests come with the
    listener task.
- Stand-in: the token guards the control endpoint; no store, event or fault changes here, and the token is never
  written to a log or an error text.

## Acceptance criteria

- [ ] `ComposeAsync_AnUnreadableControlTokenFile_IsRefused37BeforeTheLockIsTaken` passes.
- [ ] `ComposeAsync_AControlTokenOutsideItsBounds_IsRefused2WithoutTheToken` passes for a too-short, a too-long and a
      non-ASCII token.
- [ ] `ServeAsync_AControlTokenFile_AcceptsItsFirstLineWithoutTheLineEndAsTheToken` (or a `ComposeAsync_` test of
      the same) passes for `\n` and `\r\n` line ends and a file with no line end.
- [ ] Without `--control`, no token file is read (a test whose `ReadStartFile` throws for any token path still starts).
- [ ] `dotnet build Application/Surl.Composition.UnitTests -warnaserror` is clean, the fast tests are green, and
      `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-816 (timed out after 120 min); reuses its token reading from stash `darkfactory BL-816 20261106-033059`.
