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
completed:
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

- [ ] `Documentation/Wiki/Glossary.md` defines each new term once, and
      `rg -n` for each term's retired synonym (if any) across `*.md` and `*.cs` finds nothing.
- [ ] Every command line in `README.md` runs as written against the code (checked by
      running each with `dotnet run --project Surl.Console -- ...` or stating in Notes why
      one cannot run in the session).
- [ ] The four `CLAUDE.md` files and the product overview name only types, options and files
      that exist (`rg` each name).
- [ ] No behaviour change: `git diff --stat` shows only `*.md` files; `dotnet build` and the
      fast tests still pass.

## Notes

## Log

- 2026-09-29: Created.
