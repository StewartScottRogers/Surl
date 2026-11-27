---
id: BL-1825
title: Name every registered tool in the instructions surl-mcp sends at initialize
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Mcp, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-26
completed:
---
# BL-1825 — Name every registered tool in the instructions surl-mcp sends at initialize

## Goal

The `instructions` an MCP client receives from `surl-mcp` name every tool it registers, and a test fails when
a registered tool is left out.

## Context

Found while running BL-1807 (checked on 2026-11-26). `Program.Instructions`
(`Application/Surl.Mcp/Program.cs`) makes no false statement, but an agent reading it learns of neither the
read-back tools (`list_mailboxes` to `read_zone`), nor `set_key`, `add_ldap_entries` and `add_dns_zone`, nor
`delete_file`, `remove_account` and the proxy, TLS-SRP and password tools. Without the read-back tools an agent
may not know it can observe what a client left in a stand-in's stores.

The only test of the text, `ProgramTests.RunAsync_Initialize_AnswersWithSurlMcpsServerInfoAndTheToolsCapability`,
compares the answer with the constant, so no expected string needs updating. Add the names to the existing
prose, grouped by job (run, watch, seed and delete, read back, accounts, reset and restore, faults), and keep the
sentences about loopback addresses and the `surl://aihelp` resources. ADR-0103 decision 6 describes a shorter,
two-sentence text from when there were four tools; the string has grown since, and this follows it.

No store, event or fault changes: this task changes the text sent at `initialize`.

## Acceptance criteria

- [ ] A test in `Application/Surl.Mcp.UnitTests/ProgramTests.cs` reads the names `tools/list` returns and fails,
      naming the tool, unless `Program.Instructions` contains each as a whole word (`read_key` is not satisfied
      by `read_keys`).
- [ ] That test passes, as does the rest of `Surl.Mcp.UnitTests`, with no `TestCategory=Integration`.
- [ ] `Program.Instructions` still says that servers listen on loopback addresses unless whoever started
      `surl-mcp` allowed others, and that the `surl://aihelp` resources explain `start_server`'s listen URLs and
      options.
- [ ] `dotnet build Application/Surl.Mcp.UnitTests -warnaserror` is clean.

## Notes

## Log

- 2026-11-26: Created.
