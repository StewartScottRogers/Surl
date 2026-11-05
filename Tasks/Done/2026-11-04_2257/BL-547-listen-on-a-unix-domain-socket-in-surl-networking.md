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
completed: 2026-11-04
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

- [x] `Surl.Networking.UnitTests` has tests that start a Unix socket listener in a temporary
      directory, connect to it with a client `Socket` in the test, exchange bytes through the
      accepted `IConnection`, and stop it, checking that the socket file is created and removed as
      the ADR says. Tests that need `AF_UNIX` on the platform are marked as the ADR says, and none
      needs `TestCategory=Integration`.
- [x] A socket file already present, a path longer than the platform's limit, and an abstract name
      on a platform without them each fail with the `ListenerBindFailure` and `SurlExitCode` the
      ADR names, each with its own named test.
- [x] The accepted connection's `ExchangeContext` describes a Unix socket peer as the ADR decides,
      with a named test in the library that owns the type.
- [x] `dotnet build Foundation/Surl.Networking.UnitLibrary -warnaserror` and the Abstractions
      library's build are clean, the fast tests are green on Windows, and both libraries keep 100%
      line and branch coverage.

## Notes

- No path in a test is Windows-only; temporary paths come from `Path.GetTempPath()` and stay under
  the platform's `sun_path` limit (root `CLAUDE.md`, "Tests pass on Windows, Linux and macOS").
- Delivered (2026-11-04): `UnixSocketAddress`, `ListenUrl.UnixSocket`, `UnnamedUnixSocketPeer`,
  `ListenerBindFailure.UnixSocketPathTooLong` and `.AbstractUnixSocketUnsupported` in
  Abstractions; `UnixSocketConnectionListener`, `UnixSocketBindRules`, `UnixSocketFileEndPoint`
  and `SocketListenerFactory`'s Unix branch in Networking. `Surl.Core`'s `MapBindFailure`
  already maps every failure but `HostNotFound` to `BindFailed` (45), so it needed no change.
- Choices within ADR-0094, taken as sensible defaults:
  - **.NET deletes the socket file itself.** Measured: `Socket.Dispose` on a socket bound to a
    `UnixDomainSocketEndPoint` deletes the file at its path even after it was replaced by
    someone else's file, which breaks decision 5 ("only the file it created"). The bind goes
    through `UnixSocketFileEndPoint`, an `EndPoint` serializing to the same address, so .NET
    does not own the file, and the listener deletes it only while its last-write time is still
    the one read right after the bind. Test:
    `DisposeAsync_PathReplacedByAnotherFile_LeavesThatFileAlone`.
  - **Missing directory checked before binding.** Windows reports it as `NetworkDown` (the
    ADR's measurement), which `BindFailureClassifier` would call `Other`; mapping `NetworkDown`
    globally would change TCP too, so `UnixSocketBindRules` finds a missing parent directory
    before binding and calls it `AddressNotAvailable`, as decision 5 asks.
  - **Unnamed peer detection.** Windows reports an unnamed client as a
    `UnixDomainSocketEndPoint` printing `@` followed by NULs/blanks; an endpoint whose name is
    empty once a leading `@`, NULs and blanks are trimmed, or none at all, is
    `UnnamedUnixSocketPeer.Instance`.
  - **No `NoDelay`** on accepted Unix sockets: Windows refuses the option for `AF_UNIX`.
  - `ListenerBindException`'s message adds ` over unix://<path>` (or `unix://@<name>`) for a Unix
    socket listen URL; the start-up line and the console's wording are BL-548's.
- Tests: new `UnixSocketBindRulesTests`, `UnixSocketFileEndPointTests` and
  `UnixSocketConnectionListenerTests` (real sockets, fast; the `0600` test runs off Windows, the
  abstract-name tests split Linux / not Linux) in `Surl.Networking.UnitTests`, and
  `UnixSocketAddressTests`, `UnnamedUnixSocketPeerTests` and new cases in `ListenUrlTests`,
  `ListenerBindExceptionTests` and `ExchangeContextTests`. Both libraries 100% line and branch
  coverage (`Measure-CodeQuality.ps1 -SkipTestRun` over the two test projects' Cobertura).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.Networking listens on a Unix socket file (and a Linux abstract name) through IListenerFactory, per ADR-0094
