---
id: BL-1868
title: Correct the start-file statements in the Surl.Composition docs plus the host hand-off sentence in the Surl.Cli notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1828]
touches: [Application/Surl.Composition.UnitLibrary/CLAUDE.md, Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs, Application/Surl.Composition.UnitLibrary/SurlServerCompositionSeams.cs, Application/Surl.Cli.UnitLibrary/CLAUDE.md, Application/Surl.Composition.UnitLibrary/ControlComposition.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1868 — Correct the start-file statements in the Surl.Composition docs plus the host hand-off sentence in the Surl.Cli notes

## Goal

The `Surl.Composition` docs state when the `--control` token file is read relative to the other start files and which files `ReadStartFile` reads, and the `Surl.Cli` notes state what a host hands that library, as the code does.

## Context

Found while working BL-1828 and checked against the code on 2026-11-27. Doc and comment text only; no behaviour change. Line numbers are as of that date. BL-1828 edits both `CLAUDE.md` files, so this task follows it.

- `Application/Surl.Composition.UnitLibrary/CLAUDE.md`, start table, the `--control` read row, says the token file is read "after every other start file and before the data-directory lock". The comment in `SurlServerComposition.Start.cs` (about line 303) says "last of the start files". In that file `ControlComposition.ReadToken` is the last read in `ComposeAuthentication`, which runs before `LockThenComposeAsync` takes the lock; but `ComposeServing` runs after the lock and calls `ServerTlsComposition.Compose` with `seams.ReadStartFile` (`--cert`, `--key`, `--cacert`, and under TLS-SRP `--srp-groups` and `--srp-verifiers`), and `ComposeSocksGssApiLogin` reads the `--keytab` file again, also after the lock. (BL-1827 may remove that second read; state what the code does when this task runs.)
- `Application/Surl.Composition.UnitLibrary/SurlServerCompositionSeams.cs`, the `ReadStartFile` doc comment: lists `--user-file`, `--authorized-keys`, `--keytab`, `--hostkey` and `--hostcert`. The seam also reads `--mailbox-file`, `--proxy-user-file`, `--control-token-file`, the `bind-password-file` and `ca-file` of `--user-ldap`, `--cert`, `--key`, `--cacert`, `--srp-groups` and `--srp-verifiers`. (`CommandLineRunner`'s matching comment is BL-1867's.)
- `Application/Surl.Cli.UnitLibrary/CLAUDE.md`, the paragraph "Never touch the console here; `Surl.Console` hands this library its arguments and writers": no code in `Surl.Cli.UnitLibrary` takes a writer. `CommandLineParser.Parse` is given the arguments, the extended schemes and an `IConfigFileSource`; `HelpText`, `AiHelpText`, `ManualText` and `VersionText` return text that the host writes. Keep the rule never to touch the console.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [x] The start table row says the token file is the last start file read before the data-directory lock, and names the files read after the lock.
- [x] The comment above `ControlComposition.ReadToken` in `SurlServerComposition.Start.cs` no longer says the token file is the last of all start files.
- [x] The `ReadStartFile` doc comment names every file listed in Context, or states that it reads every file the composition reads at start without giving a partial list.
- [x] The Cli notes no longer say a host hands the library writers; what they say it is handed agrees with the `CommandLineParser.Parse` overloads.
- [x] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean and the diff of each `.cs` file changes comment lines only.

## Notes

- Delivered by `align-and-document`; no behaviour change and no ADR needed.
- The code still reads `--keytab` a second time after the lock (`ComposeSocksGssApiLogin`), so the start table says so.
- Added `Application/Surl.Composition.UnitLibrary/ControlComposition.cs` to `touches`: its type summary held the same false read-order statement this task corrects. No task in `Doing` (BL-1905, BL-1910) names it.
- Also corrected the `ComposeAsync` summary in `SurlServerComposition.Start.cs`: it said no file is opened for writing, but the data-directory lock file is opened `ReadWrite`; it now says no log or trace file, and lists the start-file exit codes.
- Filed BL-1912 for the partial list in the `ReadStartFile` comment of `Surl.Api`'s `SurlServerHostSeams`.
- Verified: solution build with `-warnaserror` clean, `dotnet format --verify-no-changes` exit 0, fast tests passed in all 83 test assemblies.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The Surl.Composition docs state the start-file read order and the files read through ReadStartFile, and the Surl.Cli notes state what a host hands the library, as the code does
