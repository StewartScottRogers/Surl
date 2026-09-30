---
id: BL-258
title: Offer blowfish-cbc, cast128-cbc and hmac-ripemd160 behind --allow-weak-ssh-algorithms
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-255, BL-256, BL-257]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-039
created: 2026-09-30
completed: 2026-09-30
---
# BL-258 — Offer blowfish-cbc, cast128-cbc and hmac-ripemd160 behind --allow-weak-ssh-algorithms

## Goal

With `--allow-weak-ssh-algorithms`, surl's SSH server offers and runs the ciphers
`blowfish-cbc` and `cast128-cbc` and the MACs `hmac-ripemd160` and `hmac-ripemd160@openssh.com`
that upstream curl 8.21.0's OpenSSL builds offer; without the option it offers none of them.

## Context

- ADR-0061 (`Documentation/Planning/Decisions/ADR-0061-blowfish-cast-128-and-ripemd-160-for-curls-openssl-builds.md`)
  amends ADR-0051 decision 2
  (`Documentation/Planning/Decisions/ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md`):
  - weak ciphers, offered only with `--allow-weak-ssh-algorithms`, gain `blowfish-cbc` then
    `cast128-cbc`, appended after `arcfour`;
  - weak MACs gain `hmac-ripemd160` then `hmac-ripemd160@openssh.com`, appended after
    `hmac-md5-96`.
  ADR-0061 already records this; updating ADR-0051's table is not this task's work.
- The names come from upstream curl 8.21.0's Linux and macOS reference pins (libssh2 1.11.1 on
  OpenSSL 4.0.1), measured by BL-172 and pinned by
  `Surl.Conformance.UnitTests/UpstreamCurlOffersSshAlgorithmsTests.cs` (`OpenSslCipher`,
  `OpenSslMac`).
- Primitives, each hand-built in its own horizontal library (ADR-0061's rows in ADR-0002
  decision 3's table):
  - Blowfish: `Surl.Cryptography.Blowfish.UnitLibrary` (BL-255)
  - CAST-128: `Surl.Cryptography.Cast128.UnitLibrary` (BL-256)
  - RIPEMD-160 and HMAC-RIPEMD-160: `Surl.Cryptography.Ripemd160.UnitLibrary` (BL-257)
  `Surl.Protocol.Ssh.UnitLibrary.csproj` adds a `ProjectReference` to each of the three.
  `ProtocolIsolationTests` already allows them (BL-254).
- Where the work lands in `Surl.Protocol.Ssh.UnitLibrary`:
  - `SshAlgorithmOffer.cs`: `WeakCipher` and `WeakMac` gain the names, in the order above.
  - `SshCbc.cs` wraps a BCL `SymmetricAlgorithm` (`Aes`, `TripleDES`) and chains each packet's
    first block from the last ciphertext block of the packet before (RFC 4253 section 6.3;
    ADR-0051 decision 2.3 item 4). Blowfish and CAST-128 are not `SymmetricAlgorithm`s, so add
    a sibling `ISshPacketCipher` that runs CBC over an 8-byte block function (for example
    `SshBlockCbc`, named for what it does), with the same chaining across packets and the same
    "no bytes leave the chain as it is" rule `SshCbc.Chain` follows.
  - `SshCipherAlgorithm.cs`: `blowfish-cbc` and `cast128-cbc` with 16-byte keys and 8-byte IVs
    (RFC 4253 section 6.3: `blowfish-cbc` is Blowfish with a 128-bit key; `cast128-cbc` is
    CAST-128 with a 128-bit key), block size 8. Its `ForName` summary lists them.
  - `SshHmac.cs` is a record over the BCL's `HashAlgorithmName`, which has no RIPEMD-160. Give
    it a way to compute HMAC-RIPEMD-160 through `Surl.Cryptography.Ripemd160` without widening
    the other MACs' behaviour. `hmac-ripemd160` and `hmac-ripemd160@openssh.com` are the same
    MAC: 20-byte key, 20-byte MAC, computed over the packet in the clear (not encrypt-then-MAC).
    RFC 4253 section 6.4 names neither; they are OpenSSH's names, and libssh2 1.11.1 offers both.
    The type summary lists them.
  - `Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md` names the three new libraries beside
    `Surl.Cryptography.Rc4.UnitLibrary`.
- Tests to extend in `Surl.Protocol.Ssh.UnitTests`: `SshAlgorithmOfferTests`
  (`Default_WithoutWeakAlgorithms_ListsNoWeakName`,
  `Default_WithWeakAlgorithms_AppendsEachListsWeakNamesInTheDecisionsOrder`),
  `SshCipherAlgorithmTests.ForName_BuiltCipher_IsKeyedAsRfc4253Says`, `SshHmacTests`, and
  `SshPacketProtectionTests.EveryProtection` (drives
  `PacketsBothWays_UnderEachProtection_AreOpenedAndAnsweredInOrder` and
  `FlippedByte_UnderEachProtection_IsAnsweredDisconnect5`).
- The `--aihelp` text: if an `AiHelpProse` paragraph in `Surl.Console` lists the weak SSH
  algorithms by name, that is outside this task's `touches`; file a follow-up task rather than
  editing it here.
- Never use the Curl port as evidence (ADR-0003); code may be copied from it. No package.

## Acceptance criteria

- [x] `SshAlgorithmOfferTests.Default_WithWeakAlgorithms_AppendsEachListsWeakNamesInTheDecisionsOrder`
      pins the cipher list ending
      `"3des-cbc", "arcfour128", "arcfour", "blowfish-cbc", "cast128-cbc"` and the MAC list ending
      `"hmac-md5-96", "hmac-ripemd160", "hmac-ripemd160@openssh.com"`, and
      `Default_WithoutWeakAlgorithms_ListsNoWeakName` shows none of the four names without the
      option; both pass.
- [x] `SshCipherAlgorithmTests.ForName_BuiltCipher_IsKeyedAsRfc4253Says` has rows
      `("blowfish-cbc", 16, 8, 8)` and `("cast128-cbc", 16, 8, 8)` and passes.
- [x] The new CBC cipher's tests pass for: Eric Young's Blowfish CBC vector from Schneier's
      `vectors-2.txt` (16-byte key, cited beside the test), encrypted whole and block by block
      across calls, giving the same ciphertext; a CAST-128 CBC round trip across several calls;
      and encrypting or decrypting no bytes leaving the chain as it is.
- [x] `SshHmacTests` has a test, citing RFC 2286 section 2, that `hmac-ripemd160` and
      `hmac-ripemd160@openssh.com` each tag an RFC 2286 test case with its listed 20-byte digest,
      and that both have `KeyLength` 20, `MacLength` 20 and are not encrypt-then-MAC.
- [x] `SshPacketProtectionTests.EveryProtection` includes `blowfish-cbc` and `cast128-cbc`, each
      with at least `hmac-sha2-256`, and `hmac-ripemd160` and `hmac-ripemd160@openssh.com` beside
      `aes128-ctr`; `PacketsBothWays_UnderEachProtection_AreOpenedAndAnsweredInOrder` and
      `FlippedByte_UnderEachProtection_IsAnsweredDisconnect5` pass for every new row.
- [x] `Surl.Protocol.Ssh.UnitLibrary.csproj` references the three libraries;
      `ProtocolIsolationTests` passes.
- [x] `dotnet build Surl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes; no test needs `TestCategory=Integration`.
- [x] Quality gates for `Surl.Protocol.Ssh.UnitLibrary`: 100% line and 100% branch coverage,
      cyclomatic complexity at most 10 per method (`CA1502`), CRAP at most 30
      (`powershell -NoProfile -File Measure-CodeQuality.ps1`).
- [x] Tests are platform-neutral.

## Notes

- Proving the exchange end to end with a pinned upstream curl build that offers these names
  (the Linux or macOS reference pin) is conformance work in `Surl.Conformance.UnitTests`, outside
  this task's `touches`; if the conformance stage finds it missing, it files a follow-up task.
  It was missing: filed as BL-260.
- Delivered: `SshBlockCbc` runs CBC over a `SshBlockFunction` delegate (Blowfish's or
  CAST-128's `EncryptBlock`/`DecryptBlock`), chaining across calls and leaving the chain as it
  is for no bytes. `SshHmac` keeps its record shape (so record equality in the tests still
  holds) and marks RIPEMD-160 with its own `HashAlgorithmName` value, `Ripemd160HashName`;
  `Tag` sends only that one to `HmacRipemd160.HashData`, every other MAC still to the BCL.
- Choice: `SshPacketProtectionTests.Create_MacNotBuiltBesideAnAesCtrCipher_IsNull` and
  `HmacForName_NoMacOrOneNotBuilt_IsNull` used `hmac-ripemd160` as their unbuilt MAC; they now
  use `umac-64@openssh.com`, which surl still does not build.
- The SSH tests' own client (`SshTestPacketProtection`) gained Blowfish and CAST-128 CBC and
  HMAC-RIPEMD-160, run one block or message at a time over the hand-built primitives (as it
  already does with `Rc4`), so the server's `SshBlockCbc` is not checked against itself.
  `SshProtocolServerTests.KexInit_WithWeakAlgorithms_...` pins the four new names in the KEXINIT.
- No `AiHelpProse` paragraph lists the weak SSH algorithms by name, so no `--aihelp` follow-up.
- Verified: `dotnet build Surl.slnx -warnaserror` clean; fast tests all green (SSH 1017);
  `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` reports 0 failing members;
  `dotnet format --verify-no-changes` clean for both projects.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. surl's SSH server offers and runs blowfish-cbc, cast128-cbc, hmac-ripemd160 and hmac-ripemd160@openssh.com with --allow-weak-ssh-algorithms
