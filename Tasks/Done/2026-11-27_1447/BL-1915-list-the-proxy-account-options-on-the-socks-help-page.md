---
id: BL-1915
title: List the proxy account options on the SOCKS help page
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1915 — List the proxy account options on the SOCKS help page

## Goal

`surl --help socks` and `surl --aihelp socks` list each proxy account option the SOCKS server reads.

## Context

- Found by BL-1902. `-U`/`--proxy-user` and `--proxy-user-file` are in the `auth` and `proxy` categories of
  `Application/Surl.Cli.UnitLibrary/CommandLineOptions.cs`, not `socks`, though the SOCKS paragraph in
  `AiHelpProse` says they name the accounts of every proxy listener, SOCKS included.
- Check `--keytab` (SOCKS5 GSS-API) and `--proxy-auth` against `Surl.Composition`'s `ComposeProxyPolicy` and
  `Surl.Protocol.Socks` before listing either.

## Acceptance criteria

- [x] `surl --help socks` and `surl --aihelp socks` list `-U`, `--proxy-user-file`, and each of `--keytab` and `--proxy-auth` only if the code shows the SOCKS server reads it.
- [x] A named test in `Surl.Cli.UnitTests` pins the list.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- `--keytab` is listed: `SurlServerComposition.ComposeSocksGssApiLogin` reads it and gives the SOCKS5
  server its GSS-API login.
- `--proxy-auth` is not listed. `ComposeProxyPolicy` puts its words in the proxy policy's accepted methods,
  but the two calls the SOCKS server makes, `AuthenticationPolicy.CheckPasswordLoginAsync` and
  `JudgeKerberosLoginAsync`, never read them. The `socks` AI help already says "`--proxy-auth` does not
  apply to SOCKS".
- Tests: `HelpTextTests.Answer_Socks_ListsTheProxyAccountOptionsAndKeytabButNotProxyAuth` and
  `Answer_AiHelpSocks_ListsTheProxyAccountOptionsAndKeytabButNotProxyAuth`. A comment there that said SOCKS
  logs in by `--proxy-auth` was wrong and is corrected.
- The option pages of `-U`, `--proxy-user-file` and `--keytab` now also name the `socks` category.
- No ADR: category membership follows what the code already does, as the task asked.
- `dotnet build -warnaserror` clean, `dotnet format --verify-no-changes` clean, fast tests 30311 passed,
  0 failed, 3 skipped.
- Follow-up filed: BL-1920 (`--proxy-allow-origin`, `--head-timeout` and others the SOCKS page omits).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. surl --help socks and --aihelp socks list -U, --proxy-user-file and --keytab
