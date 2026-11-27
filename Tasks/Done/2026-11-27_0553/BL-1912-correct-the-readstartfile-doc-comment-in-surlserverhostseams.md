---
id: BL-1912
title: Correct the ReadStartFile doc comment in SurlServerHostSeams
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Api/SurlServerHostSeams.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1912 — Correct the ReadStartFile doc comment in SurlServerHostSeams

## Goal

The `SurlServerHostSeams.ReadStartFile` doc comment states every kind of file `Surl.Api` reads through the seam, as the code does.

## Context

Found while working BL-1868 and checked against the code on 2026-11-27. Comment text only; no behaviour change.

- `Application/Surl.Api/SurlServerHostSeams.cs` (about lines 33-37) says the seam reads "a file the start reads (the `--user-file`, certificates, keys, host keys)", a partial list.
- `Application/Surl.Api/SurlServer.cs`, `StartAsync`, passes the seam to `SurlServerArguments.Parse`, which reads `SurlServerOptions.ConfigFiles` through it (`ApiConfigFileSource`), and to `SurlServerCompositionSeams.ReadStartFile`. The comment on that member lists the files the composition reads.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [x] The comment says the config files (`SurlServerOptions.ConfigFiles`) are read through the seam.
- [x] The comment says every file the composition reads at start is read through it, or names them all, without giving a partial list.
- [x] `dotnet build Application/Surl.Api -warnaserror` is clean and the diff changes comment lines only.

## Notes

- The comment points at `SurlServerCompositionSeams.ReadStartFile`'s list rather than copying it, so one
  list is kept true. It does not say "every file the composition reads" without that qualifier, because
  the service state of a data directory is loaded through other seams.
- It names every `-K`/`--config` file, since `SurlServerArguments.Parse` reads those in
  `AdditionalArguments` and nested ones through the same `ApiConfigFileSource`.
- Edited in the session, not by `align-and-document`: a four-line comment change.
- `verify`: build with 0 warnings, `dotnet format --verify-no-changes` clean, fast tests 0 failed.
  No Integration run: comment only.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The SurlServerHostSeams.ReadStartFile comment states that every -K config file and every composition start file is read through the seam
