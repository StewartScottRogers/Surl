---
id: BL-030
title: Add a TFTP exchange mode to Record-CurlExchange.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed:
---
# BL-030 — Add a TFTP exchange mode to Record-CurlExchange.ps1

## Goal

`Record-CurlExchange.ps1 -Tftp` serves one TFTP transfer over UDP to the pinned upstream
curl build, a read or a write, and records every datagram in both directions. BL-037 can
then build its byte scripts from measurement.

## Context

- TFTP is RFC 1350, with option negotiation in RFC 2347, `blksize` in RFC 2348 and
  `tsize` in RFC 2349. The server answers from a new port, its transfer identifier
  (RFC 1350, section 4). The mode must do that, or curl's behaviour on the reply port is
  not what gets measured.
- Upstream curl's TFTP options include `--tftp-blksize` and `--tftp-no-options`
  (https://curl.se/docs/manpage.html, checked 2026-09-28; the page then documented
  8.23.0). Whether 8.21.0 sends an OACK-requesting option list by default is measured
  here, not assumed.
- Shape: `-Tftp` with `-TftpData` (the file served for a read, same escapes as
  `-Response`) and `-TftpReply` overrides for the first reply (for example an ERROR
  packet, or an OACK). A write (`curl -T`) is answered with ACKs, and the bytes received
  are written to `upload.bin`.
- Outputs: `request.bin` (every datagram curl sent, concatenated), `transcript.txt` (one
  line per datagram: direction, the local and remote port, and the bytes as `\xHH` where
  non-printable), plus the usual `stdout.bin`, `stderr.txt` and `exitcode.txt`.
- Same guards as every mode: `Assert-PinnedUpstreamCurl`, and refusing combinations with
  other modes and `-NoServer`.

## Acceptance criteria

- [ ] `Get-Help .\Record-CurlExchange.ps1 -Parameter Tftp` (and `TftpData`,
      `TftpReply`) documents the mode as above.
- [ ] Recording `tftp://127.0.0.1:<P>/file.txt` with the pinned build writes a
      `transcript.txt` whose first line is curl's RRQ (opcode `\x00\x01`), shows the
      server replying from a different port than `<P>`, and curl exits 0 with
      `stdout.bin` equal to `-TftpData`.
- [ ] Recording `-T <file> tftp://127.0.0.1:<P>/up.txt` writes `upload.bin` equal to the
      file.
- [ ] `-Tftp -Raw`, `-Tftp -Ftp` and `-Tftp -NoServer` are refused with a message.
- [ ] The existing modes are unchanged.

## Notes

## Log

- 2026-09-28: Created.
