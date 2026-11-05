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
`curl_ws_send` and `curl_ws_recv`, and `Run-LibcurlRtspScript.cs` loads to drive RTSP requests
(`CURLOPT_RTSP_REQUEST`) and the interleaved receive (ADR-0074 decision 12); the tests run both
through `PinnedLibcurlDriver`.
`UpstreamCurlRunner` refuses a library. `PinnedLibcurlChoice.Choose` decides which file a
driver may load before anything is loaded; `LibcurlRtspScript`, `LibcurlRtspReport` and
`LibcurlBytes` are the RTSP driver's command line, its output lines and the byte escapes,
kept here so they are unit tested while the driver itself only calls libcurl.
`UpstreamCurlRunner` takes the locator's `UpstreamCurlLocation` - never a path - so the
only curl it starts is a verified pin; it passes arguments through
`ProcessStartInfo.ArgumentList`, writes the given standard input bytes (none by default)
all at once and closes stdin, as `Record-CurlExchange.ps1` does, sets any environment
variables it is given (the SSH tests point `HOME` and `USERPROFILE` at a temporary directory,
ADR-0051 decision 8), and returns an `UpstreamCurlRunResult`
(exit code, stdout bytes, stderr text), killing curl at its timeout.
The Curl port's client has its own types, never shared with upstream curl's (ADR-0083
decision 2), and runs only in the extended servers' `Integration` tests, never for an original
protocol and never as an expected value. `CurlPortClientPins.Parse` reads `CurlPortBuilds.json`
(platform, path, sha256, commit, publishCommand, published) into `PinnedCurlPortClient`s;
`CurlPortClientLocator.Locate` expands `%NAME%` variables in the path, never returns a path
`UpstreamCurlBuilds.json` lists, and refuses a file whose SHA-256 is not its pin, or is an
upstream pin's, with `UnpinnedCurlPortClientException`; `CurlPortClientRunner` takes its
`CurlPortClientLocation` and returns a `CurlPortClientRunResult`. A missing client is a
`CurlPortClientUnavailability`, which the tests' `PinnedCurlPortClientOnThisMachine` turns
into an inconclusive test.
Parsing and matching are unit tested with no process and no network; a test that starts
upstream curl is `[TestCategory("Integration")]`. Upstream test data copied into this
repository keeps curl's `COPYING` notice beside it.

## Upstream test cases (ADR-0080)

ADR-0080 decides how Surl plays the server half of upstream curl's test cases; BL-387 to BL-397
build it, so until they land this section is intent. This library holds, with no reference but
Abstractions:

- parsing a case file into an `UpstreamTestCase` (planning name): FILEFORMAT.md's preprocessing
  (`%b64[]`, `%hex[]`, `%repeat[]`, `%include`, `%if`/`%else`/`%endif`, `crlf=`, `nonewline=`)
  and variable substitution;
- the table of `<server>` names - each one's port variable and whether its server half is built;
- selecting which cases a reference pin can run, each other case given one skip reason from
  ADR-0080 decision 3, and the build-option feature table BL-389 probes;
- building each command line as upstream's runtests does (`-q`, `--output`, `--include`,
  `--trace-ascii`);
- generating the test certificates from the imported `.prm` files with `CertificateRequest`, and
  the SSH keys and `%SSHSRVMD5`/`%SSHSRVSHA256`;
- checking a `<verify>` section, with the `<strip>`/`<strippart>`/`<stripfile>` interpreter of
  ADR-0080 decision 4's Perl subset (nothing outside it: `strippart-unsupported`);
- the per-run result lines and reading the pass-rate file and passing lists of decision 5.

It does not hold the server halves: each protocol library carries its own optional seam for a
case's reply, and `Surl.Conformance.UnitTests` composes it. It hands a reply over as BCL types
only (parts by name as bytes, `<servercmd>` lines as strings). The data itself is not here: it is
`Surl.Conformance.UnitTests/UpstreamTestCases/`, with curl's `COPYING` and `SHA256SUMS`.
