---
id: BL-1236
title: Name the proving tests in the extended conformance matrices' rows that still point at deferred parents
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Conformance/Extended]
requirement: none
created: 2026-11-15
completed:
---
# BL-1236 — Name the proving tests in the extended conformance matrices' rows that still point at deferred parents

## Goal

Every command-line row of the extended requirement matrices names the tests that prove it, and none says
"n/a" because the work is "BL-427's (Deferred)", "BL-468 adds", "BL-522's" or another task's, now that that
work is Done.

## Context

- The rows were written while the "Serve ... from the surl executable" tasks were open. Their parts are
  all Done (BL-1023 to BL-1028, BL-971, BL-978, BL-979, BL-973, BL-974), so each "n/a" below is now false
  (root `CLAUDE.md`, "Say what it does": every statement is true of the code as it is now).
- Rows to rewrite, with tests found on 2026-11-15 (confirm each still exists with a search before naming it;
  add any better one you find):

| File | Row | Tests that prove it |
| --- | --- | --- |
| `AzureStorage.md` | `AzureStorage:Surl-Tls-1` (`az`, `abfss` implicit TLS, 58 without a certificate) | `SurlServerCompositionTests.ComposeAsync_AzWithSelfSignedAndNoAzPlaintext_ServesItOverTls`, `AzPlaintextCompositionTests.MarkAzListenUrlsPlain_WithoutTheOption_IsTheCommandLineItselfAndAzStillNeedsACertificate` (search `Surl.Composition.UnitTests`, `Surl.Console.UnitTests` and `Surl.Networking.UnitTests` for an `az` exit-58 and ALPN test too) |
| `AzureStorage.md` | `AzureStorage:Surl-Tls-2` (`abfs` plain) | `SurlServerCompositionTests.ComposeAsync_AbfsWithoutACertificate_StartsPlain` |
| `AzureStorage.md` | `AzureStorage:Surl-Tls-3` (`--az-plaintext`) | `CommandLineRunnerExtendedProtocolTests.RunAsync_AzWithAzPlaintextAndNoCertificate_ServesItPlainAndWarns`, `SurlServerCompositionTests.ComposeAsync_AzWithAzPlaintext_ServesItPlainWithNoTlsSettingsAndWarns` |
| `GoogleCloudStorage.md` | `GCS:Surl-Tls-2` (`--gs-plaintext`) | `CommandLineRunnerExtendedProtocolTests.RunAsync_GsWithGsPlaintextAndNoCertificate_ServesItPlainAndWarns`, `SurlServerCompositionTests.ComposeAsync_GsWithGsPlaintext_ServesItPlainWithNoTlsSettingsAndWarns` |
| `OciRegistry.md` | `OCI:Surl-Tls-2` (`--oci-plaintext`; today just "BL-971") | `SurlServerCompositionTests.ComposeAsync_OciWithOciPlaintext_ServesItPlainWithNoTlsSettingsAndWarns`, `OciPlaintextCompositionTests.MarkOciListenUrlsPlain_WithTheOption_MarksEveryOciListenUrlPlainAndLeavesTheOthers` |
| `Kafka.md` | `Kafka:Surl-Compression-3` (`--kafka-fetch-compression` exists) | `KafkaFetchCompressionOptionTests` in `Surl.Cli.UnitTests` (`Parse_EachKafkaFetchCompressionWord_IsRead`, `Parse_UnknownKafkaFetchCompression_IsRefusedFailedInitWithTheOptionErrorText`, `KafkaFetchCompression_AllowedValues_NameTheFiveCodecs`) |
| `McpServer.md` | `MCP:Surl-Stdio-1` (listen URL shape) | `ListenUrlParserTests.Parse_McpStdioClaimed_IsAListenUrlWithNoHostAndNoPort`, `ListenUrlParserTests.Parse_McpStdioWithAPart_IsMalformed` |
| `McpServer.md` | `MCP:Surl-Stdio-2` (`FailedInit` beside another listen URL) | `CommandLineRunnerMcpStdioTests.RunAsync_McpStdioBesideAnotherListenUrl_IsRefusedWithFailedInit` (and, for an option writing to standard output, whichever test proves it; if none does, say so in the row and file the gap with `task-planner`) |
| `McpServer.md` | `MCP:Http-4` (bind to localhost) | keep "n/a" if still true, but cite the `--aihelp` example by its `AiHelpExamples` entry instead of "(BL-468)" |
| `McpServer.md` | `MCP:Surl-Http-4` (`mcps` implicit TLS) | `CommandLineRunnerExtendedProtocolTests.RunAsync_McpsWithSelfSigned_ListensAtItsDefaultPort`, `RunAsync_McpsWithoutACertificate_ExitsWithCertificateProblem` |

- Leave ADRs alone: their mentions of BL-834 or BL-522 are history, not current-state claims.

## Acceptance criteria

- [ ] Each row in the table above names, in its "Covered by" cell, at least one test that exists in the
      repository, and none of those cells still says "(Deferred)", "BL-468 changes", "BL-468 adds",
      "BL-522's" or "BL-1027's".
- [ ] `Select-String -Path Documentation/Conformance/Extended/*.md -Pattern '\(Deferred\)'` finds nothing.
- [ ] Every test name added to these five files is found by a search of the `*.UnitTests` and
      `Surl.Conformance.UnitTests` folders.
- [ ] Any requirement with no proving test is listed in Notes with the task `task-planner` filed for it.

## Notes

- Filed by `task-planner` on 2026-11-15 while reviewing the Deferred tasks.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
