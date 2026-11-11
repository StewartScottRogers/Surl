---
id: BL-561
title: Serve SOCKS5 on a Unix domain socket from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-560, BL-548]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0121-how-surl-serves-socks4-socks4a-socks5-and-socks5h.md]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-561 — Serve SOCKS5 on a Unix domain socket from the surl executable

## Goal

`surl` serves its SOCKS5 server on a Unix domain socket, using BL-571's command-line form (BL-548
added it for HTTP), so upstream curl's `--socks5 localhost/<path>` style proxy over a Unix socket
completes its exchange.

## Context

- Decisions: BL-571's ADR (the Unix socket form, which servers may use it, Windows and abstract
  names) and BL-557's ADR decision 5 (SOCKS5 on a Unix socket).
- Code: BL-548's Unix socket option handling in `Application/Surl.Cli.UnitLibrary` and
  `Application/Surl.Console/CommandLineRunner.cs`; BL-560's SOCKS composition. Whatever BL-548
  refused for SOCKS, this task allows.
- What curl does: https://curl.se/docs/manpage.html, `--proxy` and `--socks5` with a Unix socket
  path (curl 8.21.0), measured with `Record-CurlExchange.ps1` on a pin that reports `UnixSockets`.
- Upstream test cases this makes reachable by hand (tag `curl-8_21_0`), `socks5unix`: 1467, 1468,
  1470 (`!win32`, so Linux and macOS only).

## Acceptance criteria

- [x] `Surl.Cli.UnitTests` has named tests accepting the Unix socket form with a `socks5://` listen
      URL, and refusing it with `socks4://` if the ADR says so, with its `SurlExitCode`.
- [x] `Surl.Console.UnitTests` has a named test that runs `CommandLineRunner` as SOCKS5 on a Unix
      socket and relays one request to an origin started in the test, marked for the platforms the
      ADR allows, without `TestCategory=Integration`.
- [x] The SOCKS `--aihelp` topic and an `AiHelpExamples` entry describe the Unix socket form, and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` passes.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and
      `Surl.Cli.UnitLibrary` keep 100% line and branch coverage.

## Notes

- Plan: BL-548's `UnixSocketCombination` is scheme-agnostic, so a positional SOCKS listen URL with
  `--unix-socket` or `--abstract-unix-socket` already parsed and served; no production branch was
  needed. The work is the tests, the `socks` `--aihelp` paragraph and example, and the decision below.
- Decision (ADR-0121 amendment 1, decided by Claude under Stewart's delegation): `-x`/`--proxy` with a
  SOCKS URL beside a Unix socket stays refused `FailedInit` (2), `option --unix-socket: cannot be
  used with --proxy`, as ADR-0088 amendment 7 refuses every proxy listen URL; give the SOCKS URL
  positionally. SOCKS4 on a socket file is accepted (ADR-0121 decision 5), so there is no `socks4://`
  refusal; `Parse_UnixSocketWithASocks4ListenUrl_ServesSocks4OnTheSocket` pins the acceptance.
- Tests: `CommandLineParserUnixSocketTests.Parse_UnixSocketWithASocks5ListenUrl_ServesSocks5OnTheSocket`,
  `Parse_UnixSocketWithASocks4ListenUrl_ServesSocks4OnTheSocket`,
  `Parse_UnixSocketWithASocks5ProxyOptionUrl_IsRefusedAsAProxy`; and
  `CommandLineRunnerUnixSocketTests.RunAsync_UnixSocketWithSocks5h_RelaysOneRequestToTheOriginOverTheSocketFile`,
  on every platform (ADR-0094 decision 4 supports Windows `AF_UNIX`; the `!win32` mark belongs to
  upstream's test cases, BL-562's concern), not Integration, as BL-548's HTTP Unix socket test is not.
- `touches` gained the ADR-0121 file for the amendment; no task in Doing names it.
- Coverage: no production code path was added (only an `AiHelpExamples` row and an `AiHelpProse`
  string), so `Surl.Cli.UnitLibrary` and `Surl.Console` coverage is unchanged.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. surl serves SOCKS4/5 on a Unix domain socket for a positional socks listen URL; --aihelp socks topic and example describe it
