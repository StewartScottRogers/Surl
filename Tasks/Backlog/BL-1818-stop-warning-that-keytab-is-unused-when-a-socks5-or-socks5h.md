---
id: BL-1818
title: Stop warning that --keytab is unused when a socks5 or socks5h listener reads it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1817]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console/CLAUDE.md]
requirement: none
created: 2026-11-26
completed:
---
# BL-1818 — Stop warning that --keytab is unused when a socks5 or socks5h listener reads it

## Goal

A start that serves a `socks5` or `socks5h` listener with `--keytab` no longer warns that `--keytab` is unused.

## Context

- Found while working BL-1809. `KeytabComposition.WriteStartLines` (`Application/Surl.Composition.UnitLibrary/KeytabComposition.cs`) writes `surl: warning: --keytab is unused: --auth accepts neither negotiate nor gssapi` whenever `--auth` names neither word, although `ComposeSocksGssApiLogin` (`SurlServerComposition.Start.cs`) uses the keytab for SOCKS5 whatever `--auth` names.
- Decision: the ADR-0057 amendment BL-1817 records. Follow its list of schemes; look at both `SurlCommandLine.ListenUrls` and `SurlCommandLine.ProxyListenUrls`. `ComposedServing` is the one production caller.
- The warning's text does not change, so the lists that pin it (`AiHelpTextTests`, `CommandLineRunnerKeytabTests`) stay as they are. The manual and `--aihelp` sentences about it are BL-1819's.
- `Application/Surl.Console/CLAUDE.md` describes the warning ("when that is so") and changes in the same diff.
- Composition tests go in `Application/Surl.Composition.UnitTests` (for example a new `KeytabCompositionTests`), parsing a command line with `CommandLineParser.Parse`.
- Stand-in: one start-up log line; no store, event, seed, reset or fault is involved, and no byte on the wire changes.

## Acceptance criteria

- [ ] Named tests in `Surl.Composition.UnitTests` show `WriteStartLines` writes no unused warning for `--keytab` without `--auth` when a `socks5` or `socks5h` listen URL is given, both positionally and with `--proxy`.
- [ ] Named tests show the unused warning is still written when the only listen URLs are `socks4`/`socks4a` ones, or `http` ones, unless the BL-1817 amendment says otherwise for them, in which case the tests follow the amendment.
- [ ] A named test shows each skipped-key warning is still written when a `socks5` listener is served.
- [ ] The XML comment on `WriteStartLines` states the SOCKS condition as the amendment does.
- [ ] The `KeytabComposition.WriteStartLines` sentence in `Application/Surl.Console/CLAUDE.md` states the same condition.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green with no new `Integration` test, and `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-26: Created.
