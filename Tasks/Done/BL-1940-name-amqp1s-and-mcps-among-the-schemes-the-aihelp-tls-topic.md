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
completed: 2026-11-27
---
# BL-1940 — Name amqp1s and mcps among the schemes the --aihelp tls topic says start with a TLS handshake

## Goal

The first `--aihelp tls` paragraph names every scheme that starts with a TLS handshake.

## Context

Found while working BL-1926 (2026-11-27), read from source. `Foundation/Surl.Protocol.Abstractions.UnitLibrary/TlsSchemes.cs` lists `amqp1s` and `mcps`; the paragraph in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` (`tls` topic) names neither. Check the `ManualText` list too.

## Acceptance criteria

- [x] A test in `Surl.Cli.UnitTests` fails when a scheme in `TlsSchemes` is missing from the `tls` topic's first paragraph.
- [x] The paragraph names `amqp1s` and `mcps`.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Test: `AiHelpTextTests.Answer_TlsTopic_NamesEverySchemeThatStartsWithATlsHandshakeInItsFirstParagraph`. It
  failed before the fix naming exactly `amqp1s, mcps`. It checks only the list before "start with a TLS handshake".
- It reads the private `TlsSchemes.ImplicitTlsSchemes` field by reflection: that covers every scheme in the set
  without adding a public member to `Surl.Protocol.Abstractions`, which is outside `touches`. Renaming the field
  fails the test.
- `ManualText` checked: it has no list of the TLS-from-the-first-byte schemes, and its AMQP 1.0 and MCP sections
  already say `amqp1s` and `mcps` need `--cert`, `--self-signed` or TLS-SRP. No change there.
- Verify: build `-warnaserror` 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests 0 failed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. surl --aihelp tls names amqp1s and mcps, and a test fails when a TLS scheme is missing from the list
