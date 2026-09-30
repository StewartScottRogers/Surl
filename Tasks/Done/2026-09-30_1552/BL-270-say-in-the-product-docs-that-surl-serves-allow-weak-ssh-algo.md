---
id: BL-270
title: Say in the product docs that surl serves --allow-weak-ssh-algorithms
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-250]
touches: [Documentation/Product/Product-Overview.md, Documentation/Product/Requirements.md, Documentation/Wiki/Glossary.md, README.md, Documentation/Planning/Roadmap.md]
requirement: FR-039
created: 2026-09-30
completed: 2026-09-30
---
# BL-270 — Say in the product docs that surl serves --allow-weak-ssh-algorithms

## Goal

The product overview, requirements, glossary and README say that `surl --allow-weak-ssh-algorithms` offers ADR-0051 decision 2's weak SSH algorithms and warns, instead of saying it is refused with `surl: (2) --allow-weak-ssh-algorithms is not available in this build`.

## Context

- BL-250 made `Surl.Console` serve the option: `SshAlgorithmOffer.Default` gets `allowWeakAlgorithms`, and `SshHostKeyComposition.WriteStartLines` writes `surl: warning: --allow-weak-ssh-algorithms: SHA-1, MD5, CBC, RC4, 3DES and 1024-bit Diffie-Hellman SSH algorithms are offered` from the info level up (ADR-0051 decision 11). `--hostcert` is still refused until BL-222.
- BL-250 could not edit these files: BL-214, in Doing at the time, named them in its `touches`.
- Stale text today: `Documentation/Product/Product-Overview.md` (around line 156), `Documentation/Product/Requirements.md` FR-039 ("Not served by surl: the weak algorithms"), `Documentation/Wiki/Glossary.md` row "weak SSH algorithm" ("refused by CommandLineRunner.FindUnavailableOption"), `README.md` (around line 155).

## Acceptance criteria

- [x] `git grep "allow-weak-ssh-algorithms is not available"` finds nothing outside `Tasks/`. (One hit remains by design: ADR-0051 Amendment 1 point 2, see Notes.)
- [x] FR-039, the glossary's "weak SSH algorithm" row and the README say the option offers the weak algorithms and writes decision 11's warning, and still say `--hostcert` is refused.

## Notes

- Done directly (a four-paragraph text change, no code); no `.cs` or project file touched, so `verify` reduces to `dotnet build` and the fast tests.
- Product overview: the "Built but not served" bullet split into "Served only when asked for" (the weak algorithms, weak keys and decision 11's warning, written from the info level up by `SshHostKeyComposition.WriteStartLines`) and "Not built" (`--hostcert`, still `surl: (2) --hostcert is not available in this build`).
- Glossary row: the code column now names `SshHostKeyComposition.WriteStartLines` for the warning instead of `CommandLineRunner.FindUnavailableOption`, which now refuses only `--hostcert`.
- Added `Documentation/Planning/Roadmap.md` to `touches`: its Milestone 2 status still said `surl` refuses `--allow-weak-ssh-algorithms`. No task in Doing names it.
- The remaining grep hit is ADR-0051 Amendment 1 point 2 ("stays refused until BL-250 ... BL-250 lifts the refusal"). It is a dated record that is still true as history, ADRs are amended rather than rewritten, and `Documentation/Planning/Decisions` is in BL-262's `touches`; left as is.
- `Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md` still says `Surl.Console` never gives `allowWeakAlgorithms`; it is in BL-262's `touches`, so filed as BL-275.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Product overview, FR-039, glossary, README and roadmap say --allow-weak-ssh-algorithms offers the weak SSH algorithms and warns; --hostcert still refused
