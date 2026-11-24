---
id: BL-1663
title: Print -w style output per exchange from surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Cli, Application/Surl.Console, Application/Surl.Mcp.UnitLibrary, Documentation/Product/Product-Overview.md, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1663 — Print -w style output per exchange from surl

## Goal

`surl` can print a per-exchange line in the spirit of curl's `-w` / `--write-out`, so a script sees each exchange's outcome on standard output.

## Context

The product overview's "Seeing the exchange" scope lists "`-w` style output per exchange" as in scope and not built (audit BL-1651: no `--write-out` or `-w` option exists in `Application/`). Decide the variable set and format by an ADR; measure upstream curl's `-w` variables first. Adding an option needs its `--aihelp` facts.

## Acceptance criteria

- [x] `surl` accepts a write-out option, prints the chosen format once per exchange, and has `--aihelp` facts and tests.
- [x] The product overview's "not built yet" sentence is replaced by a statement naming this task.

## Notes

- Decided in ADR-0190 (Claude under Stewart's delegation), after measuring pinned curl 8.21.0 with `Record-CurlExchange.ps1`: `%%`, `\r \n \t` only, unknown `%{x}` prints nothing and warns on stderr, unterminated `%{` literal, `%{json}`.
- `-w <format>` / `--write-out`, category logging. `Surl.Cli`: `WriteOutFormat.Expand` (pure) with `WriteOutValues`/`WriteOutResult`; `Surl.Console`: `WriteOutPrinter`, an event-stream subscriber pairing started/ended events.
- Variables: exchange_id, json, listen_url, remote_ip, remote_port, scheme, size_download (surl sent), size_upload (surl received), time_total. `@file` formats not read.
- Added `Application/Surl.Mcp.UnitLibrary` to touches (no Doing task names it): `CommandLinePathMembers` must classify the new `SurlCommandLine.WriteOut` member as path-free. Also touched the product overview and ADR index.
- Not applied to `mcp+stdio:` (no exchanges, stdout is MCP only).
## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. surl -w/--write-out prints a curl-style format to stdout once per exchange (ADR-0190)
