---
id: BL-938
title: Call Start-TlsRelay by its name in Record-CurlExchange.ps1's TLS relay branch
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-11-06
completed:
---
# BL-938 — Call Start-TlsRelay by its name in Record-CurlExchange.ps1's TLS relay branch

## Goal

`Record-CurlExchange.ps1` runs its TLS relay again: a recording with `-TlsAlpn`, `-TlsRenegotiationOff`
or `-TlsSrp` starts the relay instead of failing on an unknown command.

## Context

- Line 3391 (the `elseif ($useTlsRelay)` branch; `$useTlsRelay` is set at line 999 from
  `-TlsRenegotiationOff`, `-TlsAlpn` and `-TlsSrp`) calls `Start-TlsRelay-Certificate $tlsCertificate ...`:
  the space between the function name and its `-Certificate` parameter is missing, so PowerShell looks
  for a command named `Start-TlsRelay-Certificate`, which does not exist. The function is
  `Start-TlsRelay` (line 3151). The typo came in with commit `a4fbdc30` (TLS-SRP recording).
- Found by BL-595's timed-out run (2026-11-06), which fixed it in passing; its fix is the one-line
  hunk of `Record-CurlExchange.ps1` in the stash `darkfactory BL-595 20261106-033059` in the main
  checkout (`Z:\repos\Surl`). Take it with
  `git diff "stash@{N}^" "stash@{N}" -- Record-CurlExchange.ps1 | git apply` (find N with
  `git stash list`, matching the message; never pop, apply or drop the stash), or make the one
  edit by hand: `Start-TlsRelay-Certificate` becomes `Start-TlsRelay -Certificate`.
- Stand-in: not applicable; this is the measuring tool, not a server.

## Acceptance criteria

- [ ] `Select-String -Path Record-CurlExchange.ps1 -Pattern 'Start-TlsRelay-Certificate'` finds nothing,
      and the call at the `$useTlsRelay` branch reads `Start-TlsRelay -Certificate $tlsCertificate`.
- [ ] A recording through the relay runs to its end: `Record-CurlExchange.ps1 -Tls -TlsAlpn http/1.1
      -OutDirectory <a scratch folder outside the repository> -CurlArgs ...` with the pinned Windows
      reference build (curl 8.21.0, `UpstreamCurlBuilds.json`), `-k` and an `https://` URL on the
      recorder's port, writes `alpn.txt`, `request.bin` and `exitcode.txt` there, and its
      output is not a "not recognized as the name of a cmdlet" error. The result is noted in the
      task's Log.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing.
