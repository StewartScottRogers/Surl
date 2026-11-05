---
id: BL-557
title: Decide how Surl serves SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-571, BL-550]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed:
---
# BL-557 — Decide how Surl serves SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h

## Goal

An ADR decides how Surl acts as the SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h proxy that upstream curl's
`--socks4`, `--socks4a`, `--socks5`, `--socks5-hostname`, `--proxy socks*://` and `--proxy-user`
talk to, over TCP and over a Unix domain socket, where the code lives and how it reaches the
origin.

## Context

- Scope: product overview, "Also in scope", "Proxies" (SOCKS4, SOCKS4a, SOCKS5, SOCKS5h). ADR-0080
  decision 2 marks `socks4`, `socks5` and `socks5unix` `server-not-built`.
- Upstream test cases that wait on this (tag `curl-8_21_0`, commit
  `68720b4837284335b2d63cb358f8f6ce65f5bc55`, classified with ADR-0080's rules by BL-399):
  - `socks4` (5): 700, 702, 706, 708, 729;
  - `socks5` (17): 701, 703, 707, 709, 710, 711, 712, 713, 715, 716, 717, 719, 720, 721, 728, 742,
    2055 (715 and 2055 also need `http-proxy`, BL-550's family);
  - `socks5unix` (3): 1467, 1468, 1470 - features `proxy`, `UnixSockets`, `!win32`, command
    `--socks5 localhost%SOCKSUNIXPATH`.
- Oracle: RFC 1928 (SOCKS5), RFC 1929 (username/password), the SOCKS4 protocol document and its 4a
  extension (a `0.0.0.x` address followed by a host name); curl 8.21.0's manpage,
  https://curl.se/docs/manpage.html (`--socks4`, `--socks4a`, `--socks5`, `--socks5-hostname`,
  `--socks5-basic`, `--socks5-gssapi`, `--proxy`); FILEFORMAT.md at the tag for `%SOCKSPORT`,
  `%SOCKSUNIXPATH` and any SOCKS `<servercmd>` lines. Measure each byte with
  `Record-CurlExchange.ps1` against the pinned 8.21.0 builds; never the Curl port (ADR-0003).
- Building on: BL-550's ADR (the outbound-connection contract BL-551 builds, the allowed-origin
  rule), BL-571's ADR (the Unix socket listener), ADR-0079 (category folders: a new category is a
  decision recorded in an ADR).

Decide and record:

1. The library and its folder. Planning assumption:
   `Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary` with its `.UnitTests` twin, one server for
   both versions told apart by the first byte; `Protocols/Proxy/` is a new category recorded here
   under ADR-0079 (and whether `Surl.Protocol.HttpProxy` moves there or stays beside
   `Surl.HttpMessage` in `Protocols/Http/`). Amend ADR-0002 decision 3's reference table.
2. SOCKS4 and 4a: `CONNECT` only or `BIND` too, the user ID field, the 0x5A and 0x5B replies, and
   host-name resolution for 4a.
3. SOCKS5: methods offered (no authentication, username/password; GSSAPI's answer, planning
   assumption: not offered, so curl's `--socks5-gssapi` gets `0xFF`), `CONNECT` with IPv4, IPv6
   and domain-name addresses (5h), `BIND` and `UDP ASSOCIATE` (served or refused with reply
   `0x07`), and every reply code with when it is sent.
4. RFC 1929: how accounts are named on the command line and the failure reply.
5. The command line (planning assumption: `surl socks4://127.0.0.1:1080` and
   `surl socks5://127.0.0.1:1080`, mirroring curl's proxy URL schemes, `socks4a` and `socks5h`
   being client-side choices the same listener serves) and SOCKS5 on a Unix socket via BL-571's
   form.
6. Each ADR-0006 limit the SOCKS server applies (handshake timeout, maximum user ID and host name
   lengths).
7. How the upstream test cases' server half plays `socks4`, `socks5` and `socks5unix`: the SOCKS
   server in front of BL-391's HTTP server half, and which case parts, if any, it takes.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked
      "Decided by Claude under Stewart's delegation", answering decisions 1 to 7, each citing its
      RFC section or the curl 8.21.0 measurement it rests on, and it is listed in the folder's
      `README.md`.
- [ ] If the ADR adds a category folder, it says so under ADR-0079 and names the root `CLAUDE.md`
      "Repository layout" change BL-558 makes.
- [ ] The ADR names the 25 cases above by number and the tasks that act on it: BL-558, BL-559,
      BL-560, BL-561 and BL-562.

## Notes

- Planning assumptions above are BL-399's defaults for an unattended plan; the ADR may change any
  of them, and the build tasks' `touches` follow the ADR (a task records the change in its Notes).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
