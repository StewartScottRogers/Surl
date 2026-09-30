---
id: BL-154
title: Decide the SSH transport, host key and user authentication surl offers upstream curl
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-148]
touches: [Documentation/Planning/Decisions]
requirement: FR-039
created: 2026-09-29
completed:
---
# BL-154 — Decide the SSH transport, host key and user authentication surl offers upstream curl

## Goal

An accepted ADR decides, from measurement of the pinned upstream curl 8.21.0 build, the SSH
transport `Surl.Protocol.Ssh` offers (identification string, algorithm lists in order), where
its host key comes from and the options that name it, how user authentication works and the
contract it calls, the limits, the log notes and the exit codes, so BL-156 to BL-172 can be
built without a question.

## Context

- Every pinned build carries libssh2/1.11.1 (`UpstreamCurlBuilds.json`: the Windows reference
  build `C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256 `0E7737...8778`; the Linux and
  macOS reference builds are static OpenSSL builds).
- **Measure first (ADR-0003).** `Record-CurlExchange.ps1 -Raw -RawReplyFirst -RawReply
  'SSH-2.0-surl\r\n'` with `-sS -v sftp://127.0.0.1:<P>/x` and with `scp://...` records curl's
  identification line and its `SSH_MSG_KEXINIT` (RFC 4253 sections 4.2 and 7.1), which travel
  in clear: its kex, host-key, cipher, MAC and compression name-lists, and any pseudo-algorithms
  such as `ext-info-c` or `kex-strict-c-v00@openssh.com` if present. Repeat with
  `--compressed-ssh`. Put the recordings under `Surl.Protocol.Ssh.UnitTests/Fixtures/` only if
  this task's touches allowed it; they do not, so keep them in the ADR as hex and as the
  decoded lists, and BL-159 turns them into fixtures. The Linux and macOS reference builds'
  lists are recorded on CI by BL-172; say so.
- **Algorithms.** Offer every algorithm the measured lists hold that surl can build from the
  BCL or from BL-148's hand-built libraries, in a stated server preference order. The plan
  already has: BCL key exchanges and host keys (BL-160), BCL ciphers and MACs (BL-161),
  `curve25519-sha256` (BL-167), `ssh-ed25519` (BL-168), `chacha20-poly1305@openssh.com`
  (BL-169), compression (BL-170). Nothing curl offers is left out because it is hard (root
  `CLAUDE.md`); if the lists hold an algorithm no planned task covers (a DSA host key, a
  post-quantum hybrid kex, `hmac-sha1`, ...), decide it here and have `task-planner` file its
  task, listed in this task's Log. Decide strict kex and `ext-info` handling if curl asks for
  them.
- **Identification string.** ADR-0006 section 3: no version number in any banner, so e.g.
  `SSH-2.0-surl` (RFC 4253 section 4.2).
- **Host key.** Decide the source (a file option, which formats: OpenSSH `openssh-key-v1`
  unencrypted, PKCS#8 PEM, ...; one key per algorithm or several), whether a start with an
  `scp`/`sftp` listen URL and no host key is refused before any listener binds (as a secure
  listen URL without `--cert` is, ADR-0032 section 10) or served with a throwaway key only when
  asked (as `--self-signed` does for TLS), the option names (curl's name where one has a server
  meaning, ADR-0007 section 1), their `--help` descriptions and categories (ADR-0034), and every
  refusal's exit code and text (ADR-0005 section 3 numbering, ADR-0007 section 5 texts). If a
  new `SurlExitCode` member is needed, name it and its number; BL-158 adds it.
- **curl's host-key check.** Cite, with the curl version the page documents, what curl's
  manual (https://curl.se/docs/manpage.html: `--hostpubsha256`, `--hostpubmd5`, `-k`) and
  libcurl's `CURLOPT_SSH_KNOWNHOSTS` page say about an unknown host key; a canned server cannot
  get past the key exchange, so what the pinned build actually does is measured by BL-172
  against surl and the ADR says so. Decide how surl's host-key fingerprint is shown to the
  operator (a verbose note, the `Listening on` line untouched, ADR-0007 section 7), so a test
  can pass `--hostpubsha256`.
- **User authentication.** The methods named in `SSH_MSG_USERAUTH_FAILURE` and their order
  (RFC 4252: `none`, `password`, `publickey`; `keyboard-interactive` (RFC 4256) if libssh2 as
  curl drives it uses it - cite curl's `CURLOPT_SSH_AUTH_TYPES` documentation); where public
  keys come from (an authorized-keys option and its file format, mapping keys to account names
  from `--user`/`--user-file`); ADR-0032's three criteria for servers not yet built; SSH's
  password is not plain-text because SSH encrypts first (ADR-0032). The contract for BL-156:
  a password login that the policy does not refuse as plain-text, and a public-key login
  ("is this key authorized for this user"; the SSH server verifies the signature itself).
  **Shape it as a new interface beside `IAuthenticationPolicy`**, or otherwise so that no
  existing implementer changes: `Surl.Authentication`'s `AuthenticationPolicy` and the test
  doubles in `Surl.Protocol.Http.UnitTests` and `Surl.Protocol.Mqtt.UnitTests` implement
  `IAuthenticationPolicy`, and BL-156 touches only Abstractions. Name the `CheckedLogin`
  method words (ADR-0038). Decide the attempts allowed per connection and
  `--allow-anonymous`'s meaning for SSH.
- **Limits and refusals** (ADR-0006 sections 1 and 5): an SSH packet is a framed message bounded
  by `--max-message`; the identification exchange and key exchange run under the head timeout;
  which `SSH_MSG_DISCONNECT` reason code answers each limit, and what a connection past a
  connection limit gets.
- **Logs** (ADR-0033): the engine's verbose and trace output shows ciphertext; decide which
  decrypted-message notes the SSH server writes at verbose, never a password, key or session
  secret (ADR-0032 section 8).
- **Help.** The protocol category's name and description for `scp` and `sftp` (ADR-0034
  decision 1's naming rule), for BL-171.

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude
      under Stewart's delegation", records each measurement with the build path, its SHA-256,
      the tool and arguments, the date, and the decoded name-lists.
- [ ] It decides every point in Context: identification string; each algorithm list in server
      order with the task that builds each entry; host-key source, options, descriptions,
      categories, refusals, exit codes and texts; fingerprint display; user-auth methods, the
      authorized-keys source and format, the contract types (as C#, as ADR-0032 section 6 did)
      that leave `IAuthenticationPolicy`'s implementers unchanged; attempts; `--allow-anonymous`;
      limits and `DISCONNECT` codes; verbose notes; the help category.
- [ ] Every algorithm in the measured lists is either assigned to a task on the board or
      decided with a task filed by `task-planner`, listed in this task's Log.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the ADR.

## Notes

## Log

- 2026-09-29: Created.
