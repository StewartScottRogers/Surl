---
id: BL-002
title: Decide the server-side TLS contract
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-000]
touches: [Documentation/Planning/Decisions, Documentation/Product/Product-Overview.md, Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
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

- [x] A new ADR, numbered with the next free number, exists in
      `Documentation/Planning/Decisions/`, is marked "Decided by Claude under Stewart's
      delegation", has status Accepted, and is listed in that folder's `README.md` index.
- [x] The ADR names the Abstractions type or member through which a protocol server
      (a) receives an already-secured connection and (b) asks for an established
      connection to be upgraded to TLS, and what it learns afterwards (negotiated
      protocol, ALPN protocol, client certificate if any).
- [x] The ADR says where the server certificate and key come from: which surl options
      (curl's names, where a server-side meaning exists), which file formats, and what
      surl does for a secure scheme when no certificate is given.
- [x] The ADR says how client-certificate verification for upstream curl's `--cert`
      is switched on and what the trust anchors are.
- [x] The ADR says which ALPN protocol IDs `Surl.Networking` offers for `https` in
      Phase 1.
- [x] The ADR records what the pinned upstream curl 8.21.0 build did against a throwaway
      certificate with and without `-k`: exit code and first stderr line, measured with
      `Record-CurlExchange.ps1 -Tls`.
- [x] The ADR states each platform difference it accounts for (Windows, Linux, macOS) and
      how BL-012's tests stay platform-neutral: certificates generated in the test with
      `CertificateRequest`, never read from a Windows certificate store.
- [x] `Documentation/Product/Product-Overview.md`, "Architecture": the `> **TODO**` no
      longer lists the server-side TLS contract and points to the new ADR.

## Notes

- Recorded as ADR-0008 (`Documentation/Planning/Decisions/ADR-0008-the-server-side-tls-contract.md`),
  indexed in the folder's `README.md`. Summary: `IConnection` gains `TlsSession` and
  `UpgradeToTlsAsync`; the engine performs the implicit handshake for the `TlsSchemes`
  schemes, and servers perform STARTTLS-style upgrades themselves; certificates come from
  `--cert`/`--cert-type`/`--key`/`--key-type`/`--pass`, or a throwaway RSA 2048
  certificate without `--cert`; ALPN offers `http/1.1` only, for `https` and `wss`;
  `--cacert` switches on required client verification with custom-root trust; exit codes
  58 and 77 join ADR-0005's table.
- Measured with the pinned curl 8.21.0 (Schannel) via `Record-CurlExchange.ps1 -Tls`:
  `-k` exit 0, no `-k` exit 60 (`SEC_E_UNTRUSTED_ROOT`); ALPN offers `http/1.1` only
  (`http/1.0,http/1.1` with `--http1.0`); `--http2` is unsupported by this build (exit 2);
  `--cacert` to a private root needs `--ssl-no-revoke` (60 without it, 0 with it); a
  missing `--cert` file is 58, a missing `--cacert` file is 2.
- Written in this session rather than by `align-and-document`, because the ADR rests on
  measurements taken in this session. The docs pipeline makes no code change, so `verify`
  ran only as the build and fast tests.
- `touches` gained `Documentation/Product/Requirements.md` (FR-021 and open question 1
  referred to "BL-002's ADR"); no task in Doing names it.
- Accepted ADRs 0004, 0006 and 0007 still say "BL-002's ADR". They are immutable, and
  ADR-0008 names each point it fills.
- Client-certificate verification end to end was not measured: the recorder does not
  request a client certificate. ADR-0008 section 5 leaves that to BL-012's conformance
  follow-up.
- Follow-ups filed by task-planner: BL-060 (Cli options), BL-061 (exit codes), BL-062
  (engine implicit handshake). BL-006 will also need `Surl.Core` in its touches, because
  the recording decorator implements `IConnection`.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ADR-0008 fixes the server-side TLS contract: IConnection.UpgradeToTlsAsync and TlsSession, certificate options and throwaway, ALPN http/1.1, client verification by --cacert, measured against curl 8.21.0
