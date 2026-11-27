---
id: BL-1893
title: Correct the ListenerStatusLine doc comments for a Unix domain socket listen URL
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Output.UnitLibrary/ListenerStatusLine.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1893 — Correct the ListenerStatusLine doc comments for a Unix domain socket listen URL

## Goal

The XML doc comments of `ListenerStatusLine` and its `Write` method say what the code does for a listen URL on a
Unix domain socket as well as for one on TCP.

## Context

Found while working BL-1871 (checked against the code on 2026-11-27). Doc comments only; no behaviour changes. All
in `Foundation/Surl.Output.UnitLibrary/ListenerStatusLine.cs`.

- `Write`'s `<param name="listenUrl">` says "with `ListenUrl.BoundPort` set", and its `<exception>` says it throws
  `ArgumentException` when the URL has no bound port yet. `Write` calls `Format`, which calls
  `FormatBoundListenUrl`; that takes `ListenUrl.Port` when `ListenUrl.UnixSocket` is set, so a Unix domain socket
  URL is written with `BoundPort` unset (`ListenUrl.UnixSocket` documents that it stays unset, ADR-0094,
  decision 6). Only a TCP listen URL with no bound port throws.
- The class `<summary>` gives only `Listening on <scheme>://<host>:<bound port>/`. For a Unix domain socket the line
  carries the port asked for and ends ` over <UnixSocketAddress.UnixUrl>`.
- `FormatBoundListenUrl`'s and `Format`'s `<exception>` comments already say "is on TCP and has no bound port yet";
  `Format`'s `<returns>` already describes the Unix form. Use the same wording.
- The library's `CLAUDE.md` already states both forms. Its `-w` sentence is BL-1890's, not this task's.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [x] `Write`'s `<param name="listenUrl">` says the URL has `BoundPort` set or is on a Unix domain socket.
- [x] `Write`'s `<exception>` says it throws only when the URL is on TCP and has no bound port yet.
- [x] The class `<summary>` also gives the Unix domain socket form: the port asked for, then ` over ` and the
      socket's URL.
- [x] `dotnet build Foundation/Surl.Output.UnitTests -warnaserror` is clean, the fast tests are green, and the only
      changed lines are `///` comment lines in `ListenerStatusLine.cs`.

## Notes

- Edited in the session rather than through `align-and-document`: three comment lines in one file, with the wording
  already given by the task, so a subagent would only add token cost.
- `Write`'s two comments use the same wording as `Format`'s, which `Write` calls.
- Checked 2026-11-27: both builds 0 warnings, 0 errors; fast tests exit 0 with no failures;
  `dotnet format --verify-no-changes --include` on the file exit 0. No ADR: no decision was needed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. ListenerStatusLine doc comments describe both the TCP and the Unix domain socket listen URL forms
