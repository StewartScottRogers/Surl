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
completed:
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

- [ ] Each of the eight server names above is named by at least one Backlog task that builds it,
      and each family has a design task that ends in an ADR.
- [ ] Each new task names the upstream test cases (by number) that wait on it.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
