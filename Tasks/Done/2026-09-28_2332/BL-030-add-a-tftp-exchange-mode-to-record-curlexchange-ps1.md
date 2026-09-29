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
completed: 2026-09-28
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

- [x] `Get-Help .\Record-CurlExchange.ps1 -Parameter Tftp` (and `TftpData`,
      `TftpReply`) documents the mode as above.
- [x] Recording `tftp://127.0.0.1:<P>/file.txt` with the pinned build writes a
      `transcript.txt` whose first line is curl's RRQ (opcode `\x00\x01`), shows the
      server replying from a different port than `<P>`, and curl exits 0 with
      `stdout.bin` equal to `-TftpData`.
- [x] Recording `-T <file> tftp://127.0.0.1:<P>/up.txt` writes `upload.bin` equal to the
      file.
- [x] `-Tftp -Raw`, `-Tftp -Ftp` and `-Tftp -NoServer` are refused with a message.
- [x] The existing modes are unchanged.

## Notes

- Choices taken (defaults, no ADR needed: recorder behaviour, not Surl behaviour):
  - The default server answers a request carrying options with an OACK accepting
    `blksize` (capped at 65464, below 8 left out), `tsize` (the data length for a read,
    curl's value echoed for a write) and `timeout` (echoed); with no known option it
    starts with DATA 1 (read) or ACK 0 (write). This lets both the option and the
    no-option exchange be measured without an override.
  - `-TftpReply` is one datagram. An OACK (opcode 6) continues the transfer with the
    block size it names; anything else ends it and the script records curl's answers
    until it goes idle.
  - Added `-TftpIdleMilliseconds` (default 2000): silence before a resend, three resends
    at most, like every other mode's idle parameter.
  - Datagrams curl sends to the listening port after the first are recorded, not
    answered. `-Tftp -Tls` is refused too.
- Measured with pinned curl 8.21.0 (win-x64), 2026-09-28:
  - By default curl's RRQ carries options: `\x00\x01file.txt\x00octet\x00tsize\x000\x00blksize\x00512\x00timeout\x006\x00`;
    a WRQ carries `tsize\x00<file length>` instead of `tsize\x000`. After an OACK curl
    sends ACK 0 on a read; on a write the OACK is followed straight by DATA 1.
  - `--tftp-no-options` sends a bare `\x00\x01f\x00octet\x00`; a 1024-byte file then
    ends with an empty DATA 3, which curl acknowledges.
  - A first reply of ERROR 1 makes curl exit 68 with `curl: (68) TFTP: File Not Found`.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Record-CurlExchange.ps1 -Tftp serves one TFTP read or write over UDP from a new port and records every datagram
