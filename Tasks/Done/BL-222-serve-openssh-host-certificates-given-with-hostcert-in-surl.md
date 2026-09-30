---
id: BL-222
title: Serve OpenSSH host certificates given with --hostcert in Surl.Protocol.Ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-171, BL-168]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests, Surl.Console, Surl.Console.UnitTests, Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md, Documentation/Product/Product-Overview.md, Documentation/Wiki/Glossary.md, Documentation/Planning/Roadmap.md, README.md]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-30
---
# BL-222 — Serve OpenSSH host certificates given with --hostcert in Surl.Protocol.Ssh

## Goal

`surl sftp://…` and `surl scp://…` given `--hostcert <file>` read an OpenSSH host certificate,
check that it certifies one of the `--hostkey` keys, and offer and serve the certificate host-key
algorithms: the certificate goes out as the host key blob and the exchange hash is signed with
the certified key.

## Context

- Decision: ADR-0051 (BL-154) - the certificate names offered and their order, the start-up
  step that reads the file, the typed refusals (not a certificate, not a host certificate,
  unsupported certificate type, malformed, certifies no `--hostkey` key) with their
  `SurlExitCode` and texts, and whether a certificate outside its validity period is refused.
- Specification: OpenSSH `PROTOCOL.certkeys` (openssh-portable, tag `V_9_9_P1`): the
  `ssh-rsa-cert-v01@openssh.com`, `ecdsa-sha2-nistp256-cert-v01@openssh.com`,
  `ecdsa-sha2-nistp384-cert-v01@openssh.com`, `ecdsa-sha2-nistp521-cert-v01@openssh.com` and
  `ssh-ed25519-cert-v01@openssh.com` blobs (nonce, the key's public fields, serial, type,
  key id, valid principals, valid after and before, critical options, extensions, reserved,
  signature key, signature); type 2 is a host certificate. The file is the one-line
  `*-cert.pub` form `ssh-keygen -s <ca> -h` writes. OpenSSH `PROTOCOL` names
  `rsa-sha2-256-cert-v01@openssh.com` and `rsa-sha2-512-cert-v01@openssh.com` (RFC 8332
  signatures over an RSA certificate).
- Offered: `rsa-sha2-512-cert-v01@openssh.com` and `rsa-sha2-256-cert-v01@openssh.com` for an
  RSA certificate, `ssh-rsa-cert-v01@openssh.com` only when weak algorithms are allowed
  (BL-221's setting, if Done; otherwise leave it unoffered and file a follow-up), and the
  ECDSA and Ed25519 certificate names for their keys. Signing is BL-160's (RSA, ECDSA) and
  BL-168's (Ed25519) path with the certified key.
- Surl.Console: read the `--hostcert` files in `CommandLineRunner`'s start-up where BL-171
  reads the `--hostkey` files (ADR-0031 decision 7's order: before any listener binds), turning
  the parser's refusals into ADR-0051's exit codes and texts, and remove BL-158's
  `surl: (2) --hostcert is not available in this build` refusal. If `ManualText` or
  `AiHelpProse` in `Surl.Cli.UnitLibrary` says `--hostcert` is unavailable, correct it in this
  change (root `CLAUDE.md`: behaviour and its AI-help prose change together).
- Fixtures: host keys, a CA key and certificates written by `ssh-keygen` (name its version in
  the test file); test-only keys, never real ones.

## Acceptance criteria

- [x] Fast tests parse an RSA, an ECDSA P-256 and an Ed25519 host certificate fixture, and
      refuse each malformed or wrong case ADR-0051 lists with its typed refusal, including a
      user certificate (type 1) and a certificate whose key matches no `--hostkey` key.
- [x] For each of the three key types, a fast test completes a key exchange where the
      test-side client negotiates the certificate algorithm, receives the certificate as the
      host key blob, and verifies the exchange-hash signature with the certified key.
- [x] A fast test shows `ssh-rsa-cert-v01@openssh.com` is absent from `KEXINIT` unless weak
      algorithms are allowed.
- [x] `Surl.Console.UnitTests` tests show a start with a valid `--hostcert` gets past start-up
      to binding, each ADR-0051 refusal returns its `SurlExitCode` and text before any listener
      binds, and the "not available in this build" refusal for `--hostcert` is gone.
- [x] `AiHelpFactsTests`, `AiHelpTextTests`, `ManualTextTests` and
      `CommandLineRunnerAiHelpTests` pass; `ProtocolIsolationTests` pass.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      is green with no socket opened by the protocol tests; `Measure-CodeQuality.ps1` reports
      100% line and 100% branch coverage, no method above cyclomatic complexity 10, and no
      failing member in `Surl.Protocol.Ssh.UnitLibrary`, `Surl.Console` and (if changed)
      `Surl.Cli.UnitLibrary`.

## Notes

- 2026-09-30 (lane 2): Built. `SshHostCertificate.Read` parses the one-line `*-cert.pub`
  (`PROTOCOL.certkeys`) for RSA, ECDSA P-256/384/521 and Ed25519 keys; `SshHostKeySet.TryAdd`
  (certificate overload) serves it as an `SshCertifiedHostKey` whose `K_S` is the certificate
  blob and whose algorithms are the key's with `-cert-v01@openssh.com` added, signing with the
  key and naming the plain algorithm in the signature blob. `SshAlgorithmOffer.Default` lists
  each certificate name just before its plain one; `ssh-rsa-cert-v01@openssh.com` only with weak
  algorithms (BL-221 was Done, so no follow-up was needed). `SshHostKey.SignRaw` became
  `internal` so the certified key can delegate to the key it certifies.
- Decisions ADR-0051 left open are recorded as its Amendment 2: DSA and `sk-*` certificates are
  "not an OpenSSH host certificate" (no pinned build names them); the server judges neither
  validity dates nor the CA signature; "certifies" is byte equality of the public key blob; a
  second certificate of one type is refused, naming the first file (mirrors `--hostkey`);
  certificates are read after every `--hostkey` and the throwaway key.
- `CommandLineRunner.FindUnavailableOption` held only `--hostcert`, so the check and its
  "not available in this build" text were removed; the exit-code guidance (2 and 37), `--manual`
  and `--aihelp` (auth and ssh topics) now describe `--hostcert`.
- Fixtures: `Surl.Protocol.Ssh.UnitTests/Fixtures/host-certificates`, written by `ssh-keygen` from
  OpenSSH_10.3p1 (Windows' OpenSSH client); commands in that folder's README. The CA private key
  was deleted. `Surl.Console.UnitTests/TestSshCertificateFiles.cs` copies the Ed25519 ones.
- `touches` grew by ADR-0051, Product-Overview.md, Glossary.md, Roadmap.md and the root README.md:
  each said `--hostcert` was refused, which is no longer true. No task in Doing names any of them.
- Measured: `Measure-CodeQuality.ps1` reports 100% line and branch coverage, 0 failing members and
  worst CRAP 10 for `Surl.Protocol.Ssh.UnitLibrary`, `Surl.Console` and `Surl.Cli.UnitLibrary`.
  Not checked against pinned upstream curl live: a conformance run (curl with a `@cert-authority`
  known_hosts line against `surl --hostcert`) belongs in `Surl.Conformance.UnitTests`, outside
  this task's `touches`.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl serves --hostcert OpenSSH host certificates for RSA, ECDSA and Ed25519 host keys, offered before each key's algorithm, with ADR-0051's refusals
