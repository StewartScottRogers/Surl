---
id: BL-270
title: Say in the product docs that surl serves --allow-weak-ssh-algorithms
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-250]
touches: [Documentation/Product/Product-Overview.md, Documentation/Product/Requirements.md, Documentation/Wiki/Glossary.md, README.md]
requirement: FR-039
created: 2026-09-30
completed:
---
# BL-270 — Say in the product docs that surl serves --allow-weak-ssh-algorithms

## Goal

The product overview, requirements, glossary and README say that `surl --allow-weak-ssh-algorithms` offers ADR-0051 decision 2's weak SSH algorithms and warns, instead of saying it is refused with `surl: (2) --allow-weak-ssh-algorithms is not available in this build`.

## Context

- BL-250 made `Surl.Console` serve the option: `SshAlgorithmOffer.Default` gets `allowWeakAlgorithms`, and `SshHostKeyComposition.WriteStartLines` writes `surl: warning: --allow-weak-ssh-algorithms: SHA-1, MD5, CBC, RC4, 3DES and 1024-bit Diffie-Hellman SSH algorithms are offered` from the info level up (ADR-0051 decision 11). `--hostcert` is still refused until BL-222.
- BL-250 could not edit these files: BL-214, in Doing at the time, named them in its `touches`.
- Stale text today: `Documentation/Product/Product-Overview.md` (around line 156), `Documentation/Product/Requirements.md` FR-039 ("Not served by surl: the weak algorithms"), `Documentation/Wiki/Glossary.md` row "weak SSH algorithm" ("refused by CommandLineRunner.FindUnavailableOption"), `README.md` (around line 155).

## Acceptance criteria

- [ ] `git grep "allow-weak-ssh-algorithms is not available"` finds nothing outside `Tasks/`.
- [ ] FR-039, the glossary's "weak SSH algorithm" row and the README say the option offers the weak algorithms and writes decision 11's warning, and still say `--hostcert` is refused.

## Notes

## Log

- 2026-09-30: Created.
