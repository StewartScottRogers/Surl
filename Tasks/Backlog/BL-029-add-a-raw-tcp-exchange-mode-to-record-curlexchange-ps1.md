---
id: BL-029
title: Add a raw TCP exchange mode to Record-CurlExchange.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed:
---
# BL-029 — Add a raw TCP exchange mode to Record-CurlExchange.ps1

## Goal

`Record-CurlExchange.ps1 -Raw` records a TCP conversation with the pinned upstream curl
build for protocols the script has no mode for: DICT, Gopher, TELNET and MQTT. It sends
scripted reply bytes after each burst curl sends, and records both directions.

## Context

- The script's HTTP mode reads until CRLF CRLF, and its `-Ftp`, `-Smtp`, `-Imap` and
  `-Pop3` modes speak those protocols. None fits DICT (RFC 2229), Gopher (RFC 1436),
  TELNET (RFC 854) or MQTT 3.1.1 (binary). Root `CLAUDE.md`: extend the recorder when it
  falls short, and never write a throwaway server.
- Shape: `-Raw` with `-RawReply` (one or more replies, in the script's existing
  backslash-escape syntax, `\xHH` included, so binary MQTT packets can be given). Serve
  one connection. Repeatedly: read what curl sends until it pauses for
  `-RawIdleMilliseconds` (default 1000) or closes, then send the next reply. After the
  last reply, keep reading until curl closes or goes idle. `-RawReplyFirst` sends the
  first reply before reading, for protocols where the server speaks first (a DICT
  banner, say).
- Outputs: `request.bin` (everything curl sent, in order), `transcript.txt` (both
  directions, `> ` for curl and `< ` for server, as `-Ftp` does, with non-printable
  bytes as `\xHH`), and the usual `stdout.bin`, `stderr.txt` and `exitcode.txt`.
- Same guards as every mode: `Assert-PinnedUpstreamCurl` runs first, and combining
  `-Raw` with another server mode or `-NoServer` is refused, as the `-NoServer` help
  describes for the other modes.
- PowerShell only, no Python (root `CLAUDE.md`).

## Acceptance criteria

- [ ] `Get-Help .\Record-CurlExchange.ps1 -Parameter Raw` (and `RawReply`,
      `RawReplyFirst`, `RawIdleMilliseconds`) documents the mode as above.
- [ ] Recording `dict://127.0.0.1:<P>/d:hello` with the pinned build, with a banner sent
      first and a DICT reply, writes `request.bin` and `transcript.txt`, and
      `request.bin` holds the command lines curl sent.
- [ ] Recording `mqtt://127.0.0.1:<P>/topic` with a CONNACK reply given as `\x20\x02\x00\x00`
      (MQTT 3.1.1, section 3.2) writes `request.bin` starting with byte `0x10`, a CONNECT
      packet (MQTT 3.1.1, section 3.1).
- [ ] `-Raw -Ftp` and `-Raw -NoServer` are refused with a message.
- [ ] The existing modes are unchanged: re-recording the `.EXAMPLE` HTTP command in the
      script's help still produces the same `request.bin` shape.

## Notes

The two sample recordings are made to prove the mode. They are not committed as fixtures.
The protocol tasks (BL-033 to BL-036) record their own.

## Log

- 2026-09-28: Created.
