---
id: BL-561
title: Serve SOCKS5 on a Unix domain socket from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-560, BL-548]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-561 — Serve SOCKS5 on a Unix domain socket from the surl executable

## Goal

`surl` serves its SOCKS5 server on a Unix domain socket, using BL-546's command-line form (BL-548
added it for HTTP), so upstream curl's `--socks5 localhost/<path>` style proxy over a Unix socket
completes its exchange.

## Context

- Decisions: BL-546's ADR (the Unix socket form, which servers may use it, Windows and abstract
  names) and BL-557's ADR decision 5 (SOCKS5 on a Unix socket).
- Code: BL-548's Unix socket option handling in `Application/Surl.Cli.UnitLibrary` and
  `Application/Surl.Console/CommandLineRunner.cs`; BL-560's SOCKS composition. Whatever BL-548
  refused for SOCKS, this task allows.
- What curl does: https://curl.se/docs/manpage.html, `--proxy` and `--socks5` with a Unix socket
  path (curl 8.21.0), measured with `Record-CurlExchange.ps1` on a pin that reports `UnixSockets`.
- Upstream test cases this makes reachable by hand (tag `curl-8_21_0`), `socks5unix`: 1467, 1468,
  1470 (`!win32`, so Linux and macOS only).

## Acceptance criteria

- [ ] `Surl.Cli.UnitTests` has named tests accepting the Unix socket form with a `socks5://` listen
      URL, and refusing it with `socks4://` if the ADR says so, with its `SurlExitCode`.
- [ ] `Surl.Console.UnitTests` has a named test that runs `CommandLineRunner` as SOCKS5 on a Unix
      socket and relays one request to an origin started in the test, marked for the platforms the
      ADR allows, without `TestCategory=Integration`.
- [ ] The SOCKS `--aihelp` topic and an `AiHelpExamples` entry describe the Unix socket form, and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` passes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and
      `Surl.Cli.UnitLibrary` keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
