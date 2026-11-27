---
id: BL-1822
title: Correct the remaining stale library lists and Surl.Console attributions in the Surl.Cli and Surl.Composition CLAUDE.md files
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary/CLAUDE.md, Application/Surl.Composition.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed:
---
# BL-1822 — Correct the remaining stale library lists and Surl.Console attributions in the Surl.Cli and Surl.Composition CLAUDE.md files

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

- [ ] Both files' reference descriptions agree with the `ProjectReference` items in their csproj files.
- [ ] The Cli file names the `Surl.Composition` types that read or check `--user-file`, `--keytab`, `--ssh-ciphers`, `--ssh-macs` and the SSH files, wherever it now says `Surl.Console` does.
- [ ] The TLS-SRP bullet states which listen URLs `TlsSrpCombination` counts, `--proxy` listen URLs included.
- [ ] The Composition file states the position of the readable-by-others warning as `ComposedServing.WriteStartUpLines` writes it.
- [ ] "Phase 1." is removed, or kept with a statement in Notes of why it is still true.

## Notes

## Log

- 2026-11-26: Created.
