# ADR-0026 — RTSP conformance on macOS, where the pinned build has no `rtsp`

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29, in BL-085

## Context

ADR-0016 pins stunnel/static-curl's 8.21.0 static builds for Linux (`linux-x64`) and
macOS (`osx-arm64`) and quotes the release notes' protocol line, which includes `rtsp`
for both. CI run https://github.com/StewartScottRogers/Surl/actions/runs/36537650411
(BL-079) printed `curl --version` for both pinned executables, and
`UpstreamCurlBuilds.json` now carries what they printed:

- `linux-x64` lists `rtsp`.
- `osx-arm64` does not: `... pop3 pop3s scp sftp smb smbs smtp smtps telnet tftp ws
  wss`. It also prints the `AppleSecTrust` feature, which the Linux build does not.

The Windows reference build (Git for Windows' 8.21.0, Schannel) lists `rtsp`. So of the
three platforms CI and the lanes run on, only macOS has no pinned build that can speak
RTSP. Surl's RTSP server (`Surl.Protocol.Rtsp`, phase 5 of the product overview) is not
built yet, and no conformance test for it exists.

Options weighed:

1. **Mark each RTSP conformance test
   `[OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]`.** Simple, but it ties
   the exclusion to the operating system rather than to the fact that matters - the
   pinned build's protocol list. If a later macOS pin carries `rtsp`, every test would
   have to be edited to run there; and it says nothing about any other build that lacks
   a protocol.
2. **Pin a second macOS build that has `rtsp`.** Needs a new download, so Stewart's
   approval, and there is no known upstream 8.21.0 macOS build with `rtsp` whose
   provenance is as clean as ADR-0016's (the curl project ships none; Homebrew patches
   curl). It would buy coverage of RTSP on one more platform for a protocol whose client
   side is the same upstream source on every platform.
3. **Let the pinned build's own protocol list decide.** An RTSP conformance test asks
   the pin for the current platform whether its `protocols` field (the build's own
   `curl --version` output) lists `rtsp`, and reports Inconclusive, naming the build and
   the missing protocol, where it does not - the same outcome a test already has where
   no pinned build is installed (`PinnedUpstreamCurl`).

## Decision

1. **RTSP conformance runs where the pinned reference build lists `rtsp`: Windows and
   Linux.** On macOS the RTSP conformance tests report Inconclusive, not pass and not
   fail, with a message naming the `osx-arm64` pin and saying its `protocols` do not
   list `rtsp`.
2. **The check is the pin's protocol list, not the operating system.** The conformance
   tests do not use `OSCondition` for this. Before running upstream curl, an RTSP
   conformance test requires the scheme from the current platform's pin
   (`PinnedUpstreamCurlBuild.Protocols`, parsed from `UpstreamCurlBuilds.json`), and the
   check is written once, beside `PinnedUpstreamCurl.RunAsync` in
   `Surl.Conformance.UnitTests`, so any conformance test for any scheme a pinned build
   lacks (SMB on Windows today, for instance) uses the same rule. The task that writes
   the first RTSP conformance test adds it. If a later pin for macOS lists `rtsp`, the
   tests run there without an edit.
3. **No second macOS build is pinned for RTSP.** RTSP carries no TLS in curl (there is
   no `rtsps`), so the TLS-backend differences that justify per-platform pins elsewhere
   (ADR-0016) do not arise, and the Windows and Linux runs exercise the same upstream
   client code. Nothing here asks Stewart for a download; if a future need does, it is a
   new ADR and his approval.
4. **The fast (unit) tests of `Surl.Protocol.Rtsp` run on macOS as everywhere.** They
   use recordings from pinned upstream curl taken on Windows or Linux, never a network,
   so macOS still builds and tests the server; only the live exchange with upstream curl
   is skipped there.

## Consequences

Good:

- The exclusion follows the evidence the pin already records, so it cannot drift from
  what the build actually does, and it needs no edit when the pins change.
- One rule covers every protocol a pinned build lacks, on any platform.
- No download, no new third-party build.

Costs and caveats:

- A regression in Surl's RTSP server that shows only on macOS (a socket or timing
  difference below the protocol) would not be caught by conformance on macOS. Accepted:
  the transport is `Surl.Networking`, which the HTTP, TELNET and other conformance tests
  exercise on macOS already.
- An Inconclusive result on the macOS leg is not a failure, so the merge gate stays
  green there; the Windows and Linux legs carry the RTSP gate.

## Alternatives considered

- **`OSCondition` exclusion (option 1).** Rejected for option 3: same outcome today, but
  keyed on the wrong fact.
- **A second macOS pin with `rtsp` (option 2).** Rejected: a download for no coverage
  the other two platforms do not already give.
