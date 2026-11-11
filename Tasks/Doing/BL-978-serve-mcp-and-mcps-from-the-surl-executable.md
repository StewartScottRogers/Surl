---
id: BL-978
title: Serve mcp and mcps from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-466, BL-405, BL-614]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-978 — Serve mcp and mcps from the surl executable

## Goal

`surl mcp://127.0.0.1:0` serves MCP over Streamable HTTP and `surl mcps://127.0.0.1:0` serves it over TLS; the
`--aihelp` topic and help category exist, and the extension switch turns both off.

## Context

- First of the two tasks BL-468 ("Serve mcp, mcps and mcp+stdio from the surl executable") was split into on
  2026-11-06 for the 60-minute size limit: this, then BL-979 (`mcp+stdio`).
- Decision: ADR-0111 decision 4 - `mcp` is plain HTTP/1.1 on port 80 (no `--mcp-plaintext` exists, the scheme is
  the plain one); `mcps` is wrapped for TLS as `https` is (`ImplicitTlsSchemeServer`, ADR-0010), port 443, ALPN
  `http/1.1` only, `--cert` or `--self-signed` required, else `CertificateProblem` (58).
- Composition: `ExtendedProtocolServers.Compose` / `Registrations` in `Application/Surl.Composition.UnitLibrary`
  (BL-609; the start sequence moved there in BL-614).
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with `mcp` and `mcps` (BL-979 adds
  `mcp+stdio`), an `AiHelpProse.TopicAbout` paragraph naming MCP 2025-06-18 as its oracle, an `AiHelpExamples`
  entry, and the topic list in `AiHelpTextTests` growing by one.
- Stand-in: no shared store; the server's sessions are observed through the engine's exchange events.

## Acceptance criteria

- [ ] A named test finds `mcp` and `mcps` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition`, `Surl.Console` and
      `Surl.Cli` keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
