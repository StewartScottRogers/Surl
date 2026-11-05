---
id: BL-610
title: Record Stewart's reversal: Surl.Api and Surl.Mcp join surl as Surl's products
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Product-Overview.md, CLAUDE.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-610 — Record Stewart's reversal: Surl.Api and Surl.Mcp join surl as Surl's products

## Goal

An ADR records Stewart's decision of 2026-11-04 that Surl ships two more hosts beside the `surl` executable: `Surl.Api`,
a .NET programming interface that starts and stops Surl's protocol servers in-process, and `Surl.Mcp`, an MCP server
host on standard input and output that lets AI agents start, list and stop Surl servers and inspect what a client
sent. The Product Overview and root `CLAUDE.md` stop saying that `surl` is the only product.

## Context

- This reverses Stewart's 2026-09-28 answer to Product Overview open question 4 ("A managed NuGet API, or the `surl`
  executable only?" answered "the executable"). The decision is Stewart's, not Claude's: the ADR is marked
  "Decided by Stewart, 2026-11-04", not "Decided by Claude under Stewart's delegation". Do not reopen it.
- What Stewart decided, and all this ADR records (the design is decided later, by BL-611 for the composition and
  `Surl.Api`, and BL-612 for `Surl.Mcp`):
  - `Surl.Api`: a .NET test suite can stand up a server without launching a process - for example start an FTP or
    HTTP server on `127.0.0.1` port 0, read the bound address, configure accounts, content and limits the way the
    command line does, and stop it. It follows the same secure-by-default rules as the command line (ADR-0032).
  - `Surl.Mcp`: an MCP server host (stdio) whose tools start, list and stop Surl servers and show what a client sent.
  - Both live in `Application/` beside `Surl.Console` and `Surl.Cli` (ADR-0079), are composition roots like
    `Surl.Console`, and are modelled on the Curl port's `Curl.Api` and `Curl.Mcp` hosts. Code may be copied from the
    port; the port is never an oracle (ADR-0003).
  - Upstream curl stays Surl's only oracle; nothing about the hosts changes what a protocol server answers.
- Places that say `surl` is the only product, found on 2026-11-04:
  - `Documentation/Product/Product-Overview.md`: "Non-goals" (the bullet "**A managed NuGet API.** The `surl`
    executable is the only product ..."), "Users" (lists only `surl` on `PATH`, the Curl port and contributors), and
    "Open questions" row 4.
  - Root `CLAUDE.md`: "Overview" (describes Surl only as the `surl` command line), "Repository layout" (the
    `Application/` line names only `Surl.Console` and `Surl.Cli`), and "Project naming" ("The executable is
    `Surl.Console` ... It is the only exception.").
- The ADR index is `Documentation/Planning/Decisions/README.md`; take the next free ADR number from that folder at the
  time of writing.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by Stewart,
      2026-11-04". It states that it reverses the answer to Product Overview open question 4, names `Surl.Api` and
      `Surl.Mcp` with the purpose of each as given above, places both in `Application/`, says both keep the
      command line's secure-by-default rules, says the design is decided by BL-611 and BL-612, and is listed in the
      folder's `README.md` index.
- [ ] `Documentation/Product/Product-Overview.md`: the "A managed NuGet API" non-goal is gone or replaced by a true
      statement naming the ADR; "Open questions" row 4 reads "Reversed 2026-11-04" with Stewart's new answer and
      the ADR; "Users" adds .NET test suites (through `Surl.Api`) and AI agents (through `Surl.Mcp`); "Last updated"
      is 2026-11-04.
- [ ] Root `CLAUDE.md`: "Overview" mentions the two hosts as planned (with the ADR); the `Application/` line in
      "Repository layout" lists `Surl.Api` and `Surl.Mcp` as planned; "Project naming" no longer says `Surl.Console`
      is the only exception and states that `Surl.Api` and the `Surl.Mcp` executable are naming exceptions too,
      as intent until they exist.
- [ ] No sentence in those files still says `surl` is Surl's only product.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
