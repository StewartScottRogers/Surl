---
id: BL-1920
title: List the other options the SOCKS server reads on the SOCKS help page
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1920 — List the other options the SOCKS server reads on the SOCKS help page

## Goal

`surl --help socks` and `surl --aihelp socks` list each non-account option the SOCKS server reads.

## Context

- Found by BL-1915, which added `-U`, `--proxy-user-file` and `--keytab` to the `socks` category.
- The `socks` paragraphs in `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` name `--proxy-allow-origin`,
  `--head-timeout`, `--max-connections`, `--max-connections-per-address`, `--idle-timeout`, `--max-time`,
  `--unix-socket` and `--abstract-unix-socket`, and none of their `CommandLineOptions` rows has the `socks`
  category.
- Decide which belong on the page by how other server topics treat each one (for example, `--head-timeout`
  lists its server topics; `--max-connections` is in `limits` only).

## Acceptance criteria

- [ ] `surl --help socks` and `surl --aihelp socks` list `--proxy-allow-origin` and `--head-timeout`, and each other option named above that the same rule used by other server topics puts there; the task's Notes say why each other one is left out.
- [ ] `Surl.Cli.UnitTests`' `HelpTextTests` pins the new list.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
