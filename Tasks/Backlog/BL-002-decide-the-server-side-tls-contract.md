---
id: BL-002
title: Decide the server-side TLS contract
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-000]
touches: [Documentation/Planning/Decisions, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-002 — Decide the server-side TLS contract

## Goal

An accepted ADR fixes how a protocol server receives a TLS-secured connection. It covers
both TLS from the first byte (`https`, `ftps` implicit, `imaps` and the rest) and an
upgrade in the middle of a connection (FTP `AUTH TLS`, SMTP, IMAP and POP3 `STARTTLS`),
so that BL-006 can write the contract and BL-012 can implement it.

## Context

- Product overview, "Also in scope": TLS on the server side covers certificates and
  keys, client-certificate verification for upstream curl's `--cert`, and ALPN.
  "Architecture" says server-side TLS uses the BCL's `SslStream`, and only
  `Surl.Networking` constructs one (root `CLAUDE.md`).
- ADR-0002, "Consequences": a secure variant is the same protocol server over a secured
  connection, never a second library.
- The listener-seam ADR recorded by BL-000 defines the connection type this contract
  extends.
- `Record-CurlExchange.ps1 -Tls` already serves TLS with a throwaway certificate, and
  curl needs `-k` for it. Its `.PARAMETER Tls` help describes it. Use it to measure what
  the pinned upstream curl 8.21.0 build (Schannel) does against a throwaway certificate
  with and without `-k`.
- CI runs the fast tests on Windows, Linux and macOS (root `CLAUDE.md`). `SslStream`'s
  server side behaves differently per platform (certificate formats, ephemeral keys, ALPN
  support). The ADR must hold on all three.
- This is a design decision delegated to Claude (root `CLAUDE.md`, "Decisions").

## Acceptance criteria

- [ ] A new ADR, numbered with the next free number, exists in
      `Documentation/Planning/Decisions/`, is marked "Decided by Claude under Stewart's
      delegation", has status Accepted, and is listed in that folder's `README.md` index.
- [ ] The ADR names the Abstractions type or member through which a protocol server
      (a) receives an already-secured connection and (b) asks for an established
      connection to be upgraded to TLS, and what it learns afterwards (negotiated
      protocol, ALPN protocol, client certificate if any).
- [ ] The ADR says where the server certificate and key come from: which surl options
      (curl's names, where a server-side meaning exists), which file formats, and what
      surl does for a secure scheme when no certificate is given.
- [ ] The ADR says how client-certificate verification for upstream curl's `--cert`
      is switched on and what the trust anchors are.
- [ ] The ADR says which ALPN protocol IDs `Surl.Networking` offers for `https` in
      Phase 1.
- [ ] The ADR records what the pinned upstream curl 8.21.0 build did against a throwaway
      certificate with and without `-k`: exit code and first stderr line, measured with
      `Record-CurlExchange.ps1 -Tls`.
- [ ] The ADR states each platform difference it accounts for (Windows, Linux, macOS) and
      how BL-012's tests stay platform-neutral: certificates generated in the test with
      `CertificateRequest`, never read from a Windows certificate store.
- [ ] `Documentation/Product/Product-Overview.md`, "Architecture": the `> **TODO**` no
      longer lists the server-side TLS contract and points to the new ADR.

## Notes

## Log

- 2026-09-28: Created.
