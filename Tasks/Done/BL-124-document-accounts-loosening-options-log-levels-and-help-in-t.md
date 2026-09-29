---
id: BL-124
title: Document accounts, loosening options, log levels and help in the glossary, README and project CLAUDE.md files
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-118, BL-123]
touches: [Documentation/Wiki, README.md, Documentation/Product/Product-Overview.md, Surl.Authentication.UnitLibrary/CLAUDE.md, Surl.Output.UnitLibrary/CLAUDE.md, Surl.Console/CLAUDE.md, Surl.Cli.UnitLibrary/CLAUDE.md]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
---
# BL-124 — Document accounts, loosening options, log levels and help in the glossary, README and project CLAUDE.md files

## Goal

The glossary, the root README, the product overview and the `CLAUDE.md` files of
`Surl.Authentication.UnitLibrary`, `Surl.Output.UnitLibrary`, `Surl.Console` and
`Surl.Cli.UnitLibrary` state, truly of the code as it then is, secure-by-default
authentication, the loosening options, `--self-signed`, the log levels and the categorised
help.

## Context

ADR-0032 (BL-100), ADR-0033 (BL-101), ADR-0034 (BL-102); the behaviour landed in BL-103 to
BL-118 and BL-123. `align-and-document` owns this ("Say what it does, do what it says"): one
concept, one name, the glossary's.

- `Documentation/Wiki/Glossary.md`: add the terms the ADRs introduce (for example account,
  loosening option, plain-text secret, log level, trace dump, throwaway certificate as
  `--self-signed` now makes it) with the names the code uses.
- `README.md`: the quick-start examples still work (any `https` example needs `--cert` or
  `--self-signed`); a short "Logging in" and "Logging" section.
- `Documentation/Product/Product-Overview.md`: "Also in scope" (authentication, "Seeing the
  exchange") and the "Layers" row for `Surl.Authentication` if ADR-0032 decision 7 let it
  reference `Surl.Cryptography`.
- The four `CLAUDE.md` files describe what each project now holds (e.g.
  `Surl.Console/CLAUDE.md` lists the `--user-file` read, the warnings and the log
  composition; `Surl.Authentication.UnitLibrary/CLAUDE.md` names the methods present and
  that Kerberos is later work).
- NTLM, Negotiate and SigV4 (BL-120 to BL-122) may land after this task; describe only what
  exists when it runs, and each of those tasks updates `Surl.Authentication.UnitLibrary/CLAUDE.md`
  itself.

## Acceptance criteria

- [x] `Documentation/Wiki/Glossary.md` defines each new term once, and
      `rg -n` for each term's retired synonym (if any) across `*.md` and `*.cs` finds nothing.
- [x] Every command line in `README.md` runs as written against the code (checked by
      running each with `dotnet run --project Surl.Console -- ...` or stating in Notes why
      one cannot run in the session).
- [x] The four `CLAUDE.md` files and the product overview name only types, options and files
      that exist (`rg` each name).
- [x] No behaviour change: `git diff --stat` shows only `*.md` files; `dotnet build` and the
      fast tests still pass.

## Notes

- Delivered by `align-and-document`. Changed only `*.md` files: 7 files, 233 lines added, 51 removed.
- Glossary additions:
  - Authentication: account, user file, anonymous read, authentication method,
    authentication policy, plain-text secret, refusal delay, loosening option, startup
    warning, throwaway certificate.
  - New Logging section: log level, log stream, info line, trace dump, trace time stamp.
  - New Help section: help subject, help category, short list, option page, manual.
- Glossary corrections:
  - "negatable option" now names `CommandLineOption.Negatable`.
  - "verbose exchange log" now says the log stream (`LevelledExchangeLogFactory`), not stderr.
  - "listener status line" is written from the info level up.
- Retired synonyms: `VerboseExchangeLogFactory`, `bool Verbose`, `HelpText.Text` and
  "served directory" appear in no `.cs` file and no live document. They appear only in
  ADRs, as dated history. ADR-0028 line 27 still names `VerboseExchangeLogFactory` in its
  decision text, and BL-138 adds a dated note there.
- README commands:
  - Every surl command ran with `dotnet run --project Surl.Console -- ...`, and each paired
    curl line ran with pinned curl 8.21.0 through `Record-CurlExchange.ps1 -NoServer`.
  - Cases checked: https with `--cert`/`--key` and `--cacert`; `--self-signed` with `-k`;
    `--user-file` with Digest; `--user` over https; `-v --trace-time`; `--trace-ascii -`;
    `--log-file`; `--help` with each subject; `--manual`; `--version`. All exited 0.
  - Not run: `RunDarkFactory.cmd -Hours 4 -MaxTasks 3`, because it starts a factory shift
    (its parameters exist in `RunDarkFactory.ps1`). Also not run: the `install.sh` and
    `install.ps1` one-liners, because no release exists yet and they download and run
    remote scripts.
- Defaults taken:
  - The quick start uses `127.0.0.1`, not `0.0.0.0`, as the manual's deployment checklist
    advises.
  - It uses `--directory site` with a `hello.txt`, so the example fetch succeeds.
- The product overview's Layers row says `Surl.Authentication.UnitLibrary` references
  `Surl.Cryptography.UnitLibrary`, which its csproj confirms. Parts not built yet (Kerberos
  in Negotiate, `Proxy-Authenticate`, `-w`) are marked "in scope, not built yet".
- Name check: the 290 backticked identifiers in the edited files were checked against the
  tracked `.cs` files, and every type and member exists.
- Follow-ups filed:
  - BL-137: the `--auth` help says a method is refused but every word is served now, and
    the manual's exit code 2 text is stale.
  - BL-138: the `AllowPlaintextAuth`/`AllowPlaintextAuthentication` name split, the
    `SilentExchangeLog` doc comment, and the ADR-0028 note.
- Build is clean. Fast tests: 0 failed.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Glossary, README, product overview and four project CLAUDE.md files now describe accounts, loosening options, --self-signed, log levels and categorised help as the code has them
