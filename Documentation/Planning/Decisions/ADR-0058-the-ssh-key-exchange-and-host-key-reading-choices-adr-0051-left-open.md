# ADR-0058 — The SSH key exchange and host-key reading choices ADR-0051 left open

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-160 (FR-039), and recorded here on 2026-09-30 by BL-231, because BL-173 held
  `Documentation/Planning/Decisions` while BL-160 ran.
- **Amends:** [ADR-0051](ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md)
  decisions 4 (the host keys) and 9 (limits and `SSH_MSG_DISCONNECT`), and decision 2.1 (strict
  key exchange), where they are silent. Nothing ADR-0051 says is changed.

## Context

ADR-0051 decides the SSH transport surl offers upstream curl: the algorithms, the host-key
formats and their refusals, and the `DISCONNECT` codes. BL-160 built the key exchanges
(finite-field groups 14, 16 and 18, ECDH on the NIST curves, and
`diffie-hellman-group-exchange-sha256`) and the host-key file reader in
`Surl.Protocol.Ssh.UnitLibrary`, and met eight questions ADR-0051 does not answer. It chose,
and wrote the choices in its task Notes; this ADR records them as the code now does them, in
`SshDiffieHellman`, `SshNistCurve`, `SshEcdhKeyExchange`, `SshGroupExchangeKeyExchange`,
`SshHostKeyFile`, `SshPkcs8Decryption`, `SshOpenSshKeyDecoder`, `SshPacketReader` and
`SshPacketWriter`.

None of the eight changes a byte upstream curl sends or reads on a successful exchange: each is
what the server does with a client that is broken, hostile or not libssh2, or with a key file
the operator gives. The oracle for what libssh2 sends is libssh2 1.11.1's source (`kex.c`), the
build ADR-0051 measured.

## Decision

### 1. A bad client public value is `DISCONNECT` 2

A client finite-field value e outside 1 < e < p - 1 (RFC 4253 section 8; RFC 8268), or an ECDH
point that is not an uncompressed point of the curve's length, has a coordinate not below the
prime, or is not on the curve (RFC 5656 section 4 and SEC 1 section 3.2.2.1), ends the
connection with `DISCONNECT` 2 `PROTOCOL_ERROR`, `Protocol error`.

**Why.** ADR-0051 decision 9 gives 2 to "a malformed message" and 3 `KEY_EXCHANGE_FAILED` to a
failed negotiation: no common algorithm, or no group for the range. A value off the curve or out
of range is not a negotiation that found nothing; it is a message no conforming client sends,
and it is refused before any secret is computed from it (the small-subgroup and invalid-curve
checks). The verbose note says which check failed; the description does not (ADR-0006 section 3).

### 2. `SSH_MSG_KEX_DH_GEX_REQUEST_OLD` is not answered

Under `diffie-hellman-group-exchange-sha256`, the server reads only
`SSH_MSG_KEX_DH_GEX_REQUEST` (34, three values: min, n, max). The old one-value request (30,
RFC 4419 section 5) is an unexpected message: `DISCONNECT` 2.

**Why.** libssh2 1.11.1 sends the three-value request (`kex.c`, with its
`LIBSSH2_DH_GEX_MINGROUP`, `OPTGROUP` and `MAXGROUP`), so upstream curl never sends 30. RFC 4419
keeps 30 only for old clients, and answering it would add a second hash layout
(`n` alone in H) for no client surl serves.

### 3. Where the server's ephemeral secrets come from

- **Finite field:** the private exponent y is 512 bits (64 bytes, top bit set) drawn from
  `ISshRandomSource`, which production fills from `RandomNumberGenerator`.
- **ECDH:** the ephemeral key pair is `ECDiffieHellman.Create(curve)`, the platform's generator.

**Why.** 512 bits is at least twice the security strength of every group offered (RFC 3526
section 8 estimates at most 200 bits for group 18, the 8192-bit one) and of SHA-512, and keeps
`ModPow` cheap next to a full-size exponent. Drawing it from `ISshRandomSource` lets tests pin
y and so pin f, K, H and the signature byte for byte. The ECDH key is left to the platform's
generator, used for one exchange and disposed: the BCL makes a fresh key pair on a named curve
in one call, and the tests need no pinned server key, because the test client
(`SshTestKeyExchangeClient`) computes K from its own side and checks the signature over H.

### 4. Encrypted PKCS #8 is decrypted by hand from BCL primitives

`-----BEGIN ENCRYPTED PRIVATE KEY-----` is decrypted by `SshPkcs8Decryption`: PBES2 (RFC 8018
section 6.2) with PBKDF2 over HMAC-SHA-1, -256, -384 or -512 and AES-128, -192 or -256 in CBC
mode, from `Rfc2898DeriveBytes.Pbkdf2` and `Aes`, then read as a plain PKCS #8
`PrivateKeyInfo`. Any other scheme, PRF or cipher (PBES1, 3DES, RC2), or an IV that is not one
AES block, is `not a private key surl can read`. PBKDF2's iteration count is not capped: the file
is the operator's own.

**Why.** The BCL's `ImportEncryptedPkcs8PrivateKey` is a method of one algorithm's class: the
caller must know the key is RSA or ECDSA before decrypting it, and the algorithm OID is inside
the encrypted part. Decrypting first lets an encrypted DSA or Ed25519 key be refused for its
type (decision 6), which is the true reason, rather than as a passphrase that does not decrypt
it. PBES2 with PBKDF2 and AES-CBC is what OpenSSL 3 and `ssh-keygen -m PKCS8` write; the older
schemes are weak and neither tool writes them by default.

### 5. A legacy encrypted PEM is not read

A PEM block with RFC 1421 headers (`Proc-Type: 4,ENCRYPTED` and `DEK-Info`, the old OpenSSL
"traditional" encryption of `RSA PRIVATE KEY` and `EC PRIVATE KEY`) is `not a private key surl
can read`.

**Why.** Its key derivation is OpenSSL's `EVP_BytesToKey` over MD5, which the BCL does not
offer and which is too weak to be worth building by hand. Every tool that writes it also writes
PKCS #8 (decision 4) or `openssh-key-v1`, so the operator has a way to give the same key.

### 6. Ed25519 and DSA host keys, until their tasks land

- An Ed25519 host key was `key type ssh-ed25519 is not supported` until BL-168 served
  `ssh-ed25519`. It is now read (amended by BL-168 on 2026-09-30, decided by Claude under
  Stewart's delegation) from PKCS #8, encrypted or not (RFC 8410 section 7: the algorithm
  identifier has no parameters, the private key octets hold a `CurvePrivateKey` OCTET STRING of
  the 32-byte seed), and from `openssh-key-v1` (the 32-byte public key, then the 64-byte private
  key: the seed followed by the public key again). Each of these is "not a private key surl can
  read": algorithm parameters present, private key octets that are not one OCTET STRING, a seed
  that is not 32 bytes, and in `openssh-key-v1` a private key that is not 64 bytes, or either copy
  of the public key differing from the one the seed gives. The attributes and public key a version
  2 `OneAsymmetricKey` may carry after the private key are not read, since the key served is the
  seed's whatever they hold.
- A DSA host key is ADR-0051 decision 4's weak-key refusal
  (`DSA keys of <bits> bits need --allow-weak-ssh-algorithms`) without
  `--allow-weak-ssh-algorithms`, and `key type ssh-dss is not supported` with it, until BL-221
  serves `ssh-dss`.

**Why.** ADR-0051 lists both as accepted key types but assigns their signing to later tasks. A
key the server cannot sign with must be refused before any listener binds (ADR-0051 decision
4's "read when"), and ADR-0051's own "another key type" row is the honest text for it. The weak
refusal comes first for DSA so that an operator who gives one learns about the option before
learning the build cannot use it yet.

### 7. Naming a key type surl does not know

An unsupported PKCS #8 algorithm is named in the `key type <type> is not supported` text by its
object identifier (for example `key type 1.2.840.113549.1.3.1 is not supported`); an EC key on
a curve other than P-256, P-384 or P-521 as `ecdsa on curve <oid>`.

**Why.** An unknown algorithm has no name surl could know, and the OID is what `openssl asn1parse`
prints, so the operator can look it up. It is the key's type, not key material, so ADR-0051
decision 4's "no text holding key material" holds.

### 8. Only the client's sequence number is checked for wrap in a strict first exchange

Under strict key exchange (ADR-0051 decision 2.1; OpenSSH `PROTOCOL`, "kex-strict"), a client
packet that would wrap the read sequence number past 2^32 - 1 before the first `NEWKEYS` is
`DISCONNECT` 2. The server's write sequence number has no such check.

**Why.** The rule exists so that a peer cannot slide the sequence numbers by sending packets
nobody authenticates (the Terrapin attack). The client controls how many packets it sends, so
its counter can reach the wrap. The server writes at most four packets in its first exchange
(`KEXINIT`, for group exchange its `GROUP`, the method's reply, then `NEWKEYS`), so its own
counter cannot come near it, and a check that can never fire would be a branch no test reaches.

## Alternatives considered

- **`DISCONNECT` 3 for a bad public value.** Rejected: 3 means the negotiation found nothing
  (decision 9's table), and a value off the curve is a malformed message.
- **Answering `GEX_REQUEST_OLD`.** Rejected: no client surl serves sends it (decision 2).
- **`ImportEncryptedPkcs8PrivateKey`, trying each algorithm in turn.** Rejected: every failure
  looks like a wrong passphrase, and a DSA or Ed25519 key would be refused for the wrong reason.
- **Reading legacy encrypted PEM by building `EVP_BytesToKey` over MD5.** Rejected: an MD5-based
  derivation adds nothing a PKCS #8 or OpenSSH key does not already give.
- **A full-size private exponent.** Rejected: many times slower in group 18, with no security
  gained past the group's own strength.

## Consequences

- `Surl.Protocol.Ssh.UnitLibrary` already does all eight; this ADR changes no code.
- BL-168 replaced decision 6's Ed25519 refusal and amended it; BL-221 is to replace its DSA
  refusal and amend this ADR when it lands.
- ADR-0051's Consequences name this ADR.
