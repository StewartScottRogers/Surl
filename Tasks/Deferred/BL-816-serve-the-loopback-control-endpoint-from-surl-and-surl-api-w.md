---
id: BL-816
title: Serve the loopback control endpoint from surl and Surl.Api when --control is given
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-814, BL-815, BL-805]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-816 — Serve the loopback control endpoint from surl and Surl.Api when --control is given

## Goal

With `--control` given, `surl` (and a `Surl.Api` server given the option through `AdditionalArguments`) binds the
control endpoint on its loopback listen URL beside the protocol listeners, reads its token, warns that it is on,
reports its bound URL, and answers it against the instance's own event stream, fault plan and stores.

## Context

- Decision: BL-822's ADR decision 7 (the status line or note naming the bound control URL, the start-up warning
  text, what an unreadable token file does - planning `CouldNotReadFile` (37) - and whether the control listener
  counts toward `--max-connections`).
- Built on: BL-814 (`Surl.Control`, its `IControlTarget` and request handler), BL-815 (`SurlCommandLine`'s control
  members), BL-803 and BL-805 (`ComposedServing`'s stream, fault plan and store control).
- Where: `Application/Surl.Composition.UnitLibrary`: add the project reference to
  `Application/Surl.Control.UnitLibrary`; implement `IControlTarget` over `ComposedServing`; read the token file
  through the `ReadStartFile` seam (`SurlServerCompositionSeams`), in the ordered start before any listener binds;
  bind the control listener through the same `IListenerFactory` so tests use `FakeListenerFactory`; write the
  warning with the other start-up warnings in `ComposedServing.ServeAsync`. The control endpoint's own exchanges
  are not published on the stream they serve (so watching the stream does not record the watching), unless the
  ADR says otherwise.
- Update `Application/Surl.Composition.UnitLibrary/CLAUDE.md`'s ordered-start table.
- Stand-in: the control endpoint, live. BL-819 proves it with pinned upstream curl.

## Acceptance criteria

- [ ] Named tests over `FakeListenerFactory` (no network): with `--control http://127.0.0.1:0` and a token file,
      a fake connection to the control listener with the token gets `GET /v1/exchanges` listing a fake HTTP
      exchange served on the same instance; a `POST /v1/faults` drop rule then drops the next HTTP connection;
      `POST /v1/reset` empties a seeded file; a request without the token is refused.
- [ ] Named tests: an unreadable token file refuses the start with the ADR's exit code and text before any
      listener binds; the start-up warning is written byte for byte; without `--control` no control listener
      exists.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
<<<<<<<< HEAD:Tasks/Blocked/BL-816-serve-the-loopback-control-endpoint-from-surl-and-surl-api-w.md
- 2026-11-06: Doing -> Blocked. Stewart: Surl dark factory timed out after 120 min; see Z:\repos\Surl.logs\BL-816-20261106-033059-L7.jsonl
========
- 2026-11-06: Doing -> Deferred. Split into BL-957, BL-964, BL-972, BL-977 (timed out after 120 min; Stewart asked for the split 2026-11-06)
>>>>>>>> 39b53578 (chore(tasks): split BL-545, BL-816, BL-834 after timeouts, and 16 oversized Backlog tasks into 44 (60-minute size audit)):Tasks/Deferred/BL-816-serve-the-loopback-control-endpoint-from-surl-and-surl-api-w.md
