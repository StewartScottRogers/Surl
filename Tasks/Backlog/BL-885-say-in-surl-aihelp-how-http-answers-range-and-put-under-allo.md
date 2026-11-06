---
id: BL-885
title: Say in surl --aihelp how http answers Range and PUT under --allow-uploads
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-723]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-885 — Say in surl --aihelp how http answers Range and PUT under --allow-uploads

## Goal

`surl --aihelp http` says that a `GET` with one `Range` is answered `206` (`416` past the end), and
that under `--allow-uploads`, with a login, a `PUT` is stored and answered `201` or `204`, as
ADR-0133 decides.

## Context

- Found in BL-723 (2026-11-05), which built both. `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`
  still says POST, PUT, DELETE and the rest are answered `405` with `Allow: GET, HEAD`; under
  `--allow-uploads` a `PUT` is now stored, and the other methods' `405` says `Allow: GET, HEAD, PUT`.
  BL-723 left `Surl.Cli` alone because BL-880 held it.
- ADR-0133 decision 1 (Range: one range, `If-Range` by the exact `Last-Modified`, `416` with
  `Content-Range: bytes */<length>`, several ranges served whole `200` until BL-884) and decision 2
  (PUT: `201` creates, `204` replaces, `403` not permitted, `409` a directory or no such directory,
  `413` past `--max-filesize`).

## Acceptance criteria

- [ ] The `http` topic's prose in `AiHelpProse` states the Range and PUT answers ADR-0133 gives, and
      the `405` sentence says it applies to PUT only without `--allow-uploads`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-05: Created.
