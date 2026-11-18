---
id: BL-1374
title: Document SurlExitCode's namespace and the exit-code prefix on SurlServerStartException.Lines in the Surl.Api README
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Api/README.md]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-1374 — Document SurlExitCode's namespace and the exit-code prefix on SurlServerStartException.Lines in the Surl.Api README

## Goal

A reader can compile the `Surl.Api` README's first example as written, and knows each
`SurlServerStartException.Lines` item starts with the exit code in parentheses.

## Context

- From the Surly cookbook bug report of 2026-11-18 (finding 3).
- `Application/Surl.Api/README.md:20` uses `SurlExitCode.Ok`, but `SurlExitCode` lives in namespace
  `Surl.Protocol.Abstractions`, not `Surl.Api`, and the README never says so; the example does not compile without
  `using Surl.Protocol.Abstractions;`.
- `README.md:50` describes the lines as "the lines `surl` writes after `surl: `" but each item really starts with the
  exit code, e.g. `(58) https://127.0.0.1:0/ needs a certificate ...`.

## Acceptance criteria

- [x] The README's first example has `using Surl.Protocol.Abstractions;` (or the README states the namespace beside its first use of `SurlExitCode`).
- [x] The README's `SurlServerStartException` paragraph shows a `Lines` item with its `(58) ` prefix and says each item starts with the exit code in parentheses.
- [x] Every statement changed is checked against `SurlServerStartException.cs` and the code that builds `Lines`.

## Notes

- Docs only; no behaviour change.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing.
- 2026-11-18: Doing -> Done. README example now imports Surl.Protocol.Abstractions; Lines prefix documented, checked against SurlStartFailure sites
