# Surl.Conformance.UnitLibrary

Phase 1 onward; the full push is Phase 6.

The server half of upstream curl's own test suite. Upstream curl 8.21.0 ships 2,013 test
cases in `tests/data` (tag `curl-8_21_0`, counted 2026-09-28); each names a command line,
the server's scripted replies and what to verify. This library parses those cases, plays
each case's replies from Surl while a pinned upstream curl build runs the case's command
line, and checks the case's verify section - so upstream curl's own suite, not one of
Surl's invention, decides whether Surl answers correctly.

Only builds pinned in `UpstreamCurlBuilds.json` run here, never the Curl port (ADR-0003).
`UpstreamCurlBuildPins.Parse` reads the pin file and `UpstreamCurlLocator` finds the
pinned build for a platform and role (`Locate`) or for the protocol a test measures
(`LocateForProtocol`: the reference build when it supports the protocol, else the first
supplementary build that does), or refuses any curl whose SHA-256 is not pinned.
Get every curl you run through it.
Each pin has a `Kind`: `Curl` (the default) or `Library`, a shared libcurl such as the
reference build's `libcurl-4.dll` (ADR-0071 decision 10). `Locate`, `LocateForProtocol` and
`RequirePinned` only ever answer with a curl; `LocateLibrary` and `RequirePinnedLibrary` only
with a library, which `Run-LibcurlWebSocketScript.cs` at the repository root loads to drive
`curl_ws_send` and `curl_ws_recv`. `UpstreamCurlRunner` refuses a library.
`UpstreamCurlRunner` takes the locator's `UpstreamCurlLocation` - never a path - so the
only curl it starts is a verified pin; it passes arguments through
`ProcessStartInfo.ArgumentList`, writes the given standard input bytes (none by default)
all at once and closes stdin, as `Record-CurlExchange.ps1` does, sets any environment
variables it is given (the SSH tests point `HOME` and `USERPROFILE` at a temporary directory,
ADR-0051 decision 8), and returns an `UpstreamCurlRunResult`
(exit code, stdout bytes, stderr text), killing curl at its timeout.
Parsing and matching are unit tested with no process and no network; a test that starts
upstream curl is `[TestCategory("Integration")]`. Upstream test data copied into this
repository keeps curl's `COPYING` notice beside it.
