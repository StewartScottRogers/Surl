---
id: BL-612
title: Decide Surl.Mcp's tools and the MCP core it shares with the extended MCP server
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-611]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed:
---
# BL-612 — Decide Surl.Mcp's tools and the MCP core it shares with the extended MCP server

## Goal

An ADR decides how `Surl.Mcp` is built: the MCP core it shares with the extended MCP protocol server
(`Surl.Extended.Protocol.McpServer`, BL-463 to BL-469), the split between executable and tool library, the tools
that start, list and stop Surl servers and show what a client sent, their safety rules, its agent-facing help, and
how it ships.

## Context

- BL-610's ADR records Stewart's decision that `Surl.Mcp` exists. BL-611's ADR decides `Surl.Api`, which the tools
  drive. This ADR is marked "Decided by Claude under Stewart's delegation".
- Specification: MCP, https://modelcontextprotocol.io/specification (revision 2025-06-18 is the one BL-463 plans
  for): "Base Protocol > Overview" (JSON-RPC 2.0), "Lifecycle", "Transports > stdio" (newline-delimited, nothing but
  MCP messages on standard output, logs on standard error), "Server Features > Tools" and "> Resources",
  "Utilities" (`ping`, pagination, cancellation).
- The extended MCP server already planned, which needs the same machinery: BL-463 (its decisions 3 and 7 are
  version negotiation and JSON-RPC error codes), BL-464 (JSON-RPC messages and the lifecycle), BL-465 (`tools/list`,
  `tools/call`, pagination), BL-467 (the stdio transport). They are amended to build on the core this ADR decides.
- The port's design, copyable but never evidence (ADR-0003), read with
  `git -C Z:\repos\Curl show origin/master:<path>`: `Curl.Mcp/CLAUDE.md`, `Curl.Mcp.UnitLibrary/CLAUDE.md`,
  `Curl.Mcp.Protocol.UnitLibrary/CLAUDE.md`, `Curl.Mcp/Curl.Mcp.csproj`, and its ADR-0548, ADR-0596 (packing),
  ADR-0799 (the hand-written server), ADR-0800 (JSON-RPC parser rules) and ADR-0801 (tool and resource model: one
  explicit `McpServerRegistry.Add` per tool, hand-written input schemas, source-generated JSON).
- Constraints: base class library only (`System.Text.Json` with `Utf8JsonReader`/`Utf8JsonWriter` or a
  source-generated context); native AOT, so no assembly scanning; the four quality gates; MSTest only; tests pass
  on Windows, Linux and macOS.
- Publishing a NuGet package or a GitHub release needs Stewart (root `CLAUDE.md`). Decide what is packed and
  published by the workflow's dry run; do not plan the push.

Decide and record:

1. **The shared MCP core.** Planning assumption: yes, one horizontal library,
   `Protocols/Http/Surl.McpServing.UnitLibrary` (namespace `Surl.McpServing`) with its `.UnitTests` twin, holding
   JSON-RPC 2.0 messages and error codes, the lifecycle with version negotiation, `ping`, the tool and resource
   model and registry (one explicit `Add` per tool; input schemas written by hand), `tools/list` and `tools/call`
   with pagination, `resources/list` and `resources/read`, the newline-delimited session over a stream pair, and
   the message size limit. It references nothing, or only `Surl.Protocol.Abstractions`; it gets a row in
   ADR-0002's table and in `ProtocolIsolationTests.HorizontalLibraries`. The Streamable HTTP transport and the
   extended server's own tools stay in `Surl.Extended.Protocol.McpServer`. Decide the name, the folder (a new
   category needs this ADR to say so), and the protocol versions accepted with the answer to an unsupported one,
   and the JSON-RPC error code of each failure - which then bind BL-463's decisions 3 and 7.
2. **The host's projects.** Planning assumption, mirroring the port: `Application/Surl.Mcp` (the executable, a
   naming exception; decide its assembly name, planning `surl-mcp`), `Application/Surl.Mcp.UnitLibrary` (all tool
   logic, over `Surl.Api`), and `Application/Surl.Mcp.UnitTests` testing both.
3. **The tools.** Names, descriptions and input schemas. Planning assumption: `start_server` (listen URLs and the
   options `Surl.Api` takes), `list_servers`, `stop_server`, and `get_exchanges` (what each client sent and was
   sent, text where it is UTF-8 and base64 where not, cut to a cap). Results and `isError` for each failure,
   including each `SurlExitCode` a start can fail with.
4. **Safety.** Which listen hosts a tool may bind (planning: loopback only unless the host is started with an
   explicit switch), the most servers at once, which local paths a tool may name (a data directory, `--user-file`,
   certificates, keytabs, host keys) and the root they are confined to (the port uses `CURL_MCP_ROOT`), and that the
   command line's secure-by-default rules apply unchanged.
5. **Lifetime.** Every server the host started stops when standard input closes, on Ctrl+C or SIGTERM; logs go to
   standard error only.
6. **Agent-facing help.** Planning assumption: each tool's description and schema, and the `surl --aihelp` topics
   served as MCP resources (a URI per topic, from `Surl.Cli`'s `AiHelpText`), with a completeness test that every
   topic is a resource; and whether `surl --aihelp`'s overview points at `Surl.Mcp`.
7. **Shipping.** Planning assumption: `Surl.Mcp` publishes native AOT as a second executable in
   `.github/workflows/release.yml`, one archive per runtime identifier beside `surl`'s, listed in `DOWNLOAD.md`; and
   whether it is also packed as a NuGet MCP server package (`PackageType` `McpServer`, `.mcp/server.json`) as the
   port's is. Pushing either is Stewart's.
8. **Amendments to the extended MCP server's tasks.** Confirm that `Surl.Extended.Protocol.McpServer` references the
   core, which BL-464, BL-465 and BL-467 now assume.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by Claude
      under Stewart's delegation", citing BL-610's and BL-611's ADRs and each specification page it relies on. It
      answers decisions 1 to 8 above and lists the follow-up tasks named in this task's Notes.
- [ ] The ADR amends ADR-0002's table (it says so in its own text, and the index row says "amends ADR-0002"), and
      is listed in `Documentation/Planning/Decisions/README.md`'s index.
- [ ] Where the ADR departs from a planning assumption above, its Consequences section names each follow-up task
      whose Goal or touches it changes.

## Notes

Follow-up tasks filed with this one (2026-11-04), which the ADR lists: the core BL-619 (JSON-RPC and lifecycle),
BL-620 (tools), BL-621 (stdio session) and BL-623 (resources); the host BL-624 (start, list and stop tools), BL-625
(the executable), BL-626 (the exchanges tool) and BL-627 (`--aihelp` resources); BL-628 (`surl --aihelp`'s pointer),
BL-632 (proof against pinned upstream curl), BL-637 (publishing `surl-mcp`), BL-643 (NuGet packing, dry run only)
and BL-649 (documents). Amended to build on the core: BL-463 (now depends on this task), BL-464, BL-465, BL-467, and
BL-468 (composition moved by BL-609 and BL-614).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
