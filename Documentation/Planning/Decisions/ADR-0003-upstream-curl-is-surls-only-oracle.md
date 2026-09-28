# ADR-0003 — Upstream curl is Surl's only oracle; the Curl port is measured against Surl last

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Stewart, 2026-09-28

## Context

Surl needs something to be right against. Two candidates exist, and they are easy to
confuse:

- **upstream curl** - the original C implementation, https://github.com/curl/curl;
- **the Curl port** - Stewart's C# port of curl, https://github.com/StewartScottRogers/Curl,
  Surl's sibling, whose structure Surl mirrors (ADR-0002).

Stewart's direction, 2026-09-28: the original implementation of curl is the measure of
Surl's success, not the C# port; later, Surl will be used to validate the port's quality.

The ordering matters because of what each check can prove. A Surl built to satisfy the
port would absorb the port's defects as requirements, and the later check of the port
against Surl would pass on exactly those defects - a circle that proves nothing.

Telling the two apart also takes care. Measured on Stewart's machine on 2026-09-28:

- A bare `curl` in Windows PowerShell 5.1 is the `Invoke-WebRequest` alias.
- The first `curl.exe` on `PATH` is WinGet's curl 8.18.0 (LibreSSL), not the release the
  port targets.
- Git for Windows ships upstream curl 8.21.0 (Schannel) at
  `C:\Program Files\Git\mingw64\bin\curl.exe` - the same build the Curl port treats as its
  Windows reference, and so the right baseline for comparing the two in the last phase.
- `C:\Windows\System32\curl.exe` is upstream curl 8.21.0 built by Microsoft, without
  `rtsp`, `scp`, `sftp` or `smb`.
- The Curl port deliberately reports upstream's own version number, so `--version` cannot
  tell it from upstream curl either.

No single measured build covers all 29 schemes at the reference release: the 8.21.0
builds lack `smb` and `smbs` and HTTP/2 and HTTP/3, which only the WinGet 8.18.0 build
has.

## Decision

1. **Upstream curl is Surl's only oracle.** A Surl behaviour is right when a pinned
   upstream curl build completes the exchange against it as the protocol's specification
   and upstream curl's own behaviour say. The Curl port is never a test client, never a
   source of fixtures or expected bytes, and never evidence in an ADR.
2. **The reference release is 8.21.0**, the release the Curl port targets, so the last
   phase compares like with like.
3. **Builds are pinned by file.** `UpstreamCurlBuilds.json` records each upstream build's
   default path, SHA-256, version line, protocols and features. `Record-CurlExchange.ps1`
   refuses to run any curl whose SHA-256 is not pinned, and every later tool that runs
   curl for Surl (the conformance tests first) does the same.
4. **Pinned today:** Git for Windows' upstream curl 8.21.0 build (Schannel), SHA-256
   `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`.
5. **Needed, not yet pinned:** an upstream 8.21.0 build with SMB and HTTP/2 and HTTP/3 -
   the curl project's own Windows build from curl.se is the candidate - and builds for
   Linux and macOS. Downloading a build needs Stewart's approval; pinning one is a
   decision recorded in an ADR. The SMB server and the HTTP/2 and HTTP/3 servers are not
   validated until such a build is pinned.
6. **The last phase turns Surl on the port.** The port runs the same conversations
   against Surl beside pinned upstream curl; wherever the two disagree, upstream is right,
   and the disagreement is filed as a defect on the port's own board. Surl never changes
   to make the port pass.
7. **Code may be copied from the port; verdicts may not.** Copying code (a cryptographic
   primitive with its test vectors, say) saves work and decides nothing. Every expected
   result still comes from upstream curl or a published specification.

## Consequences

Good:

- The last phase measures something real: the port against an instrument that upstream
  curl, and only upstream curl, has validated.
- A mistaken curl on `PATH` cannot contaminate a fixture; the recording script refuses it.

Costs and caveats:

- A new or updated upstream build is refused until it is pinned, so a Git for Windows
  update stops the recording script until someone pins the new file. Accepted: the
  reference moves only by decision.
- The Curl port's copy of `Record-CurlExchange.ps1` can measure the Linux build through
  `wsl.exe`; Surl's refuses `wsl.exe`, because a curl inside WSL cannot be checksummed
  from Windows. Linux builds are measured on Linux once they are pinned.
- SMB and HTTP/2 and HTTP/3 wait on open question 1 of the product overview.

## Alternatives considered

- **Validate against the Curl port.** Rejected: circular, as above.
- **Trust whatever `curl` is on `PATH`.** Rejected: measured to be three different
  programs on one machine.
- **Identify upstream curl by `--version`.** Rejected: the port reports the same version.
