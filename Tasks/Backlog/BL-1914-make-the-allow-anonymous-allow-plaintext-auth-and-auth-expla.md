---
id: BL-1914
title: Make the --allow-anonymous, --allow-plaintext-auth and --auth explanations true of every server that now lists them
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1914 — Make the --allow-anonymous, --allow-plaintext-auth and --auth explanations true of every server that now lists them

## Goal

The explanations of `--allow-anonymous`, `--allow-plaintext-auth` and `--auth` say nothing false or incomplete about the servers whose help pages show them.

## Context

- Found by BL-1902, which added these options to the help pages of more servers.
- `AllowAnonymousExplanation` and `AllowPlaintextAuthExplanation` in `Application/Surl.Cli.UnitLibrary/CommandLineOptions.cs`
  list only some of the servers each option loosens, and `AuthExplanation` does not say which methods the DNS over
  HTTPS, gRPC, Cloud Storage, MCP, SignalR and WebTransport servers judge by it.

## Acceptance criteria

- [ ] Each of the three explanations either names every server in the option's categories that it changes, or says in words that the servers named are examples.
- [ ] Each statement is checked against the server code it describes.
- [ ] The pinned explanation lines in `Surl.Cli.UnitTests` match.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
