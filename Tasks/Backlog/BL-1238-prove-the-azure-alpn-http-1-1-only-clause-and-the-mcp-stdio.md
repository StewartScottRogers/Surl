---
id: BL-1238
title: Prove the Azure ALPN http/1.1-only clause and the mcp+stdio refusal beside a standard-output option with tests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitTests, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Documentation/Conformance/Extended]
requirement: none
created: 2026-11-15
completed:
---
# BL-1238 — Prove the Azure ALPN http/1.1-only clause and the mcp+stdio refusal beside a standard-output option with tests

## Goal

Two extended-matrix requirements with no proving test have one: `AzureStorage:Surl-Tls-1`'s ALPN `http/1.1`-only clause, and `MCP:Surl-Stdio-2`'s `FailedInit` for `mcp+stdio:` with an option writing to standard output.

## Context

Found by BL-1236 while naming proving tests in `Documentation/Conformance/Extended/AzureStorage.md` and `McpServer.md`, whose rows say "no test of its own" / "no test proves". Existing neighbours: `SurlServerCompositionTests.ComposeAsync_AzWithSelfSignedAndNoAzPlaintext_ServesItOverTls` (Surl.Composition.UnitTests) and `CommandLineRunnerMcpStdioTests` (Surl.Console.UnitTests). If the mcp+stdio refusal is not implemented, decide by ADR and implement.

## Acceptance criteria

- [ ] A named test proves `az` and `abfss` offer only ALPN `http/1.1`.
- [ ] A named test proves `mcp+stdio:` with an option writing to standard output is `FailedInit` (2), or an ADR records the behaviour chosen instead.
- [ ] The two matrix rows name those tests.

## Notes

## Log

- 2026-11-15: Created.
