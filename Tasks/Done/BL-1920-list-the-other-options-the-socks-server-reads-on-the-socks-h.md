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
completed: 2026-11-27
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

- [x] `surl --help socks` and `surl --aihelp socks` list `--proxy-allow-origin` and `--head-timeout`, and each other option named above that the same rule used by other server topics puts there; the task's Notes say why each other one is left out.
- [x] `Surl.Cli.UnitTests`' `HelpTextTests` pins the new list.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Added the `socks` category to `--head-timeout` and `--proxy-allow-origin`. `--head-timeout` lists each server
  topic whose server reads it, and `Socks5Exchange` and `SocksExchange` read `Limits.HeadTimeout` to bound the
  handshake and the origin's connect. `--proxy-allow-origin` sets which origins the SOCKS server may connect to.
- `--max-connections` and `--max-connections-per-address` left out: in `limits` only; no server topic lists them
  and the SOCKS server does not read them.
- `--max-time` left out: in `limits` only; the SOCKS server does not read it.
- `--idle-timeout` left out: it is on the `amqp`, `amqp1`, `nats` and `stomp` topics, whose servers
  `Surl.Composition` hands the value to. The SOCKS server is not handed it and does not read it; the serving
  engine applies it to every exchange alike.
- `--unix-socket` and `--abstract-unix-socket` left out: in `surl` only; no server topic lists them, although
  other servers serve on a socket file too.
- The `socks` paragraphs in `AiHelpProse` already describe all of these, so no prose changed.
- Verified: `dotnet build -warnaserror` 0 warnings; `dotnet format --verify-no-changes` exit 0; every fast test
  project passed (`Surl.Cli.UnitTests` 2007 of 2007).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. surl --help socks and --aihelp socks now list --head-timeout and --proxy-allow-origin
