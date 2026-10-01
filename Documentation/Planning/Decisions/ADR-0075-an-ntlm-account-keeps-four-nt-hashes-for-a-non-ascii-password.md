# ADR-0075 — An NTLM account keeps four NT hashes, for a non-ASCII password

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-321, which made and built the decision; recorded by BL-325, since BL-321's lane could not
  write in this folder while another lane held it.
- **Amends:** [ADR-0039](ADR-0039-http-ntlm-challenge-and-ntlmv2-check.md) decision 6, which
  gives each account one NT hash, `MD4(UTF-16LE(password))`.

## Context

[MS-NLMP] section 3.3.1 defines the NT hash as `MD4(UNICODE(Passwd))`, and ADR-0039 decision 6
computes it as `MD4(UTF-16LE(password))`. For an ASCII password every pinned upstream curl build
hashes that. For a non-ASCII password they do not agree, because each build turns the password
it was given into `UNICODE(Passwd)` its own way.

Measured on 2026-09-30 by BL-321 with `Record-CurlExchange.ps1 -ResponsesPerConnection 2`, the
password `pässword` for user `tester`, and the fixed server challenge `0123456789abcdef`, on a
Windows 11 machine with ANSI code page 1252 and OEM code page 437; fixtures and command lines in
`Surl.Authentication.UnitTests/Fixtures/README.md`, "Non-ASCII NTLM password (BL-321)". Every
case answered with NTLMv2 and exited 0:

- **stunnel/static-curl 8.21.0 for Windows** (ADR-0042; `Unicode` feature, SSPI) hands SSPI the
  password as UTF-16 and proves `MD4(UTF-16LE(password))`, from the command line and from a UTF-8
  `-K` file alike.
- **The Windows reference build** (Git for Windows, Schannel, SSPI, no `Unicode` feature) hands
  SSPI an ANSI identity, and SSPI reads its bytes in the OEM code page. From the command line the
  bytes are the argument's Windows-1252 encoding, so it proves
  `MD4(UTF-16LE(cp437(cp1252(password))))` (`pΣssword`, `E4` read in code page 437); from a UTF-8
  `-K` file the bytes are UTF-8, so it proves `MD4(UTF-16LE(cp437(UTF-8(password))))`
  (`p├ñssword`).
- **Upstream curl's own NTLM code**, which the Linux and macOS builds use
  (`Curl_ntlm_core_mk_nt_hash` in `lib/curl_ntlm_core.c` at `curl-8_21_0`), widens each UTF-8
  byte of the password to 16 bits. Read from the source, not measured: no such build is on the
  Windows lane machine; recording it is BL-324.

With one hash an account with a non-ASCII password could log in from only one of these builds.

## Decision

1. **Four hashes per account.** `NtlmPasswordHashes.Compute` gives every NTLM account four NT
   hashes, in this order, each serving the builds named:
   1. `MD4(UTF-16LE(password))`, the specification's: static-curl for Windows, and any client
      handing SSPI a Unicode identity;
   2. MD4 of the password's UTF-8 bytes each widened to 16 bits: upstream curl's own NTLM code,
      the Linux and macOS builds (`NtlmPasswordHashes.WidenedUtf8Index`, also the hash SMB's
      NTLMv1 check uses, ADR-0073 decision 3);
   3. `MD4(UTF-16LE(cp437(cp1252(password))))`: the reference build given the password on the
      command line;
   4. `MD4(UTF-16LE(cp437(UTF-8(password))))`: the reference build given the password in a UTF-8
      `-K` file.

   For an ASCII password all four are the same hash.
2. **An answer proving any of them logs in.** `NtlmHandshake.FindAnsweringAccount` checks the
   NTLMv2 `NTProofStr` against each hash and accepts if one matches, taking that hash's session
   base key.
3. **The code pages are fixed at 1252 and 437**, the ANSI and OEM code pages of a US-English
   Windows, the machine the reference build was measured on. They are not read from the machine
   surl runs on, which says nothing about the client's. A Windows client without the `Unicode`
   feature on a machine with other code pages is not matched for a non-ASCII password; it still
   logs in with an ASCII one.
4. **The extra hashes are accepted as a small widening.** Each extra hash is a deterministic form
   of the same password, so it admits only a near-guess of it: an account `pässword` also
   accepts an answer for `pΣssword` or `p├ñssword`, never an unrelated password. That was judged
   acceptable for a test server that otherwise could not serve the reference build.
5. **The work stays constant** (ADR-0032 section 8). Every hash is checked whatever matches, so
   the time does not say which matched, and the dummy account an unknown user gets keeps four
   random hashes (`NtlmPasswordHashes.ComputeRandom`), so it costs the same four checks and
   matches nothing. Each comparison stays fixed-time.
6. **The encodings come from `CodePagesEncodingProvider.Instance`**, which is in the .NET shared
   framework: no package, and nothing registered process-wide with `Encoding.RegisterProvider`.

## Alternatives considered

- **Keep the specification's hash only.** Rejected: the reference build, the one Surl is measured
  against first, could then never log in with a non-ASCII password.
- **Read the code pages from the machine surl runs on.** Rejected: the hash depends on the
  client's code pages, not the server's, and a server on Linux has none.
- **Let the command line choose which hashes an account keeps.** Rejected for now: four checks
  cost little, and an option to narrow them answers no measured need.

## Consequences

- `AccountBook` computes each `NtlmAccount`'s `NtHashes`, four of them, at start-up, in place
  of one NT hash.
- HTTP NTLM, Negotiate's NTLM and every other user of `NtlmHandshake` accept all four hashes;
  SMB's NTLMv1 check uses the widened UTF-8 hash alone (ADR-0073).
- The Linux and macOS hash is read from the source, not measured; BL-324 records it.
