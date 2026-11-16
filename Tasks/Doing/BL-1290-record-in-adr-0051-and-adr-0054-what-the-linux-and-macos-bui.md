---
id: BL-1290
title: Record in ADR-0051 and ADR-0054 what the Linux and macOS builds negotiated over SSH
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1289]
touches: [Documentation/Planning/Decisions/ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md, Documentation/Planning/Decisions/ADR-0054-how-the-ssh-server-answers-upstream-curls-scp-and-sftp-requests.md]
requirement: FR-039
created: 2026-11-16
completed:
---
# BL-1290 — Record in ADR-0051 and ADR-0054 what the Linux and macOS builds negotiated over SSH

## Goal

ADR-0051 and ADR-0054 state what the Linux and macOS reference builds of upstream curl 8.21.0 (OpenSSL, libssh2 1.11.1) were measured to negotiate and transfer against `surl` over `scp` and `sftp`, citing BL-1289's CI run, instead of "predicted ... not yet measured".

## Context

- ADR-0051, decision 2, "What was negotiated", bullet "Linux and macOS reference builds (OpenSSL): predicted from their `KEXINIT` lists (Context), not yet measured by a Linux or macOS run": kex `curve25519-sha256`; host key `rsa-sha2-512` for RSA, `ecdsa-sha2-nistp256` for ECDSA P-256, `ssh-ed25519` for Ed25519; cipher `chacha20-poly1305@openssh.com`, MAC `implicit`, compression `none` (`zlib` with `--compressed-ssh`), strict kex on.
- ADR-0054, decision 16 item 4: "The Linux and macOS builds (OpenSSL) are predicted to complete the transfer with either key; that is pinned for their CI legs, not yet measured (ADR-0051 decision 2)."
- The measurement is in `Documentation/Conformance/Conformance-Off-Windows.md` (BL-1289): the outcomes of `UpstreamCurlLogsInToSurlOverSshTests`, `UpstreamCurlOffersSshAlgorithmsTests` and the other SSH classes on each platform, and the `SSH negotiated` lines surl wrote, including those of `HostKey_OnlyEllipticCurveOnOpenSslBuilds_NegotiatesItAndDownloadsTheFile`.
- Record, do not decide: replace "predicted ... not yet measured" with what was measured, keep the prediction only where nothing measured it, and say so. Where a measurement disagrees with the prediction, write the disagreement and file a task for it; do not change code here.
- Stand-in: not applicable; a record of upstream curl's results.

## Acceptance criteria

- [ ] ADR-0051 decision 2's Linux and macOS bullet gives, per platform, the kex, host-key, cipher, MAC and compression algorithms from the `SSH negotiated` lines in `Conformance-Off-Windows.md`, and names the CI run.
- [ ] ADR-0054 decision 16 item 4 says whether the ECDSA-only and Ed25519-only transfers completed on `linux-x64` and `osx-arm64`, naming the test and the CI run.
- [ ] Neither ADR still says the Linux or macOS SSH results are "predicted" or "not yet measured" for anything `Conformance-Off-Windows.md` records as Passed.
- [ ] Any Inconclusive or failed SSH test in `Conformance-Off-Windows.md` is named in ADR-0051 or ADR-0054 with its reason, and a task is filed for each failure (its ID in Notes).

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
