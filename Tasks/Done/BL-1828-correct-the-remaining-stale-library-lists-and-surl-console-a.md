---
id: BL-1828
title: Correct the remaining stale library lists and Surl.Console attributions in the Surl.Cli and Surl.Composition CLAUDE.md files
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary/CLAUDE.md, Application/Surl.Composition.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed: 2026-11-27
---
# BL-1828 — Correct the remaining stale library lists and Surl.Console attributions in the Surl.Cli and Surl.Composition CLAUDE.md files

## Goal

The reference lists, the attributions of file reading and checking, the TLS-SRP listen URL rule and the warning order in the two `CLAUDE.md` files match the code.

## Context

Found while working BL-1820. Docs only; no `.cs` or project file changes. Each item was reported from a reading of the files, so check it against the code before editing. Line numbers are as of BL-1820.

In `Application/Surl.Cli.UnitLibrary/CLAUDE.md`:

- Line 3, "Phase 1.": looks left over for a library that now holds options from later work. Check `Documentation/Planning` before removing it.
- Lines 14-16: the "It references ..." list omits `Surl.Cookies.UnitLibrary`, which the csproj references.
- Lines 30-32: says `Surl.Console` reads `--user-file` and `--keytab`; `Surl.Composition` does (`AuthenticationComposition`, `KeytabComposition`), and `CommandLineRunner` passes in the file-reading function.
- Line 54: says `Surl.Console` checks `--ssh-ciphers` and `--ssh-macs`; `SshAlgorithmComposition` does.
- Line 57: says `Surl.Console` reads the SSH files and serves those options; `Surl.Composition` does (`SshHostKeyComposition`, `AuthenticationComposition`).
- TLS-SRP bullet: "no listen URL that uses TLS" omits that `TlsSrpCombination` also counts `--proxy` listen URLs.

In `Application/Surl.Composition.UnitLibrary/CLAUDE.md`:

- Line 3: "every original protocol server and the horizontal libraries" omits the extended servers, `Surl.Cli` and `Surl.Control`, which the csproj also references.
- Line 220: says the readable-by-others warning comes right after the `--fault` warning; `ComposedServing.WriteStartUpLines` writes other warnings between them.

## Acceptance criteria

- [x] Both files' reference descriptions agree with the `ProjectReference` items in their csproj files.
- [x] The Cli file names the `Surl.Composition` types that read or check `--user-file`, `--keytab`, `--ssh-ciphers`, `--ssh-macs` and the SSH files, wherever it now says `Surl.Console` does.
- [x] The TLS-SRP bullet states which listen URLs `TlsSrpCombination` counts, `--proxy` listen URLs included.
- [x] The Composition file states the position of the readable-by-others warning as `ComposedServing.WriteStartUpLines` writes it.
- [x] "Phase 1." is removed, or kept with a statement in Notes of why it is still true.

## Notes

- Reference lists: Cli gained `Surl.Cookies.UnitLibrary` (4 references); Composition's intro now covers all 48 (17
  original servers, 19 extended, 12 other libraries).
- "Phase 1." removed: Product-Overview's Phasing table does list `Surl.Cli` under phase 1, but the bare line read as
  the library's scope, and the file now covers much later work.
- Verified: `dotnet build` 0 warnings, 0 errors; fast tests exit 0. Docs only, so no `.cs` or project file changed.
- Stale statements found outside this task's scope are filed as BL-1859 to BL-1863.

## Log

- 2026-11-26: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Both CLAUDE.md files match the code on references, file readers, the TLS-SRP listen URL rule and warning order
