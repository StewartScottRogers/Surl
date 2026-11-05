---
id: BL-547
title: Listen on a Unix domain socket in Surl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-571]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-547 — Listen on a Unix domain socket in Surl.Networking

## Goal

`IListenerFactory.StartConnectionListenerAsync` starts a connection listener on a Unix domain
socket path (and, on Linux, an abstract name) when the listen request says so, as BL-571's ADR
decides, and every connection-oriented protocol server runs on it unchanged.

## Context

- Decision: BL-571's ADR. It fixes how a listen request names a Unix socket, the socket file's
  creation, permissions and deletion, abstract names, Windows `AF_UNIX`, and each failure's
  `SurlExitCode`. Follow it where it differs from this task.
- Code: `Foundation/Surl.Protocol.Abstractions.UnitLibrary/ListenUrl.cs` and `IListenerFactory.cs`
  (the contract; ADR-0004 typed endpoints as `EndPoint` for this), and in
  `Foundation/Surl.Networking.UnitLibrary` `SocketListenerFactory`, `ListenerBinder`,
  `BindFailureClassifier` and `TcpConnectionListener`. The BCL type is `UnixDomainSocketEndPoint`.
  Only `Surl.Networking` constructs the `Socket`.
- Upstream test cases that wait on this listener: `http-unix` 1435, 1436 (BL-549) and
  `socks5unix` 1467, 1468, 1470 (BL-562), at tag `curl-8_21_0`.

## Acceptance criteria

- [ ] `Surl.Networking.UnitTests` has tests that start a Unix socket listener in a temporary
      directory, connect to it with a client `Socket` in the test, exchange bytes through the
      accepted `IConnection`, and stop it, checking that the socket file is created and removed as
      the ADR says. Tests that need `AF_UNIX` on the platform are marked as the ADR says, and none
      needs `TestCategory=Integration`.
- [ ] A socket file already present, a path longer than the platform's limit, and an abstract name
      on a platform without them each fail with the `ListenerBindFailure` and `SurlExitCode` the
      ADR names, each with its own named test.
- [ ] The accepted connection's `ExchangeContext` describes a Unix socket peer as the ADR decides,
      with a named test in the library that owns the type.
- [ ] `dotnet build Foundation/Surl.Networking.UnitLibrary -warnaserror` and the Abstractions
      library's build are clean, the fast tests are green on Windows, and both libraries keep 100%
      line and branch coverage.

## Notes

- No path in a test is Windows-only; temporary paths come from `Path.GetTempPath()` and stay under
  the platform's `sun_path` limit (root `CLAUDE.md`, "Tests pass on Windows, Linux and macOS").

## Log

- 2026-11-04: Created.
