# ADR-0048 — The hand-built SSH primitives live in four libraries: Curve25519, Ed25519, ChaCha20 and Poly1305

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-148.
- **Amends:** [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3: its table of
  horizontal libraries gains the four rows of decision 1 below. Everything else in ADR-0002
  stands.

## Context

The SSH server (`Surl.Protocol.Ssh`, Phase 2) needs Curve25519 key agreement, Ed25519
signatures and the ChaCha20 and Poly1305 primitives. Root `CLAUDE.md`, "Decisions", says
they are built by hand and that each hand-built piece is isolated in its own
`Surl.<Area>.UnitLibrary` with its own `.UnitTests` project, held to the same quality
gates. ADR-0002 decision 3 says a further hand-built library joins its table of horizontal
libraries through a new ADR.

Where things stand on 2026-09-29:

- `Surl.Cryptography.UnitLibrary` holds `Sha512Slash256` (BL-112) and `Md4` (BL-119),
  references nothing, and is the only hand-built library in ADR-0002's table.
  `Surl.Cryptography.UnitLibrary/CLAUDE.md` says the SSH primitives would land there too.
- The plan already names four libraries for them: BL-149 creates the projects, BL-150 to
  BL-153 build one primitive each, and BL-167 to BL-169 use them in `Surl.Protocol.Ssh`.
- The dark factory never gives two lanes tasks whose `touches` overlap, and a task's
  `touches` names projects. Four primitives in one project would be four tasks touching the
  same project, worked one after another.

The base class library already has most of what SSH needs. Microsoft's
[cross-platform cryptography](https://learn.microsoft.com/dotnet/standard/security/cross-platform-cryptography)
page (read on 2026-09-29) lists AES-GCM on Windows, Linux and macOS, and `ChaCha20Poly1305`
on Windows 10 build 20142 and later, on Linux with OpenSSL 1.1.0 and later, and on macOS;
on iOS, tvOS and MacCatalyst from .NET 9 only, on Android from API level 28, and never in
the browser. Code checks at run time with
[`ChaCha20Poly1305.IsSupported`](https://learn.microsoft.com/dotnet/api/system.security.cryptography.chacha20poly1305).
The BCL has no X25519 and no Ed25519 on any platform.

The BCL's `ChaCha20Poly1305` does not help SSH even where it is supported. It is RFC 8439's
AEAD only: a 256-bit key, a 96-bit nonce and a 128-bit tag over associated data and
ciphertext, with no raw keystream and no stand-alone Poly1305. OpenSSH's
`chacha20-poly1305@openssh.com`
([`PROTOCOL.chacha20poly1305`](https://github.com/openssh/openssh-portable/blob/V_9_9_P1/PROTOCOL.chacha20poly1305))
is a different construction: 512 bits of key material split into K_2 (the first 256 bits)
and K_1 (the second); the 4-byte packet length encrypted alone under K_1; Bernstein's
original ChaCha20 with a 64-bit nonce, the packet sequence number, and a 64-bit block
counter; the Poly1305 key taken from K_2's keystream block 0 and the payload encrypted
from block 1; and the tag computed over the encrypted length and the encrypted payload
together, without RFC 8439's padding and length block. So SSH needs raw ChaCha20 and raw
Poly1305 whatever the platform.

## Decision

1. **Four libraries, one per hand-built primitive**, each a folder at the repository root
   with its `.UnitTests` twin, created by BL-149:

   | Library | Holds | Specification | May reference |
   | --- | --- | --- | --- |
   | `Surl.Cryptography.Curve25519.UnitLibrary` | GF(2^255-19) field arithmetic and X25519 | [RFC 7748](https://www.rfc-editor.org/rfc/rfc7748) | nothing |
   | `Surl.Cryptography.Ed25519.UnitLibrary` | Ed25519 key generation, signing and verification | [RFC 8032](https://www.rfc-editor.org/rfc/rfc8032) section 5.1 | `Surl.Cryptography.Curve25519.UnitLibrary` (the field arithmetic) |
   | `Surl.Cryptography.ChaCha20.UnitLibrary` | The ChaCha20 block function and stream, in RFC 8439's 96-bit nonce form and the 64-bit nonce and 64-bit counter form `chacha20-poly1305@openssh.com` uses | [RFC 8439](https://www.rfc-editor.org/rfc/rfc8439) sections 2.1 to 2.4; OpenSSH `PROTOCOL.chacha20poly1305` | nothing |
   | `Surl.Cryptography.Poly1305.UnitLibrary` | The Poly1305 one-time authenticator | RFC 8439 section 2.5 | nothing |

   Each is bytes in, bytes out: no socket, no file, no clock.

2. **They are horizontal libraries.** ADR-0002 decision 3's table gains the four rows,
   with the references in the last column above, so every protocol server may reference
   them as it may `Surl.Cryptography.UnitLibrary`, and `Surl.Protocol.Ssh` does.
   `ProtocolIsolationTests` in `Surl.Protocol.Abstractions.UnitTests` learns the rows when
   BL-149 creates the projects.

3. **What is not a primitive library.** Nothing the BCL offers on Windows, Linux and macOS
   is rebuilt:

   | Need | Comes from |
   | --- | --- |
   | SHA-2 (SHA-256, SHA-384, SHA-512, and Ed25519's SHA-512) | The BCL, `System.Security.Cryptography` |
   | HMAC | The BCL |
   | AES, including the ECB transform AES-CTR is composed from | The BCL |
   | AES-GCM | The BCL, `AesGcm` |
   | ECDH and ECDSA over the NIST curves | The BCL, `ECDiffieHellman` and `ECDsa` |
   | RSA | The BCL |
   | Finite-field Diffie-Hellman arithmetic | The BCL, `System.Numerics.BigInteger` |
   | `chacha20-poly1305@openssh.com`, composed from ChaCha20 and Poly1305 | `Surl.Protocol.Ssh` |
   | AES-CTR, composed from AES-ECB | `Surl.Protocol.Ssh` |
   | The Diffie-Hellman groups and their key exchanges | `Surl.Protocol.Ssh` |

   The BCL's `ChaCha20Poly1305` is not used for SSH, for the reason given in "Context".

4. **This ADR decides where the pieces live, not which algorithms surl offers.** Which key
   exchanges, host-key types, ciphers and MACs the SSH server offers is BL-154's. If that
   work finds upstream curl offering an algorithm that needs another hand-built piece, the
   piece gets its own library by the same rule, in a new ADR.

5. **`Surl.Cryptography.UnitLibrary` keeps what it holds** (`Sha512Slash256`, `Md4`) and is
   not where the SSH primitives go. The statement in
   `Surl.Cryptography.UnitLibrary/CLAUDE.md` that they would land there is superseded by
   this ADR.

6. **Copying from the Curl port is allowed as code.** The Curl port's
   `Curl.Cryptography.UnitLibrary` has `Field25519.cs`, `X25519.cs`, `Edwards25519.cs`,
   `Scalar25519.cs`, `Ed25519.cs`, `ChaCha20.cs` and `Poly1305.cs`. They may be copied into
   the matching library; they are never evidence (ADR-0003). Every test vector comes from
   the specification named in decision 1, cited beside the vector.

## Consequences

Good:

- BL-150 to BL-153 touch four different projects, so four dark factory lanes can build
  them at once.
- Each primitive is held to the quality gates on its own, and its test project holds only
  its own vectors.
- The reference graph states the one dependency between them (Ed25519 on Curve25519's
  field arithmetic) and `ProtocolIsolationTests` enforces it.

Costs and caveats:

- Eight more projects in `Surl.slnx`, and four more rows to keep true in ADR-0002's table
  and `ProtocolIsolationTests`.
- `Surl.Cryptography.UnitLibrary` and the four libraries share a name prefix without one
  referencing the others; the prefix groups them in the flat, ordinal project run and
  implies no dependency.

## Alternatives considered

- **Put the four primitives in `Surl.Cryptography.UnitLibrary`**, as its `CLAUDE.md` said.
  Rejected: four tasks touching one project run one after another, and root `CLAUDE.md`
  isolates each hand-built piece in its own library.
- **One `Surl.Cryptography.Ssh.UnitLibrary` for all four.** Rejected for the same reason,
  and because the name would claim SSH for primitives that TLS, among others, also uses.
- **Use the BCL's `ChaCha20Poly1305` where `IsSupported` is true.** Rejected: SSH's
  construction is not RFC 8439's AEAD, so the raw primitives are needed on every platform.
- **Merge ChaCha20 and Poly1305 into one library.** Rejected: they are independent
  primitives with independent vectors, and separate libraries let two lanes build them.
