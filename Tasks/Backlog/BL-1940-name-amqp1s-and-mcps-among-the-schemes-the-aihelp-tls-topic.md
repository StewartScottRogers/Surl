---
id: BL-1940
title: Name amqp1s and mcps among the schemes the --aihelp tls topic says start with a TLS handshake
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1940 — Name amqp1s and mcps among the schemes the --aihelp tls topic says start with a TLS handshake

## Goal

The first `--aihelp tls` paragraph names every scheme that starts with a TLS handshake.

## Context

Found while working BL-1926 (2026-11-27), read from source. `Foundation/Surl.Protocol.Abstractions.UnitLibrary/TlsSchemes.cs` lists `amqp1s` and `mcps`; the paragraph in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` (`tls` topic) names neither. Check the `ManualText` list too.

## Acceptance criteria

- [ ] A test in `Surl.Cli.UnitTests` fails when a scheme in `TlsSchemes` is missing from the `tls` topic's first paragraph.
- [ ] The paragraph names `amqp1s` and `mcps`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
