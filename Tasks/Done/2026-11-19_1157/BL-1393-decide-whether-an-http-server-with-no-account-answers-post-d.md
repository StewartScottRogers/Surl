---
id: BL-1393
title: Decide whether an HTTP server with no account answers POST, DELETE and PATCH 401 or 405, and align the server and --aihelp http
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Application/Surl.Cli.UnitLibrary/AiHelpProse.cs, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-1393 — Decide whether an HTTP server with no account answers POST, DELETE and PATCH 401 or 405, and align the server and --aihelp http

## Goal

`surl --directory site http://127.0.0.1:8080/` answers POST, DELETE and PATCH the way `surl --aihelp http` says,
and the order of the login check and the method check is recorded in an ADR.

## Context

- Surly cookbook bug report 2026-11-19 06:42, finding 1 (commit `993aa7072`; the client was the Curl port, so
  re-check with pinned upstream curl).
- Observed: with no `--user` and no `--allow-anonymous`, `curl -X POST -d x .../hello.txt` and `curl -X DELETE ...`
  get `401 Unauthorized` with Digest challenges.
- `--aihelp http` (`Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`, near line 179) says: "POST, PUT, DELETE,
  CONNECT, OPTIONS, TRACE and PATCH are answered 405 Method Not Allowed with `Allow: GET, HEAD`; under `--...`".
- Secure by default (ADR-0032): an unauthenticated client should learn as little as possible, which argues for
  login before method. The cookbook recipe `http-serve-an-empty-store` claims `401` for DELETE and will follow
  whichever is chosen.

## Acceptance criteria

- [x] An ADR in `Documentation/Planning/Decisions` ("Decided by Claude under Stewart's delegation") fixes the order:
      login check before or after the method check, for a server with and without accounts, and with
      `--allow-anonymous`.
- [x] The HTTP server and the `--aihelp http` paragraph agree with the ADR, in the same diff; any `AiHelpExamples`
      entry showing a refused method is updated, and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` stays green.
- [x] Named fast tests in `Surl.Protocol.Http.UnitTests` pin POST, DELETE and PATCH on a server without
      `--allow-anonymous` (status and, for 405, the `Allow` header; for 401, the challenge).
- [x] Named fast tests pin the same three methods under `--allow-anonymous` without `--allow-uploads` (405 with
      `Allow: GET, HEAD` expected unless the ADR says otherwise).
- [x] `dotnet build` clean, fast tests green, coverage gates held.

## Notes

- Default leaning: keep 401 first (secure by default) and correct the help.
- Decided (ADR-0180): login before method, as ADR-0032 s4 already had it; server unchanged, help corrected, tests in HttpMethodLoginOrderTests. Not measured against curl: no bytes changed.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
- 2026-11-19: Doing -> Done. HTTP login-before-method order recorded in ADR-0180, aihelp http corrected, 401/405 tests pin POST/DELETE/PATCH
