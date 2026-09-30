---
id: BL-252
title: Decide how surl answers blowfish-cbc, cast128-cbc and hmac-ripemd160 from curl's OpenSSL builds
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-172]
touches: [Documentation/Planning/Decisions]
requirement: FR-039
created: 2026-09-30
completed: 2026-09-30
---
# BL-252 — Decide how surl answers blowfish-cbc, cast128-cbc and hmac-ripemd160 from curl's OpenSSL builds

## Goal

A new ADR amends ADR-0051 decision 2. It decides how surl answers the four algorithm names that
upstream curl 8.21.0's OpenSSL builds offer and that no row of decision 2 covers, and it lists
the tasks that build them. Those tasks are filed on the board.

## Context

- BL-172 recorded the `KEXINIT` lists of the Linux and macOS reference pins in
  `UpstreamCurlBuilds.json`: curl 8.21.0, stunnel/static-curl, libssh2 1.11.1 on OpenSSL 4.0.1.
  The lists are in BL-172's Notes and pinned by
  `Surl.Conformance.UnitTests/UpstreamCurlOffersSshAlgorithmsTests.cs` (`OpenSslCipher`,
  `OpenSslMac`). BL-172 made that measurement, so this task depends on it. Those lists add four
  names that ADR-0051 decision 2 does not cover:
  - ciphers `blowfish-cbc` and `cast128-cbc`, which use 64-bit blocks (RFC 4253 section 6.3)
  - MACs `hmac-ripemd160` and `hmac-ripemd160@openssh.com`, which use RIPEMD-160
- ADR-0051 decision 2 says "Every entry of the measured lists is thereby assigned: none is left
  out". It measured only the Windows (WinCNG) build, and says a name outside its lists gets a
  task. `Documentation/Planning/Decisions/ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md`.
- Standing rule (root `CLAUDE.md`, "Decisions"): nothing is left out because it is hard or
  because the BCL has no primitive for it. Each hand-built primitive goes in its own
  `Surl.<Area>.UnitLibrary` with a `.UnitTests` twin, held to the quality gates, as ADR-0048
  (Curve25519, Ed25519, ChaCha20, Poly1305) and ADR-0051 decision 3 (Rc4, BcryptPbkdf) did.
  The BCL has no Blowfish, CAST-128 or RIPEMD-160 on .NET 10.
- The expected outcome is to offer all four only with `--allow-weak-ssh-algorithms`, appended
  after that option's existing cipher and MAC entries (ADR-0051 decision 2's weak table). Each
  primitive is built by hand:
  - Blowfish: `Surl.Cryptography.BcryptPbkdf.UnitLibrary` already holds a Blowfish inside it
    (BL-220). The ADR decides whether that Blowfish moves into its own library or is exposed
    for reuse. Whichever it chooses, `Surl.Protocol.Ssh` must reference only libraries that
    ADR-0002 decision 3's table (as amended) lists.
  - CAST-128 (RFC 2144).
  - RIPEMD-160.
- The ADR amends ADR-0002 decision 3's table for any new library, as ADR-0048 did. It also
  names the order of the tasks: first the projects, then one primitive each, then the SSH
  composition in `Surl.Protocol.Ssh` (`SshAlgorithmOffer.cs`, `SshCbc.cs`, `SshHmac.cs`). Each
  test vector comes from the algorithm's specification. Any byte curl sends or expects is
  measured with `Record-CurlExchange.ps1` against a pinned build. Never use the Curl port as
  evidence (ADR-0003).
- ADR-0051's "Amended by" line gains the new ADR.

## Acceptance criteria

- [x] A new ADR under `Documentation/Planning/Decisions/`, marked "Decided by Claude under
      Stewart's delegation", amends ADR-0051 decision 2. It states:
      - where `blowfish-cbc`, `cast128-cbc`, `hmac-ripemd160` and
        `hmac-ripemd160@openssh.com` sit in the server order, and under which option
      - which library builds each primitive, and its row in ADR-0002 decision 3's table
      - the tasks that build them, by ID
- [x] ADR-0051's header lists the new ADR under "Amended by".
- [x] `task-planner` has filed the build tasks named in the ADR in `Tasks/Backlog`, with
      dependencies in the order the ADR gives and `touches` naming only their own projects.
      The ADR's task IDs match those files.
- [x] No package is added. Every statement in the ADR is true of the repository.

## Notes

- Decided in ADR-0061 (Claude under Stewart's delegation): the four names are offered only with
  `--allow-weak-ssh-algorithms`, appended after the existing weak entries (ciphers
  `blowfish-cbc`, `cast128-cbc`; MACs `hmac-ripemd160`, `hmac-ripemd160@openssh.com`) so
  nothing negotiated today changes. Why weak: 64-bit blocks and RIPEMD-160, all four removed from
  OpenSSH's server in 7.6.
- Blowfish moves out of BcryptPbkdf into `Surl.Cryptography.Blowfish` (BcryptPbkdf then
  references it) rather than being exposed from BcryptPbkdf: a cipher taken from a library named
  for a key derivation breaks "say what it does". CAST-128 and RIPEMD-160 (with HMAC-RIPEMD-160,
  since the BCL's HMAC cannot take an unknown hash) get one library each.
- Filed by task-planner: BL-254 (projects), BL-255 / BL-256 / BL-257 (one primitive each, in
  parallel), BL-258 (the SSH composition).
- ADR-0002's table lacked the `Rc4` and `BcryptPbkdf` rows ADR-0051 decision 3 added; recorded
  them with an "Amended" line while adding ADR-0061's rows.
- No pinned curl build negotiates these against surl (libssh2 prefers
  `chacha20-poly1305@openssh.com` and `hmac-sha2-256`, which surl offers by default), so the ADR
  says they are proven by unit tests against the specifications' vectors.
- Docs only, no `.cs` or project file touched, so the verify skill was not needed.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0061 decides blowfish-cbc, cast128-cbc and hmac-ripemd160 behind --allow-weak-ssh-algorithms; BL-254 to BL-258 filed to build them
