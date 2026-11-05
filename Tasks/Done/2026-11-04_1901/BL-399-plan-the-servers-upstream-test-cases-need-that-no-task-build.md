---
id: BL-399
title: Plan the servers upstream test cases need that no task builds yet
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-386]
touches: [Tasks/Backlog]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-399 — Plan the servers upstream test cases need that no task builds yet

## Goal

Backlog tasks exist that build each server ADR-0080 decision 2 marks `server-not-built`, other
than `http/2` and `http/3`: the HTTP `CONNECT` proxy (`http-proxy`), the HTTPS proxy
(`https-proxy`), SOCKS4 and SOCKS5 (`socks4`, `socks5`, `socks5unix`), HTTP over a Unix domain
socket (`http-unix`), TLS-SRP (`httptls+srp`, `httptls+srp-ipv6`) and the test DNS server (`dns`).

## Context

- ADR-0080 decision 2's table and "Consequences": 67 runnable-shaped cases at tag `curl-8_21_0`
  wait on these servers (`http-proxy` 24, `socks5` 17, `https-proxy` 5, `socks4` 5,
  `httptls+srp` 4, `dns` 3, `socks5unix` 3, `http-unix` 2; `http/2` 4 and `http/3` 2 are Phase
  6's own tasks).
- The proxies are in scope (product overview, "Also in scope", "Proxies"). TLS-SRP is not offered
  by `SslStream`, so it is hand-built in its own library (root `CLAUDE.md`, "Decisions").
- Plan with `/task-plan` or the `task-planner` agent: one ADR-first design task per server family,
  then build and conformance tasks, each with its upstream test-case server table row flip.

## Acceptance criteria

- [x] Each of the eight server names above is named by at least one Backlog task that builds it,
      and each family has a design task that ends in an ADR.
- [x] Each new task names the upstream test cases (by number) that wait on it.

## Notes

- Measured 2026-11-04: sparse `git clone` of `https://github.com/curl/curl` tag `curl-8_21_0`
  (HEAD `68720b4837284335b2d63cb358f8f6ce65f5bc55`, `tests/data` only, into `%TEMP%`, outside the
  repo), classified with ADR-0080's first-rule order in PowerShell. Every class count matches
  ADR-0080 (1,296 runnable, 67 waiting on unbuilt servers). Waiting cases:
  - `http-proxy` (24): 80, 83, 95, 275, 445, 714, 715, 744, 1078, 1230, 1287, 1288, 1297, 1319,
    1320, 1321, 1428, 1849, 1904, 2050, 2055, 2107, 3028, 3106
  - `https-proxy` (5): 1586, 1630, 1631, 1632, 3401
  - `socks4` (5): 700, 702, 706, 708, 729
  - `socks5` (17): 701, 703, 707, 709, 710, 711, 712, 713, 715, 716, 717, 719, 720, 721, 728, 742, 2055
  - `socks5unix` (3): 1467, 1468, 1470; `http-unix` (2): 1435, 1436
  - `httptls+srp` (4): 320, 321, 322, 324; `httptls+srp-ipv6`: none at the tag
  - `dns` (3): 2102, 2103, 2104 (need feature `override-dns`, which BL-569 measures per pin)
  - 715 and 2055 name both `http-proxy` and `socks5`, hence 69 names over 67 cases.
- Filed by `task-planner`, BL-571 to BL-570, five families, each opening with an ADR task:
  - Unix domain sockets: BL-571 (ADR), BL-547 listener, BL-548 executable, BL-549 server half.
  - HTTP and HTTPS proxies: BL-550 (ADR), BL-551 outbound origin connections, BL-552 forward and
    CONNECT, BL-553 Proxy-Authorization, BL-554 HTTPS proxy, BL-555 executable, BL-556 server half.
  - SOCKS: BL-557 (ADR), BL-558 SOCKS4/4a, BL-559 SOCKS5/5h, BL-560 executable, BL-561 SOCKS5 on
    a Unix socket, BL-562 server half.
  - TLS-SRP: BL-563 (ADR), BL-564 SRP-6a, BL-565 TLS 1.2 record layer, BL-566 SRP handshake,
    BL-567 executable, BL-568 server half (Linux and macOS legs only: the Windows pin is Schannel).
  - Test DNS server: BL-569 (ADR: is the server half BL-478..BL-482's extended DNS server?),
    BL-570 server half. The DNS build itself is the existing BL-479 to BL-482, not duplicated.
- Default taken: library names and locations (`Surl.Protocol.HttpProxy` in `Protocols/Http`, a
  new `Protocols/Proxy` category for `Surl.Protocol.Socks`, `Surl.Cryptography.Srp` and
  `Surl.TlsSrp` in `Cryptography/`) are planning assumptions each design ADR may change, so no
  ADR is written here; BL-399 only plans.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. BL-571 to BL-570 plan the proxy, SOCKS, Unix-socket, TLS-SRP and dns server halves the 67 waiting upstream test cases need, each family opening with an ADR task
