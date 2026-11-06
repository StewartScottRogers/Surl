---
id: BL-939
title: Carry an HTTP/2 request's :scheme on Http2RequestHead and show Surl.Http2's internals to the proxy's tests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-939 — Carry an HTTP/2 request's :scheme on Http2RequestHead and show Surl.Http2's internals to the proxy's tests

## Goal

`Http2RequestHead` in `Surl.Http2` carries the request's `:scheme` pseudo-header (null for a `CONNECT`),
and `Surl.Protocol.HttpProxy.UnitTests` may use `Surl.Http2`'s internal frame codec, so the HTTPS proxy
can forward an HTTP/2 request by its scheme (BL-940).

## Context

- First of the four tasks BL-595 ("Serve the HTTPS proxy over HTTP/2 for --proxy-http2") was split into
  on 2026-11-06 after it timed out at 120 minutes: BL-938 (recorder fix), this, BL-940 (the proxy serves
  HTTP/2), BL-941 (the listener offers `h2`, ADR-0088 Amendment 5).
- A proxy forwards a request by its `:scheme` (RFC 9113 section 8.3.1); a `CONNECT` carries none
  (section 8.5). `Http2RequestHeadMapping` already reads the pseudo-headers but drops `:scheme`.
- The work is written. The stash `darkfactory BL-595 20261106-033059` in the main checkout
  (`Z:\repos\Surl`; find its index N with `git stash list`, matching the message; never pop, apply or
  drop it) holds these four tracked changes, and none of the files has moved on the branch since:
  - `Protocols/Http/Surl.Http2.UnitLibrary/Http2RequestHead.cs` - an init-only `string? Scheme` with its doc comment;
  - `Protocols/Http/Surl.Http2.UnitLibrary/Http2RequestHeadMapping.cs` - sets
    `Scheme = pseudoHeaders.GetValueOrDefault(":scheme")`;
  - `Protocols/Http/Surl.Http2.UnitLibrary/Surl.Http2.UnitLibrary.csproj` - `InternalsVisibleTo
    Surl.Protocol.HttpProxy.UnitTests`, and the comment above it naming both test projects;
  - `Protocols/Http/Surl.Http2.UnitTests/Http2RequestHeadMappingTests.cs` - asserts `"http"` for a GET and null for a CONNECT.
  Take them with `git diff "stash@{N}^" "stash@{N}" -- Protocols/Http/Surl.Http2.UnitLibrary Protocols/Http/Surl.Http2.UnitTests | git apply`.
- `Surl.Http2` is a horizontal library (ADR-0086; `ProtocolIsolationTests.HorizontalLibraries`), so
  the proxy referencing it later needs no ADR-0002 change.
- Stand-in: not applicable; a request-head field, no store, no events.

## Acceptance criteria

- [x] `Http2RequestHeadMappingTests` asserts `head.Scheme` is `"http"` for the mapped GET and null for
      the mapped `CONNECT`, and passes.
- [x] `Surl.Http2.UnitLibrary.csproj` has `<InternalsVisibleTo Include="Surl.Protocol.HttpProxy.UnitTests" />`.
- [x] `dotnet build Protocols/Http/Surl.Http2.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Surl.Http2.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- The stash could not be read from this lane (git stash list was denied), so the four changes were written by hand to the specification above: an init-only `Scheme` on `Http2RequestHead` (kept off the positional parameters so no existing constructor call changes), set by `Http2RequestHeadMapping` from `:scheme`; the `InternalsVisibleTo`; and the two assertions. The projects CLAUDE.md now names the new visible-to project and `Scheme`.
- Coverage: `Http2RequestHead` and `Http2RequestHeadMapping` are 100% line and branch with `Surl.Http2.UnitTests`; the change adds no branch.
- `Surl.Extended.Protocol.Nats.UnitTests` failed one test once under the full fast run and passed in three isolated reruns and a second full run; it does not reference Surl.Http2.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Http2RequestHead carries :scheme (null for CONNECT) and Surl.Protocol.HttpProxy.UnitTests sees Surl.Http2's internals
