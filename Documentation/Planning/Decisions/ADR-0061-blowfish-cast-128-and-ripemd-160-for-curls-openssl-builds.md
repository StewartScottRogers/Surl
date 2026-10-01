# ADR-0061 — Blowfish, CAST-128 and RIPEMD-160 for upstream curl's OpenSSL builds

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-252 (FR-039).
- **Amends:** [ADR-0051](ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md)
  decision 2: its weak table gains two ciphers and two MACs (decision 1 below), and its decision 13
  gains the tasks of decision 4. [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md)
  decision 3's table gains three hand-built libraries, and `Surl.Cryptography.BcryptPbkdf`'s row
  changes (decision 2).

## Context

ADR-0051 decision 2 assigned every algorithm name in the `KEXINIT` of the Windows reference pin
(curl 8.21.0 with libssh2 on WinCNG), and said a name outside its lists would get a task. BL-172
then recorded the lists of the Linux and macOS reference pins in `UpstreamCurlBuilds.json`
(curl 8.21.0, stunnel/static-curl, libssh2 1.11.1 on OpenSSL 4.0.1). They are in BL-172's Notes
and pinned by `KexInit_LinuxAndMacOSBuilds_ListTheOpenSslAlgorithms` in
`Surl.Conformance.UnitTests/UpstreamCurlOffersSshAlgorithmsTests.cs` (`OpenSslCipher`,
`OpenSslMac`), which runs on the Linux and macOS CI legs. Those lists hold four names that no row
of ADR-0051 decision 2 covers:

| List | Name | Algorithm | Specification |
| --- | --- | --- | --- |
| cipher | `blowfish-cbc` | Blowfish, 128-bit key, 64-bit block, CBC | RFC 4253 section 6.3; Schneier, 1993 |
| cipher | `cast128-cbc` | CAST-128, 128-bit key, 64-bit block, CBC | RFC 4253 section 6.3; RFC 2144 |
| MAC | `hmac-ripemd160` | HMAC over RIPEMD-160, 20-byte key and MAC | RFC 2286; Dobbertin, Bosselaers, Preneel, 1996 |
| MAC | `hmac-ripemd160@openssh.com` | the same, under OpenSSH's name | as above |

In the OpenSSL cipher list `blowfish-cbc` sits between `aes128-cbc` and `arcfour128`, and
`cast128-cbc` between `arcfour` and `3des-cbc`; the two MACs come last in the MAC list, in the
order above.

The base class library on .NET 10 has neither Blowfish, CAST-128 nor RIPEMD-160, and
`HashAlgorithmName` names no RIPEMD-160, so `SshHmac` (built on the BCL's HMAC) and `SshCbc`
(built on a BCL `SymmetricAlgorithm`) cannot carry them as they are. A Blowfish already exists in
Surl: `BlowfishState` and `BlowfishPiDigits`, internal to `Surl.Cryptography.BcryptPbkdf.UnitLibrary`
(BL-220, ADR-0051 decision 3), with the standard key schedule and the salted one bcrypt adds.

The root `CLAUDE.md` rules that nothing is left out because the BCL lacks a primitive: each
hand-built piece goes in its own `Surl.<Area>.UnitLibrary` with its `.UnitTests` twin, held to the
quality gates, as ADR-0048 and ADR-0051 decision 3 did.

## Decision

### 1. Where the four names sit, and under which option

All four are **offered only with `--allow-weak-ssh-algorithms`**, appended after the entries that
option already adds (ADR-0051 decision 2's weak table), so no default offer changes:

| List | Weak entries, in server order | Weakness |
| --- | --- | --- |
| cipher | `aes256-cbc`, `rijndael-cbc@lysator.liu.se`, `aes192-cbc`, `aes128-cbc`, `3des-cbc`, `arcfour128`, `arcfour`, **`blowfish-cbc`**, **`cast128-cbc`** | the new two: CBC over a 64-bit block (the Sweet32 birthday bound, as for `3des-cbc`) |
| MAC | `hmac-sha1-etm@openssh.com`, `hmac-sha1`, `hmac-sha1-96`, `hmac-md5`, `hmac-md5-96`, **`hmac-ripemd160`**, **`hmac-ripemd160@openssh.com`** | the new two: a 160-bit hash with no modern review, encrypt-and-MAC rather than `-etm` |

Why weak: both ciphers have the 64-bit block that makes `3des-cbc` weak, and OpenSSH itself
removed all four from its server (Blowfish and CAST-128 in 7.6, RIPEMD-160 in 7.6); ADR-0051
decision 5 puts every algorithm a modern server no longer offers behind the one option. Why last:
the entries already in the weak table keep their places, so what surl negotiates today does not
change; the new ones follow in the order the OpenSSL build lists them. With these rows every name
in the Windows, Linux and macOS pins' measured lists is assigned.

Both ciphers use a 16-byte key and an 8-byte IV, derived as RFC 4253 section 7.2 says, pad packets
to 8 bytes, and chain each direction across packets from the last ciphertext block, as the
existing CBC ciphers do (ADR-0051 decision 2.3, item 4). Both MACs are one HMAC with a 20-byte key
and send all 20 bytes.

Against curl's OpenSSL builds nothing negotiates differently: libssh2 takes its own first
algorithm the server offers (RFC 4253 section 7.1), and it lists `chacha20-poly1305@openssh.com`
and `hmac-sha2-256` first, which surl offers by default. The four are for a client that prefers
them or offers nothing else; no pinned upstream curl build does, so they are proven by unit tests
against the specifications' vectors, not by an exchange with curl.

### 2. Which library builds each primitive

Three new libraries, each at the repository root with its `.UnitTests` twin, bytes in and bytes
out, no socket, file or clock:

| Library | Holds | Specification | May reference |
| --- | --- | --- | --- |
| `Surl.Cryptography.Blowfish.UnitLibrary` | Blowfish: the standard key schedule, the salted expansion bcrypt's eksblowfish needs, one 8-byte block encrypted or decrypted | Schneier, 1993; OpenBSD `blf.c`; vectors from Eric Young's ECB table (`vectors-2.txt`) | nothing |
| `Surl.Cryptography.Cast128.UnitLibrary` | CAST-128 with 40- to 128-bit keys, one 8-byte block encrypted or decrypted | RFC 2144, vectors from its Appendix B | nothing |
| `Surl.Cryptography.Ripemd160.UnitLibrary` | RIPEMD-160 and HMAC-RIPEMD-160 | Dobbertin, Bosselaers and Preneel's published vectors; RFC 2286 section 2 | nothing |

**Blowfish moves out of BcryptPbkdf** into its own library and becomes public;
`Surl.Cryptography.BcryptPbkdf.UnitLibrary` then references `Surl.Cryptography.Blowfish.UnitLibrary`.
Why not expose it from BcryptPbkdf: `Surl.Protocol.Ssh` would then take a cipher from a library
named for a key derivation, which breaks "say what it does", and the rule is one hand-built
primitive per library. Why not a second copy: two Blowfishes would be two things to keep right.

HMAC-RIPEMD-160 lives beside RIPEMD-160 because the BCL's `HMAC` cannot take a hash it does not
know, and RFC 2286 gives vectors for the pair. CBC chaining, key derivation and the packet format
stay in `Surl.Protocol.Ssh`, as ADR-0048 decision 3 says of every composition.

ADR-0002 decision 3's table gains the three rows above, each referencing nothing, and
`Surl.Cryptography.BcryptPbkdf.UnitLibrary`'s row (from ADR-0051) may reference
`Surl.Cryptography.Blowfish.UnitLibrary`. `ProtocolIsolationTests` in
`Surl.Protocol.Abstractions.UnitTests` learns the rows when the projects exist (BL-254).
`Surl.Protocol.Ssh` references all three.

Copying Blowfish, CAST-128 or RIPEMD-160 code from the Curl port is allowed; no expected value
comes from it (ADR-0003). Every vector is cited beside it from the specification above.

### 3. What is not built

Nothing of the four is left out. No new option: `--allow-weak-ssh-algorithms` already carries the
weak table, and its help and `--aihelp` text name no algorithm, so they do not change.

### 4. The tasks, in order

| Order | Work | Task | Depends on |
| --- | --- | --- | --- |
| 1 | The three projects and their test twins, `Surl.slnx`, `ProtocolIsolationTests`'s rows | BL-254 | BL-252 |
| 2 | Blowfish moved into `Surl.Cryptography.Blowfish`, BcryptPbkdf referencing it | BL-255 | BL-254 |
| 2 | CAST-128 in `Surl.Cryptography.Cast128` | BL-256 | BL-254 |
| 2 | RIPEMD-160 and HMAC-RIPEMD-160 in `Surl.Cryptography.Ripemd160` | BL-257 | BL-254 |
| 3 | `blowfish-cbc`, `cast128-cbc`, `hmac-ripemd160` and `hmac-ripemd160@openssh.com` in `Surl.Protocol.Ssh` (`SshAlgorithmOffer.cs`, `SshCbc.cs` or a sibling CBC over an 8-byte block, `SshCipherAlgorithm.cs`, `SshHmac.cs`) | BL-258 | BL-255, BL-256, BL-257 |

BL-255, BL-256 and BL-257 touch different projects, so three lanes can build them at once.

## Alternatives considered

- **Leave the four out.** Rejected: the root `CLAUDE.md` rules nothing is left out because the BCL
  lacks a primitive, and ADR-0051 decision 2 promises every measured name a place.
- **Offer them by default.** Rejected: they are weaker than `3des-cbc` and `hmac-md5`, which are
  already behind the option, and OpenSSH no longer offers them.
- **One library for all three primitives.** Rejected: one primitive per library is the rule
  ADR-0048 set, and three libraries let three lanes work at once.
- **Expose Blowfish from BcryptPbkdf.** Rejected in decision 2.

## Consequences

- Six more projects in `Surl.slnx` and three more rows in ADR-0002's table.
- `Surl.Cryptography.BcryptPbkdf` gains its first reference; its public API does not change.
- The four names are covered by unit tests only: no pinned upstream curl build negotiates them
  against surl while surl offers what curl prefers.
