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
completed:
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

- [ ] The comment says the config files (`SurlServerOptions.ConfigFiles`) are read through the seam.
- [ ] The comment says every file the composition reads at start is read through it, or names them all, without giving a partial list.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean and the diff changes comment lines only.

## Notes

## Log

- 2026-11-27: Created.
