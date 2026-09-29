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
completed: 2026-09-28
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

- [x] `Get-Help .\Record-CurlExchange.ps1 -Parameter Raw` (and `RawReply`,
      `RawReplyFirst`, `RawIdleMilliseconds`) documents the mode as above.
- [x] Recording `dict://127.0.0.1:<P>/d:hello` with the pinned build, with a banner sent
      first and a DICT reply, writes `request.bin` and `transcript.txt`, and
      `request.bin` holds the command lines curl sent.
- [x] Recording `mqtt://127.0.0.1:<P>/topic` with a CONNACK reply given as `\x20\x02\x00\x00`
      (MQTT 3.1.1, section 3.2) writes `request.bin` starting with byte `0x10`, a CONNECT
      packet (MQTT 3.1.1, section 3.1).
- [x] `-Raw -Ftp` and `-Raw -NoServer` are refused with a message.
- [x] The existing modes are unchanged: re-recording the `.EXAMPLE` HTTP command in the
      script's help still produces the same `request.bin` shape.

## Notes

The two sample recordings are made to prove the mode. They are not committed as fixtures.
The protocol tasks (BL-033 to BL-036) record their own.

Choices (sensible defaults, taken unattended):
- Bursts end on `Socket.Poll` with RawIdleMilliseconds rather than a read timeout, so an
  idle pause never leaves the socket in a timed-out state before the next reply.
- A pause in which curl sent nothing still sends the next reply, so a server-speaks-later
  protocol needs no extra switch.
- `-Raw -Tls` is refused: the task asks for plain TCP, and no Phase 1 raw protocol needs
  TLS. A TLS raw mode (mqtts) can be added when a task needs it.
- transcript.txt splits each burst after LF, drops a closing CRLF, writes other
  non-printable bytes as `\xHH` and a backslash as `\\`, and marks curl hanging up with
  `= curl closed the connection`.

Measured with pinned curl 8.21.0 (not committed):
- DICT `d:hello`, banner first: curl sends `CLIENT libcurl 8.21.0`, `DEFINE ! hello`,
  `QUIT` in one burst; exit 0, stdout echoes the server's lines.
- MQTT `/topic`, CONNACK `\x20\x02\x00\x00`: request.bin starts `0x10` (CONNECT, client id
  `curl` plus random letters), then SUBSCRIBE `\x82\x0A...topic\x00`; with no SUBACK
  scripted curl exits 56 "Connection disconnected".
- HTTP `.EXAMPLE` unchanged: `GET /a?b HTTP/1.1` with Host, User-Agent curl/8.21.0, Accept.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Record-CurlExchange.ps1 -Raw records scripted plain-TCP exchanges (DICT, MQTT measured) with request.bin and transcript.txt
