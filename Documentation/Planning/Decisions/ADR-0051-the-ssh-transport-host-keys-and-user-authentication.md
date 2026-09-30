# ADR-0051 — The SSH transport, host keys and user authentication surl offers upstream curl

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-154 (FR-039, FR-040).
- **Amends:** [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3's table:
  `Surl.Cryptography.Rc4` and `Surl.Cryptography.BcryptPbkdf` join it (decision 3 below), each
  referencing nothing, as [ADR-0048](ADR-0048-the-hand-built-ssh-primitive-libraries.md)'s four
  primitive libraries do. [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  section 9's warnings grow by two lines (decision 11). `IAuthenticationPolicy` (ADR-0032 section 6,
  amended by [ADR-0038](ADR-0038-checked-logins-carry-the-login-note-and-the-server-writes-it.md))
  is unchanged; SSH logins get a new interface beside it (decision 7).

## Context

`surl scp://...` and `surl sftp://...` are to be the server upstream curl's `scp://` and
`sftp://` transfers talk to. Every pinned build carries libssh2/1.11.1
(`UpstreamCurlBuilds.json`). SSH is a binary protocol that encrypts before it authenticates, so a
canned server can record what curl sends in clear - its identification line and its
`SSH_MSG_KEXINIT` (RFC 4253 sections 4.2 and 7.1) - and nothing after. This ADR decides, from that
measurement, the transport `Surl.Protocol.Ssh` offers, where its host keys come from, how users
log in and the contract they log in through, the limits, the log notes and the help category, so
BL-156 to BL-172 are built without a question. How SCP and SFTP requests are answered is BL-155's
ADR.

### What upstream curl 8.21.0 sends (measured)

- **Build:** the Windows reference build, `C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
  `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0
  (x86_64-w64-mingw32) libcurl/8.21.0 Schannel ... libssh2/1.11.1`, on 2026-09-29.
- **Tool:** `Record-CurlExchange.ps1 -Port 47301 -Raw -RawReplyFirst -RawReply 'SSH-2.0-surl\r\n'
  -CurlArgs <args>`: the recorder sends `SSH-2.0-surl` CR LF at once and records what curl sends
  until it goes quiet, then hangs up.
- **Runs** (`<P>` is `127.0.0.1:47301`):

  | Case | `CurlArgs` | Exit | What curl did |
  | --- | --- | --- | --- |
  | A | `-sS -v sftp://<P>/x` | 2 | nothing on the wire: `curl: Could not find a known_hosts file`, `curl: (2) Failed initialization` |
  | B | `-sS -v scp://<P>/x` | 2 | the same as A |
  | C | `-sS -v sftp://<P>/x --compressed-ssh` | 2 | the same as A |
  | D | `-sS -v sftp://<P>/x -k` | 2 | identification and `KEXINIT`, then `(2) Failure establishing ssh session: -1, Unable to exchange encryption keys` when the recorder hung up |
  | E | `-sS -v scp://<P>/x -k` | 2 | the same as D |
  | F | `-sS -v sftp://<P>/x -k --compressed-ssh` | 2 | the same as D, with the compression lists below |
  | G | `-sS -v sftp://<P>/x --hostpubsha256 47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=` | 2 | the same as D: no `known_hosts` file is needed |
  | H | `-sS -v sftp://<P>/x --knownhosts <an empty file>` | 2 | `* SSH: did not find host '127.0.0.1' in '<file>'`, then the same as D |
  | I | `-sS -v sftp://<P>/x -k -u user:secret` | 2 | the same as D; `* SSH: user 'user'` (without `-u`: `* SSH: user ''`) |

  Runs D to I also note `* SSH: libssh2 cryptography backend: WinCNG`. Every run's bytes are
  the same except the 16-byte cookie and the random padding: D, E, G, H and I byte for byte, F
  in its compression lists (checked by comparing the recordings outside those fields).

- **Identification line:** `SSH-2.0-libssh2_1.11.1` CR LF, sent after the server's.
- **Run D, 1112 bytes** (identification line, then one unencrypted packet: `packet_length`
  `0x0000043C`, `padding_length` 11, message 20 `SSH_MSG_KEXINIT`, cookie, ten name-lists,
  `first_kex_packet_follows` 0, reserved 0, padding):

  ```
  5353482D322E302D6C6962737368325F312E31312E310D0A0000043C0B14BE006AA04B6206A80970B74D2640F06E0000
  01006469666669652D68656C6C6D616E2D67726F75702D65786368616E67652D7368613235362C6469666669652D6865
  6C6C6D616E2D67726F757031362D7368613531322C6469666669652D68656C6C6D616E2D67726F757031382D73686135
  31322C6469666669652D68656C6C6D616E2D67726F757031342D7368613235362C6469666669652D68656C6C6D616E2D
  67726F757031342D736861312C6469666669652D68656C6C6D616E2D67726F7570312D736861312C6469666669652D68
  656C6C6D616E2D67726F75702D65786368616E67652D736861312C6578742D696E666F2D632C6B65782D737472696374
  2D632D763030406F70656E7373682E636F6D000000827273612D736861322D3531322C7273612D736861322D3235362C
  7273612D736861322D3531322D636572742D763031406F70656E7373682E636F6D2C7273612D736861322D3235362D63
  6572742D763031406F70656E7373682E636F6D2C7373682D7273612C7373682D7273612D636572742D763031406F7065
  6E7373682E636F6D0000009763686163686132302D706F6C7931333035406F70656E7373682E636F6D2C616573323536
  2D6374722C6165733139322D6374722C6165733132382D6374722C6165733235362D6362632C72696A6E6461656C2D63
  6263406C797361746F722E6C69752E73652C6165733139322D6362632C6165733132382D6362632C617263666F757231
  32382C617263666F75722C336465732D6362630000009763686163686132302D706F6C7931333035406F70656E737368
  2E636F6D2C6165733235362D6374722C6165733139322D6374722C6165733132382D6374722C6165733235362D636263
  2C72696A6E6461656C2D636263406C797361746F722E6C69752E73652C6165733139322D6362632C6165733132382D63
  62632C617263666F75723132382C617263666F75722C336465732D6362630000009D686D61632D736861322D3235362C
  686D61632D736861322D3235362D65746D406F70656E7373682E636F6D2C686D61632D736861322D3531322C686D6163
  2D736861322D3531322D65746D406F70656E7373682E636F6D2C686D61632D736861312C686D61632D736861312D6574
  6D406F70656E7373682E636F6D2C686D61632D736861312D39362C686D61632D6D64352C686D61632D6D64352D393600
  00009D686D61632D736861322D3235362C686D61632D736861322D3235362D65746D406F70656E7373682E636F6D2C68
  6D61632D736861322D3531322C686D61632D736861322D3531322D65746D406F70656E7373682E636F6D2C686D61632D
  736861312C686D61632D736861312D65746D406F70656E7373682E636F6D2C686D61632D736861312D39362C686D6163
  2D6D64352C686D61632D6D64352D3936000000046E6F6E65000000046E6F6E650000000000000000000000000026B11F
  A0183A78EF01D0F3
  ```

- **Run F, 1152 bytes** (`packet_length` `0x00000464`, `padding_length` 7):

  ```
  5353482D322E302D6C6962737368325F312E31312E310D0A000004640714A34A9E729FCF919768A8EE2A29147DA00000
  01006469666669652D68656C6C6D616E2D67726F75702D65786368616E67652D7368613235362C6469666669652D6865
  6C6C6D616E2D67726F757031362D7368613531322C6469666669652D68656C6C6D616E2D67726F757031382D73686135
  31322C6469666669652D68656C6C6D616E2D67726F757031342D7368613235362C6469666669652D68656C6C6D616E2D
  67726F757031342D736861312C6469666669652D68656C6C6D616E2D67726F7570312D736861312C6469666669652D68
  656C6C6D616E2D67726F75702D65786368616E67652D736861312C6578742D696E666F2D632C6B65782D737472696374
  2D632D763030406F70656E7373682E636F6D000000827273612D736861322D3531322C7273612D736861322D3235362C
  7273612D736861322D3531322D636572742D763031406F70656E7373682E636F6D2C7273612D736861322D3235362D63
  6572742D763031406F70656E7373682E636F6D2C7373682D7273612C7373682D7273612D636572742D763031406F7065
  6E7373682E636F6D0000009763686163686132302D706F6C7931333035406F70656E7373682E636F6D2C616573323536
  2D6374722C6165733139322D6374722C6165733132382D6374722C6165733235362D6362632C72696A6E6461656C2D63
  6263406C797361746F722E6C69752E73652C6165733139322D6362632C6165733132382D6362632C617263666F757231
  32382C617263666F75722C336465732D6362630000009763686163686132302D706F6C7931333035406F70656E737368
  2E636F6D2C6165733235362D6374722C6165733139322D6374722C6165733132382D6374722C6165733235362D636263
  2C72696A6E6461656C2D636263406C797361746F722E6C69752E73652C6165733139322D6362632C6165733132382D63
  62632C617263666F75723132382C617263666F75722C336465732D6362630000009D686D61632D736861322D3235362C
  686D61632D736861322D3235362D65746D406F70656E7373682E636F6D2C686D61632D736861322D3531322C686D6163
  2D736861322D3531322D65746D406F70656E7373682E636F6D2C686D61632D736861312C686D61632D736861312D6574
  6D406F70656E7373682E636F6D2C686D61632D736861312D39362C686D61632D6D64352C686D61632D6D64352D393600
  00009D686D61632D736861322D3235362C686D61632D736861322D3235362D65746D406F70656E7373682E636F6D2C68
  6D61632D736861322D3531322C686D61632D736861322D3531322D65746D406F70656E7373682E636F6D2C686D61632D
  736861312C686D61632D736861312D65746D406F70656E7373682E636F6D2C686D61632D736861312D39362C686D6163
  2D6D64352C686D61632D6D64352D39360000001A7A6C69622C7A6C6962406F70656E7373682E636F6D2C6E6F6E650000
  001A7A6C69622C7A6C6962406F70656E7373682E636F6D2C6E6F6E650000000000000000000000000035986F0AEF890A
  ```

- **The decoded name-lists**, the same both ways (client to server and server to client):

  | List | Upstream curl 8.21.0, Windows reference build (WinCNG), in curl's order |
  | --- | --- |
  | `kex_algorithms` | `diffie-hellman-group-exchange-sha256`, `diffie-hellman-group16-sha512`, `diffie-hellman-group18-sha512`, `diffie-hellman-group14-sha256`, `diffie-hellman-group14-sha1`, `diffie-hellman-group1-sha1`, `diffie-hellman-group-exchange-sha1`, `ext-info-c`, `kex-strict-c-v00@openssh.com` |
  | `server_host_key_algorithms` | `rsa-sha2-512`, `rsa-sha2-256`, `rsa-sha2-512-cert-v01@openssh.com`, `rsa-sha2-256-cert-v01@openssh.com`, `ssh-rsa`, `ssh-rsa-cert-v01@openssh.com` |
  | `encryption_algorithms` | `chacha20-poly1305@openssh.com`, `aes256-ctr`, `aes192-ctr`, `aes128-ctr`, `aes256-cbc`, `rijndael-cbc@lysator.liu.se`, `aes192-cbc`, `aes128-cbc`, `arcfour128`, `arcfour`, `3des-cbc` |
  | `mac_algorithms` | `hmac-sha2-256`, `hmac-sha2-256-etm@openssh.com`, `hmac-sha2-512`, `hmac-sha2-512-etm@openssh.com`, `hmac-sha1`, `hmac-sha1-etm@openssh.com`, `hmac-sha1-96`, `hmac-md5`, `hmac-md5-96` |
  | `compression_algorithms` | `none`; with `--compressed-ssh`: `zlib`, `zlib@openssh.com`, `none` |
  | `languages` | empty |

  The WinCNG backend offers no elliptic-curve key exchange, no `ssh-ed25519` or `ecdsa-*` host
  key and no AES-GCM. The Linux and macOS reference builds use libssh2's OpenSSL backend and are
  expected to offer more (`curve25519-sha256`, `ecdh-sha2-nistp*`, `ssh-ed25519`, `ecdsa-sha2-*`,
  `aes*-gcm@openssh.com`); they run only on CI, so **BL-172 records their lists** from the same
  command lines, and any name in them that no row of decision 2 covers gets a task filed then.

### What curl does with the host key and the login (documented)

- **The host-key check.** Measured above, the pinned build refuses to start an `scp://` or
  `sftp://` transfer at all, exit 2, when it finds no `known_hosts` file and neither `-k` nor
  `--hostpubsha256`/`--hostpubmd5` is given; `--knownhosts <file>` names another file. curl's
  manual (https://curl.se/docs/manpage.html, read 2026-09-29, which documents curl 8.23.0):
  `--hostpubsha256` "Pass a string containing a Base64-encoded SHA256 hash of the remote host's
  public key. curl refuses the connection with the host unless the hashes match"; `--hostpubmd5`
  "Pass a string containing 32 hexadecimal digits ... the 128 bit MD5 checksum of the remote
  host's public key". curl's `lib/vssh/libssh2.c` at tag `curl-8_21_0` compares the SHA-256
  value up to its first `=` on both sides (so padding is optional), the MD5 value
  case-insensitively, and fails either with `CURLE_PEER_FAILED_VERIFICATION` (60), "Denied
  establishing ssh session: mismatch SHA256 fingerprint". What the pinned build does with a key
  that is missing from, or differs from, its `known_hosts` file happens after the key exchange,
  which no canned server reaches: **BL-172 measures it against surl**.
- **User authentication** (`lib/vssh/libssh2.c` at `curl-8_21_0`, and libcurl's
  `CURLOPT_SSH_AUTH_TYPES`, default any): curl asks for the method list with a `none` request
  (`libssh2_userauth_list`), then tries, each only when the list names it: `publickey` (with
  `--key`/`--pubkey`, else `$HOME/.ssh/id_rsa`, then `id_dsa`, then the same names in the current
  directory), `password`, `hostbased`, the SSH agent (`publickey` again), and
  `keyboard-interactive` (answering every prompt with the password). When none succeeds it fails
  with `CURLE_LOGIN_DENIED` (67), "Authentication failure". A `none` request answered
  `SSH_MSG_USERAUTH_SUCCESS` ends authentication there (libssh2's `userauth_list` then reports
  the session authenticated).

## Decision

### 1. The identification string

surl sends `SSH-2.0-surl` CR LF as soon as a connection is accepted, before reading anything, and
sends no other line before it: no version number (ADR-0006 section 3) and no comment (RFC 4253
section 4.2). The client's line must start `SSH-2.0-` (or `SSH-1.99-`, which RFC 4253 section 5.1
lets a server treat as 2.0), end at CR LF or a bare LF (older clients send LF; the line hashed into
H is the one without its ending), be at most 255 bytes with its ending and hold no NUL; the client
may send nothing before it. Anything else is answered with decision 9's `DISCONNECT` 8.

### 2. The algorithms, in server order, and who builds each

The server's `KEXINIT` offers each list below in this order. RFC 4253 section 7.1 makes curl's
own order decide (the first client algorithm the server also offers), so against the Windows
reference build the negotiation is `diffie-hellman-group-exchange-sha256`, `rsa-sha2-512`,
`chacha20-poly1305@openssh.com` (no MAC), and `none` or, with `--compressed-ssh`, `zlib`. The
server order says what surl prefers and is what a client that follows the server would get.

**Offered by default:**

| List | Server order | Built by |
| --- | --- | --- |
| kex | `curve25519-sha256`, `curve25519-sha256@libssh.org` | BL-167 (ADR-0048's `Surl.Cryptography.Curve25519`) |
| | `ecdh-sha2-nistp256`, `ecdh-sha2-nistp384`, `ecdh-sha2-nistp521` | BL-160 (`ECDiffieHellman`) |
| | `diffie-hellman-group-exchange-sha256`, `diffie-hellman-group16-sha512`, `diffie-hellman-group18-sha512`, `diffie-hellman-group14-sha256` | BL-160 (`BigInteger.ModPow`, RFC 3526 primes) |
| | `kex-strict-s-v00@openssh.com` (a pseudo-algorithm, last; decision 2.1) | BL-159, BL-160 |
| host key | `ssh-ed25519` | BL-168 (`Surl.Cryptography.Ed25519`) |
| | `ecdsa-sha2-nistp256`, `ecdsa-sha2-nistp384`, `ecdsa-sha2-nistp521` | BL-160 (`ECDsa`) |
| | `rsa-sha2-512`, `rsa-sha2-256` | BL-160 (`RSA`, RFC 8332) |
| | each `<name>-cert-v01@openssh.com` for a key `--hostcert` certifies, before its plain name | BL-222 |
| cipher | `chacha20-poly1305@openssh.com` | BL-169 (`Surl.Cryptography.ChaCha20`, `.Poly1305`) |
| | `aes256-gcm@openssh.com`, `aes128-gcm@openssh.com`, only where `AesGcm.IsSupported` | BL-161 |
| | `aes256-ctr`, `aes192-ctr`, `aes128-ctr` | BL-161 (`Aes.EncryptEcb`) |
| MAC | `hmac-sha2-256-etm@openssh.com`, `hmac-sha2-512-etm@openssh.com`, `hmac-sha2-256`, `hmac-sha2-512` | BL-161 |
| compression | `none`, `zlib@openssh.com`, `zlib` | BL-170 (`ZLibStream`) |

- **Host-key algorithms are offered only for the keys surl holds** (decision 4): an RSA key gives
  `rsa-sha2-512,rsa-sha2-256`, an ECDSA key its curve's name, an Ed25519 key `ssh-ed25519`. A
  kex method that needs a signature is offered only when a host key is held, which is always
  (decision 4 refuses a start without one).
- **`diffie-hellman-group-exchange-sha256`** (RFC 4419): the server answers the client's
  `min`/`n`/`max` with the smallest RFC 3526 group (2048, 3072, 4096, 6144 or 8192 bits) of at
  least `n` bits within `[min, max]`, else the largest within it; none within it (or `min` above
  8192, `max` below 2048) is `DISCONNECT` 3.
- **`first_kex_packet_follows`** set by a client whose guess is wrong: the guessed packet is
  discarded (RFC 4253 section 7). BL-159.
- **Compression** is offered always, so `--compressed-ssh` needs nothing on the server; `zlib`
  starts at `NEWKEYS`, `zlib@openssh.com` at `USERAUTH_SUCCESS`, both bounded as decision 9 says.

**Offered only with `--allow-weak-ssh-algorithms`** (decision 5), appended after the default
entries of each list, and all built by **BL-221** (filed by this task):

| List | Entries | Weakness |
| --- | --- | --- |
| kex | `diffie-hellman-group14-sha1`, `diffie-hellman-group-exchange-sha1`, `diffie-hellman-group1-sha1` | SHA-1; group 1 is 1024-bit |
| host key | `ssh-rsa` (and `ssh-rsa-cert-v01@openssh.com` with `--hostcert`), `ssh-dss` for a DSA host key | SHA-1 signatures; DSA 1024 |
| cipher | `aes256-cbc`, `rijndael-cbc@lysator.liu.se`, `aes192-cbc`, `aes128-cbc`, `3des-cbc`, `arcfour128`, `arcfour` | CBC, 64-bit-block 3DES, RC4 (`arcfour` from BL-219's `Surl.Cryptography.Rc4`) |
| MAC | `hmac-sha1-etm@openssh.com`, `hmac-sha1`, `hmac-sha1-96`, `hmac-md5`, `hmac-md5-96` | SHA-1, MD5, truncation |

With it, RSA host and user keys shorter than 2048 bits and DSA user keys are also accepted.
Every entry of the measured lists is thereby assigned: none is left out.

#### 2.1 Strict key exchange and `ext-info`

- **Strict kex** (OpenSSH `PROTOCOL`, "strict key exchange", the Terrapin fix, CVE-2023-48795):
  the server always lists `kex-strict-s-v00@openssh.com`, and when the client's first `KEXINIT`
  lists `kex-strict-c-v00@openssh.com` (curl's does) the connection is strict: the client's
  `KEXINIT` must be its first packet, any other message before the first `NEWKEYS` is
  `DISCONNECT` 2 (BL-159), and each direction's sequence number is reset to 0 after each
  `NEWKEYS` (BL-160). A sequence number that would wrap during the initial key exchange is
  `DISCONNECT` 2. Without the client's marker, RFC 4253's rules apply unchanged.
- **`ext-info-c`** (RFC 8308): when the client's first `KEXINIT` lists it, the server sends
  `SSH_MSG_EXT_INFO` right after its first `NEWKEYS`, with one extension, `server-sig-algs`,
  listing the user-key signature algorithms it verifies, in decision 2's host-key order:
  `ssh-ed25519` (once BL-168 lands), `ecdsa-sha2-nistp256`, `ecdsa-sha2-nistp384`,
  `ecdsa-sha2-nistp521`, `rsa-sha2-512`, `rsa-sha2-256`, then `ssh-rsa` and `ssh-dss` with
  `--allow-weak-ssh-algorithms`. libssh2 1.11 signs with an RSA key using `rsa-sha2-*` only when
  `server-sig-algs` names it, so this is what keeps curl's RSA logins off SHA-1. BL-162 sends it.
  The server lists no `ext-info-s`: it takes no extension from the client.
- **Re-keying** (RFC 4253 section 9, RFC 4344 section 3.1): a client's `KEXINIT` is answered at
  any time after the first exchange; the server starts one itself after 1 GiB in either direction
  or one hour since the last exchange. BL-161.

### 3. Two new hand-built libraries

RC4 (for `arcfour`, `arcfour128`) and OpenSSH's `bcrypt_pbkdf` (for encrypted `openssh-key-v1`
host keys, decision 4) are not in the BCL, so each is built by hand in its own library, as the
root `CLAUDE.md` requires: **`Surl.Cryptography.Rc4`** (BL-219) and
**`Surl.Cryptography.BcryptPbkdf`** (BL-220, with its Blowfish inside it). They join ADR-0002
decision 3's table referencing nothing, and `Surl.Protocol.Ssh` references them. How the
algorithms compose into SSH stays in `Surl.Protocol.Ssh`, as ADR-0048 decision 3 says.

### 4. The host keys

**Source.** `--hostkey <file>`, repeatable: each names one private key file. The name follows
curl's "host public key" (`--hostpubsha256`, `--hostpubmd5`) and OpenSSH's `HostKey`; curl's
`--key` already means the server's TLS key in surl (ADR-0010), so SSH gets its own.

- **Formats**, recognised by content: OpenSSH `openssh-key-v1` (`-----BEGIN OPENSSH PRIVATE
  KEY-----`, what `ssh-keygen` writes; OpenSSH `PROTOCOL.key`), PKCS#8 (`-----BEGIN PRIVATE
  KEY-----`), PKCS#1 RSA (`-----BEGIN RSA PRIVATE KEY-----`) and SEC1 EC (`-----BEGIN EC PRIVATE
  KEY-----`), each with one key. Key types: RSA (at least 2048 bits), ECDSA on P-256, P-384 or
  P-521, Ed25519, and DSA only with `--allow-weak-ssh-algorithms`.
- **Encrypted keys** take their passphrase from **`--pass`**, which already means "passphrase for
  the private key" (curl's name): it now applies to `--key` and to every `--hostkey`. An encrypted
  PKCS#8 (`-----BEGIN ENCRYPTED PRIVATE KEY-----`) is read with the BCL; an encrypted
  `openssh-key-v1` (`bcrypt` KDF) is refused as not available until BL-223 reads it with BL-220's
  `bcrypt_pbkdf`.
- **At most one key per key type** (`ssh-rsa`, each ECDSA curve, `ssh-ed25519`, `ssh-dss`); all
  are offered together, and the client's host-key list picks one.
- **`--hostcert <file>`**, repeatable: an OpenSSH host certificate (`*-cert.pub`,
  `PROTOCOL.certkeys`, type host) for one of the `--hostkey` keys, offered as decision 2 says.
  Its validity dates are not checked by the server: the client judges them. Built by BL-222.
- **Read when**: after the command line is read and before any listener binds (ADR-0031
  decision 7's order), whenever the options are given, whether or not an `scp` or `sftp` URL is,
  so a bad file is found at once. Reading the files is `Surl.Console`'s (BL-171); the parsers
  (BL-160, BL-168, BL-222, BL-223) take bytes and return typed refusals.

**No host key.** A start with an `scp://` or `sftp://` listen URL and neither `--hostkey` nor
`--throwaway-hostkey` is refused before any listener binds, as a secure listen URL without
`--cert` is (ADR-0032 section 10). **`--throwaway-hostkey`** makes one RSA 3072-bit key in memory
at start, only when an `scp` or `sftp` URL is served and at most once; RSA because every pinned
build's host-key list has `rsa-sha2-*` (the WinCNG build has nothing else). It is a loosening
option (decision 11 warns), since a client must pin it or skip the check.

**Refusals**, all before any listener binds, in the `surl: ` form of ADR-0007 section 5, with no
`try` line (the command line itself was read), `<path>` as given, and no text holding key
material. 37 and 2 follow ADR-0032 section 2's split: a file that cannot be read, and a
configuration surl cannot act on.

| Case | Exit | Text after `surl: ` |
| --- | --- | --- |
| An `scp`/`sftp` listen URL with no host key | `FailedInit` (2) | `(2) <listen url> needs a host key: give --hostkey <file>, or --throwaway-hostkey for a throwaway one` (first such URL, written as ADR-0007 section 7's status line writes it) |
| `--throwaway-hostkey` with `--hostkey` (after the whole command line is read) | 2 | `option --throwaway-hostkey: cannot be used with --hostkey`, then the `try` line |
| Missing, a directory, or unreadable | `CouldNotReadFile` (37) | `(37) Could not read host key <path>` |
| Not one private key in a format above | 2 | `(2) Host key <path>: not a private key surl can read` |
| Encrypted, no `--pass` | 2 | `(2) Host key <path>: the key is encrypted; give --pass` |
| Encrypted, `--pass` does not decrypt it | 2 | `(2) Host key <path>: --pass does not decrypt the key` |
| Encrypted `openssh-key-v1`, until BL-223 | 2 | `(2) Host key <path>: encrypted OpenSSH keys are not available in this build` |
| RSA shorter than 2048 bits, or DSA, without `--allow-weak-ssh-algorithms` | 2 | `(2) Host key <path>: <key type> keys of <bits> bits need --allow-weak-ssh-algorithms` |
| Another key type | 2 | `(2) Host key <path>: key type <type> is not supported` |
| A second key of one type | 2 | `(2) Host key <path>: a <key type> host key is already given by <other path>` |
| `--hostcert`: missing, a directory, unreadable | 37 | `(37) Could not read host certificate <path>` |
| `--hostcert`: not an OpenSSH host certificate | 2 | `(2) Host certificate <path>: not an OpenSSH host certificate` |
| `--hostcert`: certifies no `--hostkey` key | 2 | `(2) Host certificate <path>: certifies no --hostkey key` |

No new `SurlExitCode` member is needed.

### 5. The options

| Long | Argument | Meaning | Default | Negatable | Repeats | `--help` description (at most 34 characters) | Categories until BL-171 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `--hostkey` | `<file>` | An SSH host private key (decision 4) | none | no | **adds** a key each time | `SSH host private key file` | `auth` |
| `--hostcert` | `<file>` | An OpenSSH host certificate | none | no | adds | `SSH host certificate file` | `auth` |
| `--throwaway-hostkey` | none | Make a throwaway RSA host key when no `--hostkey` is given | off | yes | later wins | `Throwaway SSH host key (warns)` | `testing` |
| `--authorized-keys` | `<user:file>` | The public keys a user may log in with (decision 6) | none | no | adds | `SSH public keys a user may use` | `auth` |
| `--allow-weak-ssh-algorithms` | none | Also offer decision 2's weak algorithms | off | yes | later wins | `Offer weak SSH algorithms (warns)` | `security` |
| `--pass` (existing) | `<phrase>` | Now also decrypts `--hostkey` files | none | no | last wins | `Passphrase for --key and --hostkey` | unchanged |

- `<file>` arguments follow ADR-0007 section 2 (an empty argument refused while parsing).
- BL-171 adds the `ssh` category (decision 12) to every option above and to each existing option
  the SSH server reads (ADR-0034 decision 1).
- Until BL-171 composes the SSH server, `Surl.Console` refuses a start that gives any of the new
  options with `FailedInit`, `surl: (2) --<option> is not available in this build`, no `try`
  line (ADR-0032 section 1's precedent); `--hostcert`'s refusal stays until BL-222. BL-158 parses
  them, with their help, manual and `--aihelp` facts.

### 6. User authentication

**Methods**, named in every `SSH_MSG_USERAUTH_FAILURE` in this order, `partial success` false:
**`publickey,password,keyboard-interactive`**. The list is the same whatever is configured, so it
tells a peer nothing about the accounts (ADR-0006 section 3). `hostbased` (RFC 4252 section 9) is
not offered: curl's command line has no option that names a client host key for it, so no curl
exchange needs it, and a server-side list of trusted client hosts would be trust surl cannot
check. No `SSH_MSG_USERAUTH_BANNER` is sent.

- **`none`**: refused (the list above), not delayed, no note - except under `--allow-anonymous`,
  where it is `USERAUTH_SUCCESS` at once. Since curl starts with `none`, an anonymous surl logs
  every curl client in without a credential ever being sent.
- **`password`** (RFC 4252 section 8): checked through decision 7's contract. SSH encrypts before
  it authenticates, so the password is **not** a plain-text secret and `--allow-plaintext-auth`
  plays no part (ADR-0032, "Protocol servers not yet built", criterion 3).
  `SSH_MSG_USERAUTH_PASSWD_CHANGEREQ` is never sent; a change request is refused.
- **`keyboard-interactive`** (RFC 4256): one `SSH_MSG_USERAUTH_INFO_REQUEST` with empty name and
  instruction and one prompt `Password: `, echo off; the one response is checked as a password.
  curl uses it only when `password` failed or is not offered; offering it keeps a client that
  only speaks it able to log in.
- **`publickey`** (RFC 4252 section 7): the query form (no signature) is answered
  `SSH_MSG_USERAUTH_PK_OK` when the key is authorized for the user, else `USERAUTH_FAILURE`; the
  signed form is verified by the SSH server itself over the session identifier and the request,
  with the algorithm the request names (`ssh-ed25519`, `ecdsa-sha2-*`, `rsa-sha2-*`, and `ssh-rsa`,
  `ssh-dss` only with `--allow-weak-ssh-algorithms`), and then decided by the contract.
- **The user name and service** are fixed by the first request: a later request naming another
  user or service is `DISCONNECT` 2 (as OpenSSH does). The service must be `ssh-connection`, else
  `DISCONNECT` 7. The user name is UTF-8 (RFC 4252 section 5); a name that is not is refused like a
  wrong password, with no user in the note (ADR-0038 section 6).
- **Attempts**: six refused requests of any method but `none` (a refused public-key query counts)
  end the connection with `DISCONNECT` 14 after the sixth `USERAUTH_FAILURE`, as OpenSSH's
  `MaxAuthTries` 6 does. Each refused credential is delayed 1 second inside `Surl.Authentication`
  (ADR-0032 section 8), and the whole login runs under the head timeout (decision 9).
- **Accounts.** A password login matches an account from `--user`/`--user-file` with a name
  (never an empty-name Bearer account). A public-key login matches `--authorized-keys`. With no
  account and no authorized key configured, every login is refused (ADR-0032 criterion 2).
  "No such user", "wrong password", "key not authorized" and "nothing configured" answer the same.
- **`--allow-anonymous`** means for SSH what it means everywhere: any login, or none, is accepted
  without checking - `none` succeeds, a password or keyboard-interactive answer is accepted, a
  public-key query is answered `PK_OK` and a signed request accepted whatever its signature -
  all `AcceptedUnchecked`, with no note (ADR-0038 section 5).

**`--authorized-keys <user:file>`**, repeatable: split at the first `:` as `--user` is (so a
Windows path after it keeps its drive colon); the user is an account name for SSH public-key
logins, which need not also have a password. Refused while parsing, with ADR-0032 section 1's
rules (the `try` line follows): no `:` - `option <name>: expected <user:file>`; an empty user -
`option <name>: the user name is empty`; a control character in the user - `option <name>: the user
name holds a control character`; an empty file part - `option <name>: blank argument where content
is expected`; a user given twice - `option <name>: user <user> is given twice`.

**The file is OpenSSH's `authorized_keys` format** (sshd(8), "AUTHORIZED_KEYS FILE FORMAT"), so a
user's own `~/.ssh/authorized_keys` can be named as it is: UTF-8, lines split as ADR-0032
section 2 splits them, blank lines and `#` comments skipped, every other line
`<key type> <base64 key blob> [comment]`, separated by spaces or tabs. Key types: `ssh-ed25519`,
`ecdsa-sha2-nistp256`, `ecdsa-sha2-nistp384`, `ecdsa-sha2-nistp521`, `ssh-rsa` (an RSA key,
signed with `rsa-sha2-*`) and `ssh-dss`; the blob's own type string must equal the line's. A key
is compared as its exact blob (RFC 4253 section 6.6) with `FixedTimeEquals`. The per-key options
OpenSSH allows before the type (`from=`, `command=`, `restrict`, ...) are refused rather than
ignored, since ignoring one would grant more than the file says. Parsed by `Surl.Authentication`
(BL-157) from bytes; read by `Surl.Console` (BL-171) at start, before any listener binds:

| Case | Exit | Text after `surl: ` |
| --- | --- | --- |
| Missing, a directory, or unreadable | 37 | `(37) Could not read authorized keys <path>` |
| A line without a key type and a key | 2 | `(2) Authorized keys <path>, line <n>: expected <key type> <key>` |
| Options before the key type | 2 | `(2) Authorized keys <path>, line <n>: key options are not supported` |
| An unknown key type (`sk-*` and certificates included) | 2 | `(2) Authorized keys <path>, line <n>: key type <type> is not supported` |
| The key is not base64, or its blob is malformed or of another type | 2 | `(2) Authorized keys <path>, line <n>: the key is malformed` |
| Not UTF-8 | 2 | `(2) Authorized keys <path>, line <n>: not UTF-8` |

A file with no keys is not an error. `<type>` is escaped as ADR-0006 section 3 escapes a peer's
bytes.

### 7. The contract

All in `Surl.Protocol.Abstractions.UnitLibrary`, namespace `Surl.Protocol.Abstractions`, one
public type per file, shared-framework types only; BL-156 adds them. It is **a new interface
beside `IAuthenticationPolicy`**, as ADR-0049's `IMailAuthenticationPolicy` is, so no existing
implementer changes: `IAuthenticationPolicy`, `Surl.Authentication`'s `AuthenticationPolicy` and
the test doubles in `Surl.Protocol.Http.UnitTests` and `Surl.Protocol.Mqtt.UnitTests` are
untouched. `AnonymousAuthenticationPolicy` additionally implements it (BL-156), and
`AuthenticationPolicy` does once BL-157 lands. `Surl.Console` passes the same object to the SSH
server as this interface.

```csharp
public interface ISshAuthenticationPolicy
{
    // "none": AcceptedUnchecked under --allow-anonymous, otherwise Refused. Never delayed, never noted.
    SshLoginVerdict CheckSshNoneLogin(SshNoneLogin login);

    // "password" and "keyboard-interactive": the password is not plain-text (SSH encrypts first).
    ValueTask<SshLoginVerdict> CheckSshPasswordLoginAsync(SshPasswordLogin login, CancellationToken cancellationToken);

    // "publickey": is this key authorized for this user; the server has already judged the signature.
    ValueTask<SshLoginVerdict> CheckSshPublicKeyLoginAsync(SshPublicKeyLogin login, CancellationToken cancellationToken);
}

public sealed record SshNoneLogin(
    string? UserName);                      // null when the name sent is not UTF-8

public sealed record SshPasswordLogin(
    string Method,                          // "password" or "keyboard-interactive", as on the wire
    string? UserName,                       // null when the name sent is not UTF-8
    ReadOnlyMemory<byte> Password);         // as sent (UTF-8, RFC 4252 section 8)

public sealed record SshPublicKeyLogin(
    string? UserName,                       // null when the name sent is not UTF-8
    string SignatureAlgorithm,              // the request's algorithm name, e.g. "rsa-sha2-256"
    ReadOnlyMemory<byte> PublicKeyBlob,     // RFC 4253 section 6.6, as sent
    SshPublicKeyProof Proof);

public enum SshPublicKeyProof
{
    None,                   // the query form: is the key acceptable? Never delayed, never noted
    ValidSignature,         // signed, and the server verified the signature
    InvalidSignature,       // signed, and the signature did not verify: refused, delayed, noted
}

public enum SshLoginOutcome
{
    Accepted,               // checked, and matches an account (or an authorized key)
    AcceptedUnchecked,      // --allow-anonymous (ADR-0038)
    KeyAcceptable,          // a public-key query whose key is authorized: send USERAUTH_PK_OK
    Refused,                // send USERAUTH_FAILURE; decided after the 1-second delay when a credential was checked
}

public sealed record SshLoginVerdict(
    SshLoginOutcome Outcome,
    string? AccountName,                    // the account logged in when Accepted; otherwise null
    CheckedLogin? CheckedLogin);            // the login note to write; null when nothing was checked
```

- **The server owns** the RFC 4252 framing, the method list, the attempt count, the fixed user
  and service, and the signature check (with the key and algorithm the request names, over RFC
  4252 section 7's signed data); the policy owns which accounts and keys exist, every comparison,
  the delay and the note. A `KeyAcceptable` answer to a signed request is treated as `Refused`
  (fail closed), and so is any outcome the server does not know.
- **Delay and note** (ADR-0032 section 8, ADR-0038): `Refused` for a password, a keyboard
  interactive answer or a signed public key is decided after the 1-second delay and carries a
  note; `Accepted` carries a note; a public-key query, `none` and `AcceptedUnchecked` carry none
  and are not delayed. The server writes `CheckedLogin.Note` to its exchange log before it
  answers, whenever it is not `null`.
- **The `CheckedLogin` words** (ADR-0038 section 4): the method is the RFC 4252 method name as on
  the wire - `password`, `keyboard-interactive` or `publickey` - and the user is the name sent,
  left out when it is not UTF-8: `Login accepted: publickey alice`, `Login refused: password bob`.
- **`AnonymousAuthenticationPolicy`** (the shared test double) answers `none`, every password and
  every signed key `AcceptedUnchecked` and every query `KeyAcceptable`, with no note, so curl
  completes its login in one `none` request.

### 8. The host-key fingerprints the operator sees

`Listening on ...` is untouched (ADR-0007 section 7). With `-v`, once at start, before the first
`Listening on` line, surl notes each host key it serves:

```
Serving SSH host key <key type>, --hostpubsha256 <base64> --hostpubmd5 <hex>
```

- `<key type>` is the blob's type string (`ssh-rsa`, `ecdsa-sha2-nistp256`, `ssh-ed25519`, ...);
  `<base64>` is the SHA-256 of the public-key blob, base64 with its padding, and `<hex>` the MD5
  of the blob, 32 lower-case hex digits, so each value can be pasted after the curl option it
  follows (curl compares either SHA-256 form and the MD5 case-insensitively, see Context). The
  same values are what OpenSSH prints as `SHA256:<base64 without padding>` and `MD5:<hex with
  colons>`.
- With `--throwaway-hostkey` the startup warning (decision 11) carries the SHA-256 value too, so
  the operator sees it at the default log level.
- **How tests pass the check**: BL-172 passes `--hostpubsha256` with the value from this note
  (read from surl's verbose output) and points `HOME` and `USERPROFILE` at a temporary directory
  so neither the operator's `known_hosts` nor their `id_rsa` is used; `-k` is the fallback only
  for measuring what curl does without a pin.

### 9. Limits and `SSH_MSG_DISCONNECT`

ADR-0006 governs. An SSH packet is a framed message:

- **`packet_length` + 4 above `--max-message`** (1 MiB by default, above RFC 4253 section 6.1's
  35000-byte floor) is refused before the body is read; so is a length that is not a multiple of
  the block size or leaves fewer than 4 padding bytes.
- **The head timeout** (`--head-timeout`, 30 s) covers everything from accept to
  `USERAUTH_SUCCESS`: identification, the first key exchange and user authentication. OpenSSH's
  `LoginGraceTime` does the same.
- **A decompressed payload** is counted against `--max-message` and stopped the moment it passes.
- **Channels**: at most 10 open per connection (OpenSSH's `MaxSessions`); each granted a window
  of 2097152 bytes and a maximum packet of 32768 bytes, the window re-granted once half is used;
  never more buffered than granted. A further `CHANNEL_OPEN` gets `OPEN_FAILURE` 4
  (`SSH_OPEN_RESOURCE_SHORTAGE`). Other channel types get 1 (`ADMINISTRATIVELY_PROHIBITED`) for
  `direct-tcpip`, `forwarded-tcpip` and `x11`, 3 (`UNKNOWN_CHANNEL_TYPE`) for the rest; `pty-req`,
  `env`, `shell`, `x11-req` and forwarding requests are answered `CHANNEL_FAILURE` or
  `REQUEST_FAILURE` when a reply is wanted. BL-163.

Every `DISCONNECT` has an empty language tag and a fixed description, written with ADR-0006
section 5's one-second deadline and followed by a graceful close:

| Cause | Reason code (RFC 4253 section 11.1) | Description |
| --- | --- | --- |
| The client's identification line is bad or too long, or its version is not 2.0 | 8 `PROTOCOL_VERSION_NOT_SUPPORTED` | `Protocol version not supported` |
| A packet past `--max-message`, a malformed packet or message, a message out of order (strict kex included), a changed user or service, a peer past its channel window | 2 `PROTOCOL_ERROR` | `Protocol error` |
| No common algorithm in a list; a group-exchange range no group fits | 3 `KEY_EXCHANGE_FAILED` | `No common algorithm` / `No group fits the requested range` |
| A MAC or AEAD tag that does not verify | 5 `MAC_ERROR` | `MAC error` |
| A payload that does not decompress, or decompresses past `--max-message` | 6 `COMPRESSION_ERROR` | `Compression error` |
| A `SERVICE_REQUEST` other than `ssh-userauth` (before login) or a login for a service other than `ssh-connection` | 7 `SERVICE_NOT_AVAILABLE` | `Service not available` |
| The sixth refused login | 14 `NO_MORE_AUTH_METHODS_AVAILABLE` | `Too many authentication failures` |
| The `NEWKEYS` placeholders BL-159 and BL-160 use until the next task lands | 11 `BY_APPLICATION` | `Key exchange not implemented` / `Packet protection not implemented` |

- The description names no algorithm, user or path: a peer learns nothing it did not send
  (ADR-0006 section 3). `DISCONNECT` 3 does not say which list failed; the verbose note does.
- **Not answered with a `DISCONNECT`** (ADR-0006 section 5's framed-protocol column): the head
  timeout, the idle timeout and `--max-time` close with no bytes, and a connection past a
  connection limit is closed with no bytes before the identification line, noted by ADR-0028's
  `NoteOutsideExchange`.
- A `DISCONNECT` from the client ends the connection without a reply.

### 10. The verbose notes

`Surl.Protocol.Ssh` writes these notes at `-v` (ADR-0033), each peer-supplied part escaped by
ADR-0006 section 3; the engine's `BytesReceived`/`BytesSent` and `--trace` dumps stay the bytes
on the wire, which after `NEWKEYS` are ciphertext. No decrypted dump is written: a
`USERAUTH_REQUEST` holds a password.

| When | Note |
| --- | --- |
| The client's identification line is read | `SSH client identification: <line without its ending>` |
| The first key exchange is agreed | `SSH negotiated kex <kex>, host key <alg>, cipher <c2s>/<s2c>, MAC <c2s>/<s2c>, compression <c2s>/<s2c>, strict kex <on\|off>` (`implicit` for the MAC of an AEAD cipher) |
| A key exchange fails to agree | `SSH no common <kex\|host key\|cipher\|MAC\|compression> algorithm; client offered <list>` |
| A re-exchange starts | `SSH key re-exchange started by the <client\|server>` |
| A login request other than `none` | `SSH login request: <method> for <user>` (and for `publickey`: `, key <key type> SHA-256 <base64>`, the key blob's hash as decision 8 writes it) |
| A login is decided | the contract's `CheckedLogin.Note` (decision 7) |
| A `DISCONNECT` is sent or received | `SSH disconnect sent: <code> <description>` / `SSH disconnect received: <code> <description>` |

Channel notes (`exec` commands, subsystems, exit status) are BL-155's and BL-163's. Never in any
note: a password, a keyboard-interactive answer, a private key, a shared secret, the exchange hash,
the session identifier or a session key (ADR-0032 section 8).

### 11. Warnings

ADR-0032 section 9's startup warnings grow by two lines, after the `--self-signed` line, in this
order:

```
surl: warning: --throwaway-hostkey: serving a throwaway SSH host key (--hostpubsha256 <base64>); clients must pin it or skip the check (curl -k)
surl: warning: --allow-weak-ssh-algorithms: SHA-1, MD5, CBC, RC4, 3DES and 1024-bit Diffie-Hellman SSH algorithms are offered
```

The first is written only when the throwaway key is made (an `scp` or `sftp` URL is served), the
second whenever the option is given.

### 12. The help category

One category for both schemes, **`ssh`**, described **`SSH protocol`**: curl 8.21.0's own
category for `scp` and `sftp`, measured (`curl --help ssh` prints `ssh: SSH protocol`). ADR-0034
decision 1 names a category after a scheme family; `scp` and `sftp` are two schemes of one
protocol with no secure `s` form, and curl's name is the one a curl user already knows, so this
is the one departure from that rule's wording, not from its intent. The `--aihelp` topic is
`ssh` too (ADR-0046: one topic per category). BL-171 adds the row, the topic and the option
memberships.

### 13. Who builds what

| Work | Task |
| --- | --- |
| Identification, binary packets, `KEXINIT` and negotiation, strict kex's order rule, `first_kex_packet_follows` | BL-159 |
| `ecdh-sha2-*`, the finite-field and group-exchange kex methods, `ecdsa-sha2-*` and `rsa-sha2-*` host keys, the unencrypted key formats and PKCS#8 encryption, `NEWKEYS`, strict kex's sequence reset | BL-160 |
| AES-CTR, AES-GCM, HMAC-SHA2 and their `-etm` forms, re-keying | BL-161 |
| `ssh-userauth`, the three methods, `EXT_INFO` `server-sig-algs`, attempts | BL-162 |
| Channels, `exec`, `subsystem`, the channel limits | BL-163 |
| `curve25519-sha256`, `curve25519-sha256@libssh.org` | BL-167 |
| `ssh-ed25519` host and user keys | BL-168 |
| `chacha20-poly1305@openssh.com` | BL-169 |
| `zlib@openssh.com`, `zlib` | BL-170 |
| The decision 7 contract, `AnonymousAuthenticationPolicy` | BL-156 |
| `AuthenticationPolicy`'s SSH checks, the `authorized_keys` parser | BL-157 |
| The decision 5 options, help, manual and AI help, the "not available" refusals | BL-158 |
| Composition, the files read at start, the refusals of decisions 4 and 6, the fingerprint note and warnings, the `ssh` category | BL-171 |
| Proof against the pinned builds, the Linux and macOS `KEXINIT` lists, what curl does with an unknown or changed host key and with a refused login | BL-172 |
| RC4 in `Surl.Cryptography.Rc4` | BL-219 (filed by this task) |
| `bcrypt_pbkdf` in `Surl.Cryptography.BcryptPbkdf` | BL-220 (filed by this task) |
| The weak algorithms behind `--allow-weak-ssh-algorithms` | BL-221 (filed by this task) |
| Host certificates, `--hostcert` | BL-222 (filed by this task) |
| Encrypted `openssh-key-v1` host keys | BL-223 (filed by this task) |

## Alternatives considered

- **Offering the weak algorithms by default**, since curl lists them: every pinned build prefers
  a strong entry that surl offers, so the default would only serve other clients with SHA-1, CBC
  or RC4; secure by default (Stewart, 2026-09-29) puts them behind a named, warned option.
- **Leaving the weak algorithms and host certificates out**: the root `CLAUDE.md` rules it out;
  each has its task.
- **Reusing `--key` for the SSH host key**, as curl's client uses `--key` for both TLS and SSH:
  surl's `--key` is the TLS key already, and one process serving `https` and `sftp` would need
  two keys under one name.
- **Reusing `--self-signed` for a throwaway host key**: it names a certificate; an SSH host key
  is not one, and one option switching on two throwaway secrets would say less than it does.
- **Generating a host key and keeping it under `.surl/`**, as `ssh-keygen -A` does for sshd: a
  persisted secret the operator never asked for; `--hostkey` with a file they made says what is
  served.
- **A throwaway ECDSA or Ed25519 key**: the Windows reference build's list has only RSA host-key
  algorithms, measured.
- **An `authorized_keys` format with a user column**: the user's own file could not be named as
  it is; `user:file` keeps OpenSSH's format untouched.
- **Ignoring `authorized_keys` options**: ignoring `from=` or `command=` would let a key do more
  than its line allows; refusing says so at start.
- **Not offering `keyboard-interactive`**: a client that speaks only it could not log in; with
  the attempt limit it adds no guesses.
- **Adding the SSH members to `IAuthenticationPolicy`**: every existing implementer and test
  double would change (BL-156 may touch only Abstractions).
- **Sending `DISCONNECT` 12 `TOO_MANY_CONNECTIONS` past a connection limit**: it needs the
  identification exchange first, which is work done for a peer being refused; ADR-0006 closes
  with no bytes.

## Consequences

- BL-156 to BL-172 read their decisions here; BL-155 decides SCP and SFTP on top of this.
- Five tasks are filed: BL-219 to BL-223. `Surl.Cryptography.Rc4` and
  `Surl.Cryptography.BcryptPbkdf` join ADR-0002's table.
- `--pass`'s help description becomes `Passphrase for --key and --hostkey` (BL-158).
- `Requirements.md`'s FR-039 and FR-040 read this ADR when BL-171 and BL-172 land.
- Fixtures: this task's `touches` do not include `Surl.Protocol.Ssh.UnitTests`, so the
  recordings live above as hex; BL-159 turns runs D and F into fixtures under
  `Surl.Protocol.Ssh.UnitTests/Fixtures/`, re-recorded with the same command lines.
