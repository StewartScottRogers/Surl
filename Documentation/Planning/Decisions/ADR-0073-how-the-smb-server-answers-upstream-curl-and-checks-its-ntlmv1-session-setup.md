# ADR-0073 — How the SMB server answers upstream curl, and how it checks the NTLMv1 session setup

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-283 (FR-050).
- **Amends:** [ADR-0049](ADR-0049-the-mail-servers-sasl-and-apop-logins.md) decision 3's table
  (a new `--auth` word, `ntlmv1`, for SMB only, decision 3 below) and
  [ADR-0007](ADR-0007-the-phase-1-command-line-surface.md)'s note that SMB's default port 445 is
  unmeasured (it is measured now, and is 445). [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md)'s
  SMB rows, [ADR-0010](ADR-0010-the-server-side-tls-contract.md) (`smbs`),
  [ADR-0015](ADR-0015-how-the-content-store-applies-the-exposure-options.md) (uploads),
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) (logins),
  [ADR-0033](ADR-0033-console-log-levels-trace-dumps-and-the-log-file.md) (notes),
  [ADR-0034](ADR-0034-curl-style-help-categories-and-the-manual.md) and
  [ADR-0046](ADR-0046-surl-aihelp-markdown-help-for-ai-agents.md) (help),
  [ADR-0038](ADR-0038-checked-logins-carry-the-login-note-and-the-server-writes-it.md) (the
  login note) and [ADR-0059](ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md)
  (limit or shutdown) are applied as written.
- **Amended:** 2026-09-30 in BL-334, to say what BL-296 built where this ADR was silent or could
  not be built as written: decision 1's session setup and tree connect strings, decision 4's
  session setup out of turn, and decision 8's one note for the idle timeout and the maximum
  duration.

## Context

`surl smb://...` and `surl smbs://...` are to be the server upstream curl's `smb://` and
`smbs://` transfers talk to (FR-050). BL-290 built the SMB version 1 codec in
`Surl.Protocol.Smb` (the eight requests curl sends, their responses, NetBIOS framing) and BL-291
the NTLMv1 arithmetic in `Surl.Authentication` (`NtlmV1Calculation`, with DES and MD4 from
`Surl.Cryptography`). What was left, and what this ADR decides from measurement, is everything
around them: the negotiate response, how shares map onto the content store, how the session
setup is checked through the authentication contract, the status of every refusal, uploads,
the limits, `smbs`, the notes and the help, so that BL-294 to BL-299 are built without a
question and BL-300 knows what to prove.

**What upstream curl sends, from its source** (`lib/smb.c` at tag `curl-8_21_0`, read
2026-09-30, and confirmed byte for byte below): NetBIOS session framing on a direct TCP
connection; `SMB_COM_NEGOTIATE` offering only `NT LM 0.12`; `SMB_COM_SESSION_SETUP_ANDX` with a
24-byte LM response and a 24-byte NT response (NTLMv1, no extended security), the user, the
domain (the part before `/` or `\` in `-u`, else the URL's host) and curl's own OS and LAN
manager strings, all ASCII; `SMB_COM_TREE_CONNECT_ANDX` to `\\<host>\<share>` with service
`?????`; `SMB_COM_NT_CREATE_ANDX`; `SMB_COM_READ_ANDX` 32 KiB at a time or
`SMB_COM_WRITE_ANDX` for `-T`; `SMB_COM_CLOSE`; `SMB_COM_TREE_DISCONNECT`. No directory query,
no Unicode, no signing, no `FLAGS2_NT_STATUS`, so the server answers in DOS error classes.

### What upstream curl 8.21.0 does (measured)

Measured 2026-09-30 with ADR-0030's build, the only pinned Windows build with `smb`:
`C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`, SHA-256
`589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648` (`curl 8.21.0
(x86_64-w64-mingw32) libcurl/8.21.0 OpenSSL/4.0.1 ...`). The Linux and macOS reference builds
are the same tag of the same `lib/smb.c` from the same packager (ADR-0030 decision 3); no lane
ran on them, and only the OS string in the session setup (`x86_64-w64-mingw32` here) is expected
to differ. Each case ran `Record-CurlExchange.ps1 -Raw -RawIdleMilliseconds 400` with
`-Curl <that path>` and `curl -sS -m 15 <args>`; `-Raw` sufficed, because curl sends one request
and waits for its answer, and none of curl's checks depends on a value the server must echo, so
each answer could be a fixed reply given in `\xHH` escapes. No extension of the script was needed.
`U` is `smb://127.0.0.1:18445/share/file.txt`.

**The scripted replies** (hex, NetBIOS header first; the challenge `0123456789ABCDEF`, UID
`0x0064`, TID `0x0007`, FID `0x4000`), which BL-296 to BL-298 can give `-RawReply` again to
re-record any case:

```
negotiate     00000052FF534D427200000000984100000000000000000000000000000000000000000011000003010001000411000000000100000000000000000000000000000000000000080D000123456789ABCDEF5355524C00
session setup 00000029FF534D427300000000984100000000000000000000000000000000006400000003FF00000000000000
tree connect  00000031FF534D427500000000984100000000000000000000000000070000006400000003FF00000000000800413A004E54465300
nt create     00000067FF534D42A200000000984100000000000000000000000000070000006400000022FF000000000040010000000000000000000000000000000000000000000000000000000000000000000000800000000A000000000000000A0000000000000000000000000000
read          00000046FF534D422E0000000098410000000000000000000000000007000000640000000CFF000000FFFF000000000A003C00000000000000000000000B000068656C6C6F20736D620A
write         0000002FFF534D422F00000000984100000000000000000000000000070000006400000006FF0000000A00FFFF000000000000
close         00000023FF534D4204000000009841000000000000000000000000000700000064000000000000
tree disc.    00000023FF534D4271000000009841000000000000000000000000000700000064000000000000
```

An error reply is the request's command with the status in bytes 9 to 12 (class, 0, code as
16 bits little-endian), word count 0 and byte count 0; for example a refused session setup with
`ERRSRV`/`ERRbadpw`: `00000023FF534D4273020002009841` followed by 24 zero bytes.

**A download** (`-u alice:secret U`), exit 0, stdout `hello smb\n`; curl's seven requests:

```
negotiate     0000002FFF534D427200000000184100BA000000000000000000000000001DD700000000000C00024E54204C4D20302E313200
session setup 00000095FF534D427300000000184100BA000000000000000000000000001DD7000000000DFF000000009001000100000000001800180000000000080000005800
              101C21228F73993193C75440547D94B75F3231384D879388   (LM response)
              2FECDD61DA941EC269D46DD130BA93129C4166FE03F21706   (NT response)
              616C69636500 3132372E302E302E3100 7838365F36342D7736342D6D696E67773332 00 6375726C00   ("alice", "127.0.0.1", OS, "curl")
tree connect  00000043FF534D427500000000184100BA000000000000000000000000001DD76400000004FF0000000000000018005C5C3132372E302E302E315C7368617265003F3F3F3F3F00
nt create     00000060FF534D42A200000000184100BA000000000000000000000007001DD76400000018FF000000000C0000000000000000000000008000000000000000000000000007000000010000000000000000000000000D006469725C66696C652E74787400
read          0000003BFF534D422E00000000184100BA000000000000000000000007001DD7640000000CFF00000000400000000000800080000000000000000000000000
close         00000029FF534D420400000000184100BA000000000000000000000007001DD764000000030040000000000000
tree disc.    00000023FF534D427100000000184100BA000000000000000000000007001DD764000000000000
```

(That download's URL was `smb://127.0.0.1:18445/share/dir/file.txt`.) What the bytes show:

- **Header.** Flags `0x18` (canonical and caseless path names), flags2 `0x0041` (long names
  known and used; no Unicode, no NT status, no extended security, no signing), the process ID
  split over `PIDHigh` and `PID`, MID always 0, and the UID and TID curl was given echoed from
  then on. Curl checks no MID, PID or command in a response.
- **Session setup.** Word count 13: `MaxBufferSize` `0x9000`, `MaxMpxCount` 1, `VcNumber` 1,
  the negotiate response's session key echoed, both response lengths 24, capabilities
  `0x00000008` (`CAP_LARGE_FILES`), then the responses, user, domain, OS and `curl`. Curl
  ignores the negotiate response's `MaxBufferSize` (it was 4356 here, and curl still wrote
  32767-byte writes).
- **The responses are NTLMv1 and both are real.** A C# file-based app over
  `Surl.Cryptography`'s `Md4` and `Des` (BL-291's arithmetic) reproduces both for password
  `secret` and challenge `0123456789ABCDEF`: LM `101C21228F73993193C75440547D94B75F3231384D879388`
  is `DESL(LMOWFv1)`, NT `2FECDD61...F21706` is `DESL(MD4(password))`. For an empty password
  (`-u alice:`) curl still sends both: LM `BADA4716C630D691180E163FBDD87CDE5F3231384D879388`,
  NT `3A2EB2B1B13B01B8491AB00C070DD7E1DA0B98040B02C03F`, also reproduced.
- **Tree connect.** Word count 4, no password, path `\\<URL host>\<share>` as written in the URL
  (`\\files.surl.test\Share` with `--resolve`), service `?????`.
- **NT create.** Name relative to the share, backslash-separated, no leading backslash, as
  UTF-8 bytes (`caf%C3%A9%20x/a%2Fb.txt` arrives as `caf\xC3\xA9 x\a\b.txt`: a percent-decoded
  `/` becomes a separator too), `..` passed through under `--path-as-is`, and an empty name
  (`smb://h/share/`) for the share itself. Reading: access `0x80000000` (`GENERIC_READ`), share
  access 7, disposition 1 (`FILE_OPEN`). Uploading: access `0xC0000000` (read and write),
  disposition 5 (`FILE_OVERWRITE_IF`), allocation size 0 (the size is not announced).
  `-T file smb://h/share/sub/` names the local file: `sub\up10.txt`.
- **Read.** Word count 12, offset as 32 + 32 bits, `MaxCount` and `MinCount` 32768. Curl takes
  the data length and offset from the response, stops after a read shorter than 32768 and
  trusts the NT create response's end-of-file only for its progress meter. A 40000-byte file
  came in two reads (32768, 7232) and arrived intact (SHA-256 compared).
- **Write.** Word count 14, at most 32767 data bytes per write (40000 bytes went as 32767 and
  7233), data offset 64, and the next write's offset is the previous offset **plus the count
  the server answered**, not the bytes sent: answering 32768 to a 32767-byte write moved the
  second write to offset 32768 with one byte lost, and answering a short count of 4 to a 10-byte
  write made curl send the other 6 and then hang until `-m` (exit 28). The server must answer
  exactly the count it received.
- **Close and tree disconnect** follow every transfer, and after every error but a refused
  session setup or tree connect.

**Every case** (scripted replies as above unless the row says otherwise):

| # | curl arguments | Server's scripted change | Exit, stderr |
| --- | --- | --- | --- |
| 1 | `-v -u alice:secret smb://127.0.0.1/share/f` (no port) | none (no server) | connects to port **445** |
| 2 | `-u alice:secret U` | none | 0, the file |
| 3 | `-u DOM/alice:secret U`, and `-u DOM\alice:secret U` | none | 0; user `alice`, domain `DOM` in both |
| 4 | `-u alice:secret --resolve files.surl.test:18445:127.0.0.1 smb://files.surl.test:18445/Share/Dir/File.txt` | none | 0; domain and tree path carry `files.surl.test`, case kept |
| 5 | `-u alice:secret -o big.out U` | a 40000-byte file, two reads | 0, file intact |
| 6 | `-u alice: U` | none | 0 (curl sends the empty password's responses) |
| 7 | `U` (no `-u`), and `-u :secret U` | none | **67** `Login denied`, no connection opened |
| 8 | `-u alice:secret smb://127.0.0.1:18445/` and `.../share` | none | **3** `missing share in URL path for SMB`, no connection |
| 9 | `-u alice:secret smb://127.0.0.1:18445/share/` | none | 0; NT create with an empty name |
| 10 | `-u alice:wrong U` | session setup `ERRSRV/ERRbadpw` (`0x00020002`) | **67** `Login denied`; no further request |
| 11 | `-u alice:secret U` | tree connect `ERRSRV/ERRinvnetname` (`0x00060002`) | **78** `Remote file not found`; no further request |
| 12 | the same | tree connect `ERRDOS/ERRnoaccess` (`0x00050001`) | **9** `Access denied to remote resource` |
| 13 | the same | tree connect `ERRSRV/ERRerror` (`0x00010002`), then the server closes | 78 |
| 14 | the same | NT create `ERRDOS/ERRbadfile` (`0x00020001`) | **78**, after a tree disconnect |
| 15 | the same | NT create `ERRDOS/ERRnoaccess` | **9**, after a tree disconnect |
| 16 | the same | read `ERRDOS/ERRnoaccess` | **56** `Failure when receiving data from the peer`, after close and tree disconnect |
| 17 | `-u alice:secret -T up10.txt U` | none | 0 |
| 18 | `-u alice:secret -T up40000.bin U` | writes answered 32767 and 7233 | 0 |
| 19 | `-u alice:secret -T up10.txt U` | NT create `ERRDOS/ERRnoaccess` | **9** |
| 20 | the same | write `ERRDOS` code `0x0070` | **25** `Upload failed (at start/before it took off)`, after close and tree disconnect |
| 21 | `-k -u alice:secret smbs://127.0.0.1:18445/share/file.txt` | `-Tls` (implicit TLS 1.2, throwaway certificate) | 0 |
| 22 | the same without `-k` | `-Tls` | **60** `SSL certificate OpenSSL verify result: self-signed certificate (18)` |
| 23 | `-u alice:secret U` | negotiate `ERRSRV/ERRerror` | **7** `Could not connect to server` |
| 24 | the same | negotiate answered, then the server closes | **28** after 15 s: curl never notices the close |
| 25 | the same | NT create `ERRDOS/ERRbadfile`, then the server closes | **28**: curl sent its tree disconnect and waited |
| 26 | the same | read `ERRSRV/ERRerror`, then the server closes | **28** |
| 27 | the same | close `ERRSRV/ERRerror`, or the tree disconnect left unanswered | **28**, with the file already written to stdout |

Rows 24 to 27 are the finding that shapes decisions 5 and 7: **curl's SMB client does not see
the server close the connection** (`smb_recv_message` treats a zero-byte read as "nothing yet")
and waits for its own `-m`, or for ever without one, whenever it is waiting for an answer. A
server that wants curl to stop must answer, not hang up.

## Decision

### 1. The negotiate response

Built by `SmbResponseEncoder.EncodeNegotiate` (word count 17, NT LM 0.12 without extended
security):

| Field | Value | Why |
| --- | --- | --- |
| `DialectIndex` | the index of `NT LM 0.12` in the request (0 for curl) | the one dialect curl offers; a request without it gets word count 1 and `0xFFFF` ([MS-CIFS] 3.3.5.2), then the close |
| `SecurityMode` | `0x03`: user-level, challenge/response | no share-level security, no signing bits |
| `MaxMpxCount`, `MaxNumberVcs` | 1, 1 | curl has one request outstanding on one circuit |
| `MaxBufferSize` | `--max-message`, capped at 131071 (the largest NetBIOS length) | says the limit decision 7 enforces; curl ignores it (measured) |
| `MaxRawSize` | 65536 | raw mode is not offered, the field is informational |
| `SessionKey` | 0 | curl echoes it; nothing depends on it |
| `Capabilities` | `0x00000018`: `CAP_LARGE_FILES`, `CAP_NT_SMBS` | 64-bit offsets and NT create are what curl uses; no `CAP_UNICODE`, no `CAP_STATUS32` (curl reads DOS errors), no `CAP_EXTENDED_SECURITY` |
| `SystemTime`, `ServerTimeZone` | the injected `TimeProvider`'s UTC now as a `FILETIME`, 0 | no host clock read directly, no time zone disclosed |
| Challenge | 8 bytes from an injected source, fresh per connection | the NTLMv1 responses answer it; a fixed challenge in tests |
| `DomainName` | `SURL` | says nothing about the host (ADR-0006 section 3), as NTLM's target name in ADR-0039 |

The challenge source is a small interface in `Surl.Protocol.Smb` (BL-296), its production form
`RandomNumberGenerator.Fill`; the server never references `Surl.Authentication`'s
`INtlmServerChallengeSource` (ADR-0002 decision 3). A second negotiate on a connection is
answered `ERRSRV/ERRerror`.

The other two responses that carry strings, as `SmbSession` sends them (BL-296), all ASCII and
null-terminated:

| Response | Field | Value | Why |
| --- | --- | --- | --- |
| Session setup | `NativeOS` | empty | says nothing about the host (ADR-0006 section 3); curl reads none of the three strings |
| Session setup | `NativeLanMan` | empty | the same |
| Session setup | `PrimaryDomain` | `SURL` | the negotiate response's `DomainName`, so the two agree |
| Tree connect | `Service` | `A:` | a disk share, which is what every share is (decision 2) |
| Tree connect | `NativeFileSystem` | empty | the content store's file system is the host's, and naming it would disclose it |

The tree connect's `OptionalSupport` is 0.

### 2. Shares are the content store's top-level directories

The URL path curl was given is the content path, as for every other protocol:
`smb://h/docs/a/b.txt` reads what `http://h/docs/a/b.txt` and `ftp://h/docs/a/b.txt` read.

- **The share** is the last backslash-separated component of the tree connect's path (the host
  part is ignored, as `-u`'s domain is). It names a top-level directory of the content store,
  mapped through `ContentStore.MapRequestPath` as `/<share>`. A share that maps to no
  directory - missing, a file, refused by the path rules, a dot-name without
  `--serve-dot-files`, `.surl` (ADR-0031), `IPC$` - is answered `ERRSRV/ERRinvnetname`
  (`0x00060002`, curl 78). There is no share list to enumerate and nothing distinguishes a
  hidden share from a missing one.
- **A file** is the share followed by the NT create's name: each backslash-separated component
  percent-encoded (every byte but RFC 3986's unreserved ones), joined with `/`, and mapped with
  `MapRequestPath`, so every refusal there (`..`, an empty segment, `:`, a control character, a
  reserved device name, a symbolic link outside the root) applies unchanged and is answered as
  absent.
- **Files at the root of the store** are not reachable over SMB, because curl requires a share
  (row 8). That is the cost of the one-to-one URL mapping, accepted over a fixed share name,
  whose URLs would differ from every other protocol's.
- **Up to 16 trees and 16 open files per session.** A 17th tree connect is answered
  `ERRSRV/ERRerror`, a 17th open `ERRDOS/ERRnofids` (`0x00040001`). TIDs and FIDs count from 1
  and are never 0 or `0xFFFF`; the UID of the one session is 1.

### 3. The session setup is NTLMv1 under a new `--auth` word, `ntlmv1`

- **One method, its own word.** The only login curl makes over SMB is NTLMv1, which ADR-0039
  refuses for HTTP: DES of an MD4 hash with no client nonce, which a passive listener can
  crack. ADR-0049 decision 3 shares a word only between mechanisms that "carry the same messages
  and are checked by the same code"; NTLMv1 is neither, so it gets the word `ntlmv1`, **not in
  the default set**, protocols SMB, placed directly after `ntlm` in ADR-0049's table and in the
  `--auth` warning line. An operator who enabled `ntlm` for HTTP's NTLMv2 has not thereby
  accepted NTLMv1. So `surl --auth ntlmv1 -u alice:secret smb://127.0.0.1:445/` is the shortest
  command line that logs curl in.
- **No `--allow-plaintext-auth`, on `smb` or `smbs`.** The responses are computed from the
  password, not the password (ADR-0032 section 3's "plain-text secret" column says no for every
  NTLM). `--auth ntlmv1` is the loosening, and its warning names it; the help says `smbs` keeps
  the responses from a passive listener.
- **What is checked.** The NT response, 24 bytes, against
  `NtlmV1Calculation.ComputeResponse(ComputeNtHashOfWidenedUtf8(password), challenge)` for the
  account whose name equals the session setup's user ordinally (the dictionary every method
  uses), compared with `CryptographicOperations.FixedTimeEquals`. An unknown user runs the same
  calculation against ADR-0032 section 8's dummy account. **The LM response is ignored**: curl
  always sends a real one (measured), it is weaker than the NT response and proves nothing the
  NT response does not, and checking it would only refuse passwords whose LM form is odd (over
  14 bytes, non-ASCII). An NT response that is not 24 bytes is refused as a wrong credential.
- **The domain is not matched.** Accounts have no domain (ADR-0032 section 2); `DOM/alice`,
  `DOM\alice` and plain `alice` all log in as `alice`, which is what curl's users expect from a
  server that has one account book.
- **Outcomes.** `Accepted` (session setup answered with `Action` 0); `AcceptedUnchecked` under
  `--allow-anonymous`, whatever was sent, with `Action` 1 (`SMB_SETUP_GUEST`) and no note
  (ADR-0038); `Refused` - a wrong response, no accounts, `ntlmv1` not in `--auth` - answered
  `ERRSRV/ERRbadpw` (`0x00020002`, curl 67) and then the connection is closed (curl stops at once
  and sends nothing more, row 10). A checked refusal waits ADR-0032 section 8's second first; a
  refusal because `ntlmv1` is not accepted checks nothing and does not wait. Curl cannot send an
  anonymous session setup (row 7), so `--allow-anonymous` is the only guest path.
- **The contract** (BL-294), in `Surl.Protocol.Abstractions`, shaped as ADR-0051's SSH one:

  ```csharp
  public interface ISmbAuthenticationPolicy
  {
      ValueTask<SmbLoginVerdict> CheckSmbNtlmV1LoginAsync(SmbNtlmV1Login login, CancellationToken cancellationToken);
  }

  public sealed record SmbNtlmV1Login(
      string UserName,                    // as sent, ASCII-decoded
      string DomainName,                  // as sent; for the log only
      ReadOnlyMemory<byte> ServerChallenge,
      ReadOnlyMemory<byte> LmResponse,
      ReadOnlyMemory<byte> NtResponse,
      TlsSession? TlsSession);            // null on smb://; for the log only
  // ToString shows the user and domain, never a response.

  public enum SmbLoginOutcome { Accepted, AcceptedUnchecked, Refused }

  public sealed record SmbLoginVerdict(SmbLoginOutcome Outcome, string? AccountName, CheckedLogin? CheckedLogin);
  ```

  `AuthenticationPolicy` implements it (BL-295); `AnonymousAuthenticationPolicy` answers every
  SMB login `AcceptedUnchecked` with no note, as it answers every SSH login (it is the policy
  that "lets everyone in"; BL-294's text saying it refuses is corrected by this decision). The
  server treats any outcome it does not know as `Refused`.
- **The login note** is `CheckedLogin.Note` with method `ntlmv1` and the user as sent:
  `Login accepted: ntlmv1 alice`, `Login refused: ntlmv1 alice`. A refusal because the word is
  not accepted writes `SMB session setup refused: ntlmv1 is not in --auth` instead (the server's
  note, from the verdict's `CheckedLogin` being null with `Refused`).

### 4. Statuses

Every refusal is a DOS-class status curl reads (`ERRDOS` 1, `ERRSRV` 2, `ERRHRD` 3), sent as
`SmbResponseEncoder.EncodeError` with the request's header turned round:

| When | Status | curl |
| --- | --- | --- |
| Session setup refused | `ERRSRV/ERRbadpw` `0x00020002` | 67 |
| A request other than negotiate or session setup before a login | `ERRSRV/ERRbaduid` `0x005B0002` | (curl never does) |
| A session setup before any negotiate, or after a login on the connection | `ERRSRV/ERRerror` `0x00010002` | (curl never does) |
| A second negotiate on the connection (decision 1) | `ERRSRV/ERRerror` `0x00010002` | (curl never does) |
| Unknown share (decision 2) | `ERRSRV/ERRinvnetname` `0x00060002` | 78 |
| A TID that is not connected | `ERRSRV/ERRinvtid` `0x00050002` | |
| File absent, hidden, refused by the path rules, a directory, or the share itself (an empty name) | `ERRDOS/ERRbadfile` `0x00020001` | 78 |
| Upload into a directory that does not exist | `ERRDOS/ERRbadpath` `0x00030001` | 78 |
| Upload refused: uploads off, a hidden or `.surl` location, a directory at the name | `ERRDOS/ERRnoaccess` `0x00050001` | 9 |
| Too many open files | `ERRDOS/ERRnofids` `0x00040001` | |
| A FID that is not open | `ERRDOS/ERRbadfid` `0x00060001` | 56 on read, 25 on write |
| Read on a file opened for writing, write on one opened for reading | `ERRDOS/ERRbadaccess` `0x000C0001` | |
| A write past `--max-filesize` | `ERRHRD/ERRdiskfull` `0x00270003` | 25 |
| The content store fails to read or write | `ERRHRD/ERRgeneral` `0x001F0003` | 56 or 25 |
| A command curl never sends (directory queries, `TRANS2`, `ECHO`, ...) | `ERRSRV/ERRsmbcmd` `0x00400002` | |
| A message that does not decode, or past `--max-message` | `ERRSRV/ERRerror` `0x00010002` | per step |

A directory is answered as absent rather than as "is a directory": listings are off by default
(ADR-0006 section 2), and curl, which cannot list, would fail either way. The status for a
missing file and for a file the server will not show is the same, so a peer learns nothing
about which paths exist (ADR-0006 section 3); the only thing a peer can tell apart is a share
from a file, which `cd` tells on every protocol.

### 5. Reads, uploads and the session's end

- **Reads** go through `ContentStore`: NT create with `FILE_OPEN` and no write access opens a
  file that `GetFileStatus` finds, answering `CreateAction` 1 (`FILE_OPENED`), the four times
  as the file's last write time in `FILETIME`, attributes `0x80` (`FILE_ATTRIBUTE_NORMAL`),
  allocation size and end-of-file the file's length, resource type 0, not a directory. Each
  read answers `min(MaxCount (with MaxCountHigh), 61440, bytes left)` bytes from the requested
  offset through `CopyFileBytesAsync` with a byte range; an offset at or past the end answers 0
  bytes with success. 61440 keeps the response's byte count under 65535 and is above curl's
  32768.
- **Uploads** need `--allow-uploads` (ADR-0006 section 2). An NT create that asks for write
  access or a disposition other than `FILE_OPEN` opens a random-access upload session
  (`ContentStore.OpenUploadAsync` with an opening that creates a missing file and replaces an
  existing one, as `FILE_OVERWRITE_IF` asks), answering `CreateAction` 2 (`FILE_CREATED`) or 3
  (`FILE_OVERWRITTEN`). Each write is `WriteAtAsync(offset, data)` and is answered with exactly
  the data length received (the measured count rule). A write that would take the file past
  `--max-filesize` (ADR-0015) is answered `ERRHRD/ERRdiskfull`, the session is disposed so the
  partial file is deleted, and the FID stays open only so the close curl then sends is answered
  with success. `SMB_COM_CLOSE` commits the upload (`CommitAsync`); a connection that ends with
  an upload open discards it.
- **Keep answering.** Because curl never notices a close while it waits (rows 24 to 27), the
  server answers every request it reads, including the close and tree disconnect curl sends
  after an error, and closes the connection itself only after a refused session setup, after a
  negotiate without NT LM 0.12, at a limit (decision 7), or at shutdown. A tree disconnect frees
  the tree and its open files and leaves the connection open; curl closes it.
- **NetBIOS.** Session messages (type `0x00`) are served; a keep-alive (`0x85`) is ignored; any
  other type, including a session request (`0x81`, which curl never sends on a direct
  connection), or a message that does not start `\xFFSMB`, closes the connection.

### 6. `smbs`

`smbs` is TLS from the first byte (ADR-0010), registered through `ImplicitTlsSchemeServer` as
`smtps` is, with a certificate from `--cert` or `--self-signed`; inside the TLS session the
exchange is identical (row 21). Without `-k` curl refuses the throwaway certificate with 60
(row 22).

### 7. Limits (ADR-0006)

| Limit | SMB behaviour |
| --- | --- |
| `--max-message` (1 MiB) | one SMB message, by its NetBIOS length (17 bits, at most 131071), checked before the body: when the 32-byte header has arrived, the request is answered `ERRSRV/ERRerror`, the rest of the message is read and discarded (bounded by the 17-bit length), the note decision 8 lists is written, and the session goes on, so curl's close and tree disconnect are still answered. Curl's largest message is a 32767-byte write, 32835 bytes framed |
| `--head-timeout` (30 s) | the negotiate request (ADR-0006's "first packet"): the close, nothing sent |
| `--idle-timeout` (120 s) | the close, nothing sent: an idle connection is one on which curl is not waiting for an answer |
| `--max-time` (3600 s) | a request being handled is answered `ERRSRV/ERRerror` within ADR-0059's one-second deadline, then the close; curl then waits for its own `-m` (row 26), the price of a guard that must end the exchange |
| `--max-filesize` (100 MiB) | decision 5 |
| `--max-connections`, `--max-connections-per-address` | the engine's, before any byte |
| Shutdown | no farewell (ADR-0059 decision 4): the close |
| Trees, open files | decision 2 |

### 8. Verbose and trace notes

Notes are `IExchangeLog.Note` at `verbose` and above (ADR-0033 section 3), peer values escaped as
ADR-0006 section 3 says; message bytes are in the engine's `--trace` dumps, with no note per
message. No note carries a response, a challenge or file data.

| When | Note |
| --- | --- |
| A session setup decided | the login note (decision 3) |
| A session setup refused unchecked | `SMB session setup refused: ntlmv1 is not in --auth` |
| A tree connect | `SMB tree connect <share>: connected` or `SMB tree connect <share>: no such share` |
| An open | `SMB open <share>\<name> for reading: <n> bytes`, `... for writing`, or `SMB open <share>\<name> refused: <status name>` |
| A close | `SMB close <share>\<name>: <n> bytes read` or `: <n> bytes written` |
| An upload past the cap | `SMB write <share>\<name> refused: past --max-filesize <m>` |
| A command not served | `SMB command 0x<hh> refused: not supported` |
| A message past the cap | `SMB message of <n> bytes is past --max-message <m>` |
| The head timeout's close | `SMB connection closed: head timeout` |
| The idle timeout's or the maximum duration's close | `SMB connection closed: idle timeout or maximum duration`, after the `ERRSRV/ERRerror` answer when a request was being answered (decision 7) |

The idle timeout and the maximum duration share one note because the server cannot tell them
apart: the engine cancels the exchange alike for both, and `ExchangeContext.IsCancelledForALimit`
says only that a limit fired, not which. The head timeout is the server's own clock on the
negotiate (decision 7), so its note names it. Splitting the other two would need the engine to
say which limit fired, a change to `ExchangeContext` that no other server needs.

### 9. Help and `--aihelp`

Curl has no SMB help category, so the category is Surl's own, as `dict`'s and `mqtt`'s are
(ADR-0034 decision 1): `smb`, `SMB and SMBS protocol`, claiming the schemes `smb` and `smbs`. It
holds every option the server reads: `--directory`, `--allow-uploads`, `--max-filesize`,
`--serve-dot-files`, `--follow-symlinks`, `--max-message`, `--head-timeout`, `--user`,
`--user-file`, `--allow-anonymous`, `--auth`, `--cert`, `--key`, `--self-signed`. No new
option; `--auth` gains the word `ntlmv1` (its help line lists it, not in the default). The
`--aihelp` topic is `smb`; its prose says shares are the top-level directories, curl logs in
with NTLMv1 so the server needs `--auth ntlmv1` and an account (or `--allow-anonymous`), the
domain is ignored, uploads need `--allow-uploads`, and `smbs` needs a certificate; its example
is `surl --auth ntlmv1 -u alice:secret --directory ./files smb://127.0.0.1:445/` with
`curl -u alice:secret smb://127.0.0.1/docs/readme.txt`. BL-299 adds them and grows the pinned
topic list, as root `CLAUDE.md` requires. The default port stays 445 in `SchemeDefaultPorts`
for both schemes (row 1).

### 10. The codec stays in `Surl.Protocol.Smb`; DES is `Surl.Cryptography`'s

BL-290's codec decoded every request measured here, and no other server speaks SMB, so no
separate library is planned (BL-290 needs no re-plan). DES is the hand-built
`Surl.Cryptography.Des` BL-291 added, not the BCL's `DES`, because the BCL refuses the weak keys
NTLMv1 needs (an empty password's LM key is all zero bits; row 6 sends one); MD4 is
`Surl.Cryptography.Md4`.

### 11. What BL-300 proves

Against `surl` over loopback through the conformance harness, with the pinned SMB builds:
ADR-0030's static-curl Windows build, and the Linux and macOS reference builds (Inconclusive,
naming the pin, where none is present). `P` is a temporary `--directory` holding
`files/hello.txt` (`hello smb\n`), `files/big.bin` (40000 bytes), `files/sub/` and
`.hidden/x.txt`; `A` is `--auth ntlmv1 -u alice:secret`; `S` is `smb://127.0.0.1:<p>`.

| surl options | curl command line | Exit, and what is checked |
| --- | --- | --- |
| `--directory P A` | `curl -sS -u alice:secret S/files/hello.txt` | 0, stdout `hello smb\n` |
| the same | `curl -sS -u alice:secret S/files/big.bin` | 0, the 40000 bytes |
| the same | `curl -sS -u WORKGROUP/alice:secret S/files/hello.txt` | 0 |
| the same | `curl -sS -u "WORKGROUP\alice:secret" S/files/hello.txt` | 0 |
| the same | `curl -sS -u alice:wrong S/files/hello.txt` | 67 |
| the same | `curl -sS -u ALICE:secret S/files/hello.txt` | 67 (names match ordinally) |
| the same | `curl -sS S/files/hello.txt` (no `-u`) | 67, from curl itself |
| the same | `curl -sS -u alice:secret S/files` | 3, from curl itself |
| the same | `curl -sS -u alice:secret S/nosuch/hello.txt` | 78 |
| the same | `curl -sS -u alice:secret S/.hidden/x.txt` | 78 |
| the same | `curl -sS -u alice:secret S/files/missing.txt` | 78 |
| the same | `curl -sS -u alice:secret S/files/sub` and `S/files/` | 78 |
| the same | `curl -sS -u alice:secret --path-as-is S/files/../files/hello.txt` | 78 |
| the same | `curl -sS -u alice:secret -T up.txt S/files/up.txt` | 9, nothing stored |
| `--directory P A --allow-uploads` | `curl -sS -u alice:secret -T up.txt S/files/up.txt` | 0, `files/up.txt` byte for byte |
| the same | `curl -sS -u alice:secret -T big.bin S/files/big2.bin` (40000 bytes) | 0, stored byte for byte |
| the same | `curl -sS -u alice:secret -T up.txt S/nodir/up.txt` | 78 |
| `--directory P A --allow-uploads --max-filesize 1000` | `curl -sS -u alice:secret -T big.bin S/files/big3.bin` | 25, no `big3.bin` left |
| `--directory P --auth ntlmv1` (no accounts) | `curl -sS -u alice:secret S/files/hello.txt` | 67 |
| `--directory P -u alice:secret` (`ntlmv1` not accepted) | `curl -sS -u alice:secret S/files/hello.txt` | 67 |
| `--directory P --allow-anonymous` | `curl -sS -u anyone:anything S/files/hello.txt` | 0 |
| `--directory P A --self-signed` on `smbs://` | `curl -sS -k -u alice:secret smbs://127.0.0.1:<p>/files/hello.txt` | 0 |
| the same | without `-k` | 60 |

Each row is pinned from this ADR's measurement of the step it fails at (rows 2 to 22 above) and
its decisions; a disagreement with the pinned build is fixed in the library at fault through a
new task, never by changing the expected exit (ADR-0003).

### 12. Who builds what

| Task | Builds |
| --- | --- |
| BL-294 | decision 3's contract, and `AnonymousAuthenticationPolicy`'s `AcceptedUnchecked` |
| BL-295 | decision 3's check, the `ntlmv1` word in `AuthenticationMethods`, its refusals and notes |
| BL-296 | decisions 1, 2 (shares), 3 (calling the contract), 4, 5 (keep answering, NetBIOS), 7 and 8 |
| BL-297 | decision 5's reads, decision 2's files and bounds |
| BL-298 | decision 5's uploads |
| BL-299 | decisions 6 and 9, and the `--auth` help line's new word |
| BL-300 | decision 11 |

## Alternatives considered

- **One fixed share name serving the store's root** (`smb://h/surl/a.txt`). Rejected: an SMB
  URL would then differ from the HTTP and FTP URLs for the same file, and the fixed name would
  be something curl's users must learn. Top-level directories as shares keep one URL path for
  every protocol, at the cost of files at the root (decision 2).
- **Let `ntlm` accept NTLMv1 too.** Rejected: NTLMv1 is far weaker than the NTLMv2 `ntlm`
  accepts over HTTP, and ADR-0039 refused it there; a word that silently widened to it would
  break the rule that each loosening is named (ADR-0032's rejected umbrella `--insecure`).
- **Accept NTLMv1 by default on `smbs` only.** Rejected: no method's default depends on the
  connection anywhere in ADR-0032, and SMB would then work over `smbs` and fail over `smb` with
  the same command line, which reads as a bug. One word, warned, on both.
- **Require `--allow-plaintext-auth` on `smb://`.** Rejected: NTLMv1 sends no password; that
  option means a password a passive listener reads straight off the wire.
- **Check the LM response too, or refuse a session setup whose LM response is not the NT
  response.** Rejected: curl always sends a real LMv1 response (measured), so the second would
  refuse every curl, and the first adds nothing the NT check does not prove.
- **Close the connection on every refusal or limit.** Rejected by measurement: curl waits for
  an answer until its own `-m` (rows 24 to 27), so a close turns a clear refusal into a timeout.
- **Answer a directory with `ERRDOS/ERRnoaccess`.** Rejected: a refused listing is answered as
  absent (ADR-0006 section 2), and the answer for the share's root then matches the answer for
  a missing file.
- **A separate hand-built library for the codec.** Rejected (decision 10): one server speaks it.
- **Extend `Record-CurlExchange.ps1` with an SMB mode.** Not needed: `-Raw` with fixed replies
  answered every case, because curl echoes nothing the server must compute; the replies are
  recorded above for re-recording.

## Consequences

- SMB is unusable until `--auth ntlmv1` (or `--allow-anonymous`) is given, which is the point:
  NTLMv1 is a weak method an operator chooses by name, and the warning line says so on every
  start. The help and `--aihelp` example show it.
- Files at the root of the content store are not served over SMB.
- A server limit that fires while curl waits leaves curl waiting for its own `-m`; every
  refusal Surl chooses is a status, so only the idle timeout, the maximum duration and shutdown
  can do that.
- Measured on Windows only; the Linux and macOS builds are proved by BL-300 rather than measured
  here, and a difference found there is recorded against this ADR.
