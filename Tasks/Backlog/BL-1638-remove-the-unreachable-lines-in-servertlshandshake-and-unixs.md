---
id: BL-1638
title: Remove the unreachable lines in ServerTlsHandshake and UnixSocketConnectionListener
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-24
completed:
---
# BL-1638 — Remove the unreachable lines in ServerTlsHandshake and UnixSocketConnectionListener

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary` reports 100% line and 100% branch with no failing
member, without any new coverage exclusion.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Surl.Networking.UnitLibrary
  99.88% line, 100% branch, 2 failing members. Neither gap can be closed by a test, because each line is unreachable as
  written.
- `async ServerTlsHandshake.AuthenticateTls13OnlyWithHandBuiltStreamAsync`,
  `Foundation/Surl.Networking.UnitLibrary/ServerTlsHandshake.cs:100`, 93.75% line, Cx 7, CRAP 7.01. The uncovered
  line is 119, the closing brace after `await RefuseWithProtocolVersionAlertAsync(...)`. That method (line 127) always
  ends by throwing `TlsHandshakeException`, so control never comes back to line 119. Making the throw visible at the
  call site (for example a method that writes the alert and *returns* the exception, used as
  `throw await ...`) removes the dead line.
- `UnixSocketConnectionListener..cctor()`, `UnixSocketConnectionListener.cs:274`, 0% line:
  `private static readonly TimeSpan RetryPause = TimeSpan.FromMilliseconds(50);`. The type is `beforefieldinit`, and the
  field is read only at line 267, in an excluded accept-retry path that only a runtime failure to copy a peer's address
  reaches. So the static initializer never runs in a test. A `const` in milliseconds, or a value held by the instance,
  removes the static constructor.

## Acceptance criteria

- [ ] `ServerTlsHandshake` has no statement after the protocol_version refusal that control cannot reach. The existing
      TLS 1.3-only refusal tests still pass unchanged: same alert bytes `15 03 03 00 02 02 46`, same
      `TlsHandshakeException` message.
- [ ] `UnixSocketConnectionListener` has no static constructor, and the retry pause is still 50 ms.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary` reports `100 | 100`
      with 0 failing members, and no `[ExcludeFromCodeCoverage]` was added.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- No behaviour change. BL-1639 depends on this task, because both edit `UnixSocketConnectionListener.cs`.

## Log

- 2026-11-24: Created.
