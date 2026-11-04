---
id: BL-386
title: Decide how Surl plays the server half of upstream curl's test cases
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md, Documentation/Wiki/Glossary.md, Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-386 — Decide how Surl plays the server half of upstream curl's test cases

## Goal

An ADR decides how Surl plays the server half of upstream curl's own test cases (tag `curl-8_21_0`,
`tests/data`) with a pinned upstream build as the client, and how the pass rate is stated per release
and kept from falling. BL-387 to BL-397 build what it decides.

## Context

- Scope: the product overview's success criterion 2 ("Upstream's own test cases pass with Surl as the
  server ... The pass rate is stated per release and only rises"), "Also in scope" ("Scripted
  exchanges: the server half of upstream curl's own test cases (`Surl.Conformance`), so curl's suite,
  not Surl's, decides what correct means"), and "Constraints" ("Upstream test data copied in for
  `Surl.Conformance` keeps curl's own notice beside it" and "Clean room is a choice: ... not by
  translating upstream's test servers"). `Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md` says the full push is
  Phase 6.
- The corpus: 2,013 test cases in `tests/data` at tag `curl-8_21_0` (product overview, "Scope",
  counted 2026-09-28). The format is `docs/tests/FILEFORMAT.md` at that tag:
  - Preprocessing: `%b64[]`, `%hex[]`, `%repeat[]`, `%include`, `%if`/`%else`/`%endif`, and variables
    such as `%HOSTIP`, `%HTTPPORT`, `%TESTNUMBER` and `%LOGDIR`.
  - `<reply>`: `<data>`, `<dataNUM>`, `<connect>`, `<datacheck>` and `<servercmd>`. The HTTP commands
    include `auth_required`, `idle`, `stream`, `writedelay`, `skip`, `connection-monitor`, `upgrade`
    and `swsclose`. The FTP, SMTP, POP3 and IMAP commands include `REPLY`, `COUNT`, `DELAY`, `CAPA`,
    `AUTH`, `RETRWEIRDO` and `PASVBADIP`.
  - `<client>`: `<server>` (one of 32 server names), `<features>`, `<tool>` (a libtest program built
    against libcurl), `<precheck>`, `<command>` (with `option=` and `type="perl"` or `"shell"`),
    `<file>` and `<stdin>`.
  - `<verify>`: `<errorcode>`, `<protocol>` with `<strip>` and `<strippart>` (Perl substitutions),
    `<stdout>`, `<stderr>`, `<file>`, `<upload>` and `<postcheck>`.
- How runtests changes each command: `tests/runner.pm` at the tag adds `-q` (unless
  `option="no-q"`, line 975), `--output` (lines 829-832), `--include` (unless `no-include`, line 868)
  and `--trace-ascii` (line 877).
- `<reply>` is canned server bytes, while Surl is a real server. The ADR must decide what "plays the
  server half" means, so the pass rate measures Surl and not a replay engine. Candidate meanings:
  surl's own protocol servers answering with the case's reply as their content; a scripted-reply
  mode in surl; or serving the case's files from the content store and judging only curl's side.
  Rule 1 of the overview's architecture applies: `Surl.Conformance` depends on Abstractions, and no
  protocol server references another.
- Only pinned builds run (ADR-0003). Their `features` lines decide which `<features>` cases can run.

Decide and record:

1. Where the upstream test data lives in the repository, how it is fetched (from tag `curl-8_21_0`,
   commit `68720b48`), its notice (curl's `COPYING`), and its line endings (`crlf=` attributes need
   exact bytes, so `.gitattributes` must not normalise it).
2. What "Surl plays the server half" means for each server name in FILEFORMAT's `<server>` list, and
   where that code lives (in a protocol library, `Surl.Conformance`, or a new library).
3. Which cases are out of scope and why, each class with its skip reason: `<tool>`/libtest,
   `unittest`, `debug`-only `<features>`, `type="perl"` and `type="shell"`, `<precheck>`, `<postcheck>`,
   features a pinned build lacks, and servers not built yet (for example `http-proxy`, `socks5`,
   `https-proxy`).
4. How `<strippart>` Perl substitutions are applied without Perl (base class library only, no
   Python).
5. The pass rate: the file it lives in, per platform and per release, how a release states it, and the
   test that fails when a case that passed stops passing.
6. Where the suite runs: CI legs, its time budget, and whether lanes run it.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked
      "Decided by Claude under Stewart's delegation". It answers decisions 1 to 6 and is listed in
      that folder's `README.md`.
- [ ] `Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md` states what the ADR decides the library holds.
- [ ] `Documentation/Product/Requirements.md` has a requirement row for success criterion 2 that names
      the ADR and the pass-rate file.
- [ ] `Documentation/Wiki/Glossary.md` defines "upstream test case", "server half" and "pass rate" as
      the ADR uses them.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
