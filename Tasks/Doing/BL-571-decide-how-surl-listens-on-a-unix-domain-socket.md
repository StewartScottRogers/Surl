---
id: BL-571
title: Decide how Surl listens on a Unix domain socket
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed:
---
# BL-571 — Decide how Surl listens on a Unix domain socket

## Goal

An ADR decides how `surl` is asked to listen on a Unix domain socket instead of a TCP port, so
upstream curl's `--unix-socket` and `--abstract-unix-socket`, and a SOCKS5 proxy reached over a
Unix socket, have a server to talk to.

## Context

- Upstream test cases that wait on this (tag `curl-8_21_0`, commit
  `68720b4837284335b2d63cb358f8f6ce65f5bc55`, classified with ADR-0080's rules by BL-399):
  - `http-unix` (2): 1435, 1436 - feature `UnixSockets`, command `--unix-socket %HTTPUNIXPATH`;
  - `socks5unix` (3): 1467, 1468, 1470 - features `proxy`, `UnixSockets`, `!win32`, command
    `--socks5 localhost%SOCKSUNIXPATH` (BL-557's SOCKS server listens on what this ADR decides).
- ADR-0004 typed every endpoint as `System.Net.EndPoint` so that a Unix domain socket listen URL
  fits later without changing a contract; `ListenUrl` and `IListenerFactory` live in
  `Foundation/Surl.Protocol.Abstractions.UnitLibrary`, and `ListenerBinder`,
  `SocketListenerFactory` and `TcpConnectionListener` in `Foundation/Surl.Networking.UnitLibrary`.
  `Application/Surl.Cli.UnitLibrary/ListenUrlParser.cs` parses today's listen URLs.
- What curl does: https://curl.se/docs/manpage.html, `--unix-socket` and `--abstract-unix-socket`
  (curl 8.21.0). The BCL has `UnixDomainSocketEndPoint`; Windows 10 1803 and later support
  `AF_UNIX` for stream sockets, but not abstract names.
- Only `Surl.Networking` constructs sockets (root `CLAUDE.md`).

Decide and record:

1. The command-line form. Planning assumption: an option `--unix-socket <path>` beside the listen
   URL, mirroring curl's option name, where the URL still names the scheme (and the `Host` the
   server expects); and `--abstract-unix-socket <name>` on Linux only.
2. Which servers may listen on a Unix socket (planning assumption: every connection-oriented
   server; datagram servers refuse it with a named exit code).
3. Abstract sockets: Linux only; what Windows and macOS answer (planning assumption: refused with
   `SurlExitCode.UnsupportedProtocol` before binding).
4. Windows `AF_UNIX`: supported or refused, measured on Stewart's Windows machine, and what the
   Windows reference pin's `features` line says about `UnixSockets` (ADR-0016 says the Linux and
   macOS pins report it).
5. The socket file: who creates and deletes it, what happens when the path exists already
   (planning assumption: a stale socket file is refused with `SurlExitCode.BindFailed`, never
   deleted silently), its permissions, and the maximum path length per platform
   (`sockaddr_un.sun_path`) with the exit code for a longer one.
6. How the exchange log and the start-up line name a Unix socket endpoint, and how
   `ExchangeContext` reports a peer that has no IP address.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked
      "Decided by Claude under Stewart's delegation", answering decisions 1 to 6, each failure
      naming its `SurlExitCode`, and it is listed in the folder's `README.md`.
- [ ] The ADR names the cases that wait on it (1435, 1436, 1467, 1468, 1470) and states on which
      reference pins each can run.
- [ ] The ADR's "Consequences" name BL-547, BL-548, BL-549, BL-561 and BL-562 as the tasks that
      act on it.

## Notes

- Planning assumptions above are BL-399's defaults for an unattended plan; the ADR may change any
  of them and the build tasks follow the ADR.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
