# ADR-0032 — Secure-by-default authentication: accounts, the loosening options, `--self-signed` and the contract protocol servers call

- **Status:** Accepted
- **Superseded in part:** section 1's "Descriptions for `--help`" are superseded by [ADR-0034](ADR-0034-curl-style-help-categories-and-the-manual.md) decision 2, which shortens them to fit curl's 79 columns.
- **Amended:** section 6's contract by [ADR-0038](ADR-0038-checked-logins-carry-the-login-note-and-the-server-writes-it.md) (`CheckedLogin`, the verdict's fourth parameter, `PasswordLoginVerdict.AcceptedUnchecked`).
- **Amended:** section 3's table of `--auth` words, default set and order by
  [ADR-0049](ADR-0049-the-mail-servers-sasl-and-apop-logins.md) decision 3 (the SASL mechanism
  words and `apop`); the mail logins' contract is ADR-0049 decision 6, beside section 6's.
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-100. Stewart approved the feature on 2026-09-29; the details he left to this ADR.
- **Supersedes:** ADR-0010 section 3's "No `--cert` for a secure scheme" default (a throwaway
  certificate without being asked for one) and ADR-0014 decision 3's "A will, user name and
  password the flags allow are accepted and not read". Everything else in both stands.

## Context

Stewart's approval of 2026-09-29, summarised:

1. **Secure by default.** With no accounts configured, every protocol that has a login
   refuses every login. A plain-text password over an unencrypted connection (HTTP Basic
   over `http://`, FTP `USER`/`PASS` without TLS, ...) is refused.
2. **Anonymous reads** over HTTP, Gopher and TFTP stay allowed by default, like a public web
   server; writes still need `--allow-uploads` and, where the protocol has a login, a login.
3. **Insecure by design stays as it is**: TFTP, Gopher, DICT and TELNET get no security
   bolted on.
4. **Accounts**: `--user name:password` (curl's name) and `--user-file <file>`, because a
   command-line password is visible to local users.
5. **Loosening options**, each logged as a warning on every start: `--allow-anonymous`,
   `--allow-plaintext-auth`, `--auth <methods>` and `--self-signed`. No umbrella
   `--insecure`.
6. Only what can land now is planned (the options, `Surl.Authentication`, HTTP challenges,
   MQTT CONNECT credentials, `--self-signed`); servers not yet built get acceptance criteria
   here, not tasks.

The code this changes, as it is today:

- `Surl.Authentication.UnitLibrary` holds only `CLAUDE.md` and its csproj, which references
  `Surl.Protocol.Abstractions.UnitLibrary` only.
- ADR-0002's table, enforced by `Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`,
  lets a protocol server reference only Abstractions, `Surl.Content` and `Surl.Cryptography`.
  So `Surl.Protocol.Http` and `Surl.Protocol.Mqtt` cannot reference `Surl.Authentication`.
- `Surl.Protocol.Http.UnitLibrary/HttpRequestResponder.cs` answers `GET` and `HEAD` with no
  authentication and `PUT` with `405` (ADR-0008); `Surl.Protocol.Http.UnitLibrary/HttpProtocolServer.cs`
  is constructed from a `ContentStore`.
- `Surl.Protocol.Mqtt.UnitLibrary/MqttConnectJudge.cs` validates the connect flags and the
  client identifier and does not read the will, user name or password;
  `Surl.Protocol.Mqtt.UnitLibrary/MqttProtocolServer.cs` claims `mqtt` and `mqtts`.
- `Surl.Console/ServerTlsComposition.cs` makes the throwaway certificate
  (`Surl.Networking.UnitLibrary/ThrowawayServerCertificate.cs`) whenever an implicit-TLS listen
  URL (`Surl.Protocol.Abstractions.UnitLibrary/TlsSchemes.cs`) is served without `--cert`.
- `Surl.Protocol.Abstractions.UnitLibrary/IConnection.cs` exposes `TlsSession`, `null` while the
  connection is plaintext (ADR-0010 section 1).
- `Surl.Cli.UnitLibrary/CommandLineOptions.cs` is the option table;
  `Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs` holds ADR-0005's table with ADR-0010's
  and ADR-0031's rows.

Inputs: ADR-0002, ADR-0005, ADR-0006 (section 3: nothing a peer can learn), ADR-0007 (sections
1, 2 and 5: the option conventions and error texts), ADR-0008, ADR-0010, ADR-0014, ADR-0019,
ADR-0027.

### What upstream curl 8.21.0 does with a Digest challenge

- Build: `C:\Program Files\Git\mingw64\bin\curl.exe`, curl 8.21.0 (x86_64-w64-mingw32),
  Schannel, SSPI, SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`
  (`UpstreamCurlBuilds.json`).
- Tool: `Record-CurlExchange.ps1 -Connections 2`, 2026-09-29. Connection 1 answers the canned
  `401` below, connection 2 a `200` with body `ok`. Every `Digest` challenge is
  `Digest realm="r", nonce="abc", algorithm=<A>, qop="auth"`.

| `WWW-Authenticate` lines in the `401`, in order | curl arguments | Exit | What curl sent on connection 2 |
| --- | --- | --- | --- |
| one `Digest`, `algorithm=MD5` | `-sS --digest -u a:b` | 0 | `Authorization: Digest username="a",realm="r",nonce="abc",uri="/x",cnonce="...",nc=00000001,algorithm=MD5,response="...",qop="auth"` |
| one `Digest`, `algorithm=SHA-256` | `-sS --digest -u a:b` | 94 | nothing: `curl: (94) An authentication function returned an error` |
| one `Digest`, `algorithm=SHA-512-256` | `-sS --digest -u a:b` | 94 | the same |
| `Digest` `SHA-256`, then `Digest` `MD5` | `-sS --digest -u a:b` | 94 | the same: curl answers only the first `Digest` line |
| `Digest` `MD5`, then `Digest` `SHA-256` | `-sS --digest -u a:b` | 0 | `Authorization: Digest ... algorithm=MD5 ...` |
| `Digest` `MD5`, then `Basic realm="r"` | `-sS --anyauth -u a:b` | 0 | `Authorization: Digest ... algorithm=MD5 ...` |
| `Negotiate`, `NTLM`, `Digest` `MD5`, `Basic realm="r"` | `-sS --anyauth -u a:b` | 0 | a request with no `Authorization` at all: curl chose Negotiate, and SSPI produced no token for `127.0.0.1` |

So the Windows reference build (SSPI Digest) answers only MD5 and only the first `Digest`
line; `--anyauth` picks by curl's own ranking, not by line order. The Linux and macOS
reference builds (static OpenSSL, no SSPI; ADR-0016) use curl's own Digest code; the
`version` lines in `UpstreamCurlBuilds.json` list `NTLM` but no `SPNEGO` or `Kerberos` for
them. What they send for SHA-256 and SHA-512-256 is measured on CI by BL-113 and BL-118.

## Decision

### 1. The options

Every option below follows ADR-0007 section 1 and its error texts (section 5): each refusal is
`FailedInit` (2), `surl: ` then the text, then the line `try 'surl --help' for more information`.
Each option's one-line description is given here; where it sits in `--help` is ADR-0034's
(BL-102).

| Short | Long | Argument | Meaning | Default | Negatable | Repeats |
| --- | --- | --- | --- | --- | --- | --- |
| `-u` | `--user` | `<user:password>` | Add one account. | no accounts | no | **adds** an account each time |
| | `--user-file` | `<file>` | Read accounts from a file (section 2). | none | no | last value wins |
| | `--allow-anonymous` | none | Accept every request and login without checking credentials. | off | yes | later wins |
| | `--allow-plaintext-auth` | none | Accept passwords and tokens over unencrypted connections. | off | yes | later wins |
| | `--auth` | `<methods>` | Authentication methods accepted (section 3). | `basic,bearer,digest,aws-sigv4` | no | last value wins |
| | `--self-signed` | none | Serve a throwaway self-signed certificate when no `--cert` is given. | off | yes | later wins |

Descriptions for `--help`: `--user` "Add an account (repeatable)"; `--user-file` "Read
accounts from a file"; `--allow-anonymous` "Accept any login, or none (warns)";
`--allow-plaintext-auth` "Accept passwords over unencrypted connections (warns)"; `--auth`
"Authentication methods accepted (default basic,bearer,digest,aws-sigv4)"; `--self-signed`
"Serve a throwaway certificate without --cert (warns)".

**`--user`** keeps curl's name and short form, and its value is curl's `user:password`:

- The value is split at its **first** `:`, as curl splits it: `--user a:b:c` is the name `a`
  and the password `b:c`. So a name never holds a `:` (RFC 7617 section 2 forbids one in a
  Basic user-id anyway).
- **Repeats add accounts.** This is a stated difference from ADR-0007's "last value wins":
  curl's `-u` is the one credential a client sends, a server has many accounts, and dropping
  all but the last would silently lock out the others.
- **An empty name is a Bearer token**, not an account a name-and-password method can use:
  `--user :tok` configures the token `tok` (section 3). An account with a name is never
  matched by a Bearer token, and an empty-name account is never matched by any other method.
  AWS Signature Version 4 needs nothing new: its access key ID is the name and its secret
  access key the password, exactly as curl's own `--aws-sigv4 ... --user KEYID:SECRET` takes
  them.
- **Refusals.** In each, `<name>` is the option as written without any value (`-u` or
  `--user`), unlike ADR-0007 section 2's rule, so no refusal can echo a password.

  | Case | Text after `surl: ` |
  | --- | --- |
  | No `:` in the value (curl would prompt for the password; a server cannot) | `option <name>: expected <user:password>` |
  | Empty password (`--user a:`, `--user :`) | `option <name>: the password is empty` |
  | A control character (U+0000 to U+001F, U+007F) in the name | `option <name>: the user name holds a control character` |
  | A name given twice (the empty name included) | `option <name>: user <user> is given twice` |
  | Empty argument | `option <name>: blank argument where content is expected` (ADR-0007 row 25) |
  | Missing argument | `option <name>: requires parameter` |

  The duplicate check runs after the whole command line is read, like `--tls-max`'s.

**`--user-file`** is a `<file>` (ADR-0007 section 2): an empty argument is refused while
parsing; the file is read when surl starts serving, before any listener binds (section 2).
Its accounts are added after every `--user` account.

**`--auth`** takes a comma-separated list of the words in section 3, with no spaces, matched
case-insensitively (as ADR-0010 matches `--cert-type`) and stored lower-case. An unknown
word or an empty item (`basic,,digest`, a trailing comma):
`option <name>: is badly used here` (ADR-0007 row 36), `<name>` as ADR-0007 writes it. A word
given twice counts once. `Surl.Console` refuses, before any listener binds, a word whose method
this build does not implement yet: `FailedInit`, `surl: (2) --auth <word> is not available in
this build`, with no `try` line. That refusal disappears word by word as BL-111 to BL-122 land;
it is not a decision to leave a method out.

**`--self-signed` with `--cert`** is refused after the whole command line is read:
`option --self-signed: cannot be used with --cert`. Ignoring one of the two would leave a
command line that says something surl does not do.

### 2. The `--user-file` format

- **Encoding**: UTF-8, with an optional byte-order mark that is skipped. Any byte sequence that
  is not UTF-8 is malformed.
- **Lines**: split at LF; one CR immediately before the LF (or at the end of the file) is
  dropped, so the file may be written on any platform.
- **A blank line** (empty, or only spaces and tabs) is skipped. **A comment** is a line whose
  first character is `#`, skipped.
- **Every other line is one account**, `user:password`, read exactly as `--user` reads its
  value: split at the first `:`, the empty name a Bearer token, nothing trimmed (a space is part
  of the name or password).
- **Passwords are in clear.** A salted slow hash (PBKDF2, the one the BCL has) would let
  Basic, Bearer, MQTT and the future plain-password logins be checked, but not Digest (which
  needs the password or `MD5(user:realm:password)`), NTLM (which needs `MD4` of the password)
  or Signature Version 4 (which derives HMAC keys from the secret). One format that every
  method can use beats two that each serve half of them; the file is protected the way
  `--key` is, by the file system's permissions. A hashed form for the methods that allow it
  is later work if wanted, in a new ADR.
- **Refusals**, all before any listener binds, `<path>` as given on the command line:

  | Case | Exit | Text after `surl: ` |
  | --- | --- | --- |
  | Missing, a directory, or unreadable | `CouldNotReadFile` (37) | `(37) Could not read user file <path>` |
  | A line with no `:` | `FailedInit` (2) | `(2) User file <path>, line <n>: expected <user:password>` |
  | An empty password | 2 | `(2) User file <path>, line <n>: the password is empty` |
  | A control character in the name | 2 | `(2) User file <path>, line <n>: the user name holds a control character` |
  | A name already given, in the file or by `--user` | 2 | `(2) User file <path>, line <n>: user <user> is given twice` |
  | Not UTF-8 | 2 | `(2) User file <path>, line <n>: not UTF-8` |

  37 reuses ADR-0005's row for a file surl was told to use and cannot open; a malformed
  file is a configuration surl cannot act on, which is `FailedInit`'s meaning. There is no
  `try` line: the command line itself was read. `<n>` counts from 1. No text ever holds a
  password. A file with no accounts is not an error: it configures none.

### 3. The methods

| `--auth` word | Method (curl option) | In the default set | Plain-text secret |
| --- | --- | --- | --- |
| `basic` | Basic, RFC 7617 (`--basic`, and `-u` alone) | yes | **yes** |
| `bearer` | Bearer, RFC 6750 (`--oauth2-bearer`) | yes | **yes** |
| `digest` | Digest, RFC 7616 (`--digest`) | yes | no |
| `ntlm` | NTLM (`--ntlm`) | no | no |
| `negotiate` | Negotiate, RFC 4559 (`--negotiate`), carrying NTLM (section 11) | no | no |
| `aws-sigv4` | AWS Signature Version 4 (`--aws-sigv4`) | yes | no |

- The separator is `,`, as in curl's own list-valued options (`--proto`).
- **The default set leaves out NTLM and Negotiate.** NTLM's response is built on MD4 and
  HMAC-MD5 of the password and is open to relay and offline cracking; Negotiate here carries
  NTLM. Both stay a named choice (`--auth ntlm`), which is what a loosening option is for.
  The measured `--anyauth` row shows a second reason: offering Negotiate to the Windows
  reference build makes it answer with no credentials at all.
- **A plain-text secret** is one a passive listener reads straight off the wire: Basic,
  Bearer, the MQTT `CONNECT` password, and every later clear password (FTP `PASS`, IMAP
  `LOGIN`, POP3 `USER`/`PASS`, SMTP `AUTH PLAIN` and `LOGIN`, LDAP simple bind). Digest, NTLM,
  Negotiate and Signature Version 4 send a response computed from the secret, not the
  secret, so they count as not plain-text.
- The **order methods are listed** everywhere (the `--auth` warning, `WWW-Authenticate`
  lines) is: `negotiate`, `ntlm`, `digest`, `basic`, `bearer`, `aws-sigv4`.

### 4. HTTP policy (FR-014)

**Which requests need a login.**

- With **no account** configured, a `GET` or `HEAD` needs none (anonymous reads, like a
  public web server).
- Once **any account** is configured (by `--user` or `--user-file`), every request needs one.
  Configuring accounts says the server is private, and without this there would be no request
  on which curl's `--digest`, `--ntlm` or `--negotiate` (which wait for a `401`) could ever
  log in.
- A **write** (any method other than `GET` and `HEAD`, once the server serves one) always
  needs a login, and still needs `--allow-uploads` (ADR-0006 section 2).
- **`--allow-anonymous`** makes every request proceed as anonymous: missing credentials are
  not challenged and any credentials sent are not checked.

**Where it is judged.** After a head has been read and found well-formed (so `400`, `408`,
`431` and `505` come first, ADR-0008 and ADR-0019), and before anything else: before the
`413` check of a `Content-Length`, before `100 Continue` is sent (ADR-0027), before any body
byte is read and before the method is dispatched (so an unauthenticated `PUT` gets `401`, not
`405`). A client that may not write learns nothing about what it would have written to.

**How a request is answered**, in this order:

1. `--allow-anonymous`: served.
2. It carries a **plain-text secret** (`Authorization: Basic ...` or `Bearer ...`) on a
   connection whose `TlsSession` is `null`, without `--allow-plaintext-auth`: **`403
   Forbidden`**, empty body, `Connection: close`, the connection half-closed (ADR-0008's
   refusal rule). The secret is not checked, so an unencrypted connection is never an oracle
   for passwords, and the client learns that sending it in clear was the fault. The same
   happens with no account configured.
3. It carries an `Authorization` whose method is in the accepted set: the method checks it.
   Accepted: served. A continuation step (NTLM's type 2 message, a Negotiate token): `401`
   carrying that step's one `WWW-Authenticate` line. Refused, including every login with no
   account configured: `401` with the challenges below. The answer is the same for an unknown
   user, a wrong password and a server with no accounts (section 8).
4. It carries an `Authorization` of any other method: treated as missing (step 5).
5. It needs a login and has none: `401` with the challenges below; if no method in the
   accepted set can be offered on this connection (say `--auth basic` over `http://`),
   `403 Forbidden` as in step 2, since a `401` must carry a challenge (RFC 9110 section 11.6.1).
6. Otherwise: served.

**The `401`**: `HTTP/1.1 401 Unauthorized`, then `Date`, `Server: surl` (ADR-0019) and
`Content-Length: 0`, and one `WWW-Authenticate` field per challenge, empty body. It keeps the
connection as ADR-0008's persistence rule does for a `404` (NTLM and Negotiate need the same
connection), and closes like any refusal when the request announced a body that was not read.

**The challenges**, one field each, in section 3's order, only for methods in the accepted set
that can be offered on this connection:

| Method | `WWW-Authenticate` value | Offered |
| --- | --- | --- |
| Negotiate | `Negotiate` | always |
| NTLM | `NTLM` | always |
| Digest | `Digest realm="surl", qop="auth", algorithm=MD5, nonce="<nonce>"`, then the same with `algorithm=SHA-256`, then with `algorithm=SHA-512-256` (three fields) | always |
| Basic | `Basic realm="surl", charset="UTF-8"` | only over TLS, or with `--allow-plaintext-auth` |
| Bearer | `Bearer realm="surl"` | only over TLS, or with `--allow-plaintext-auth` |
| AWS Signature Version 4 | none: curl signs the first request unasked, and the scheme defines no challenge | never |

- **Digest offers MD5 first** because the Windows reference build answers only the first
  `Digest` line and only MD5 (measured above); SHA-256 and SHA-512-256 follow for every client
  that reads further. The nonce, its lifetime and `stale=true` are BL-113's; `qop="auth-int"`
  is not offered, because checking it needs the body before the login is judged.
- The realm is `surl` for every method: fixed, so it says nothing about the host or the data
  directory (ADR-0006 section 3).
- Nothing in any response says whether an account exists or is configured.

### 5. MQTT policy (FR-020)

MQTT has a login, and Stewart's list of anonymous protocols is HTTP, Gopher and TFTP, so an
MQTT `CONNECT` needs one by default. The server reads the will (and skips it), the user name
and the password the connect flags announce, and asks the contract (section 6), then:

| `CONNECT` | Answer |
| --- | --- |
| Any, with `--allow-anonymous` | `CONNACK` 0, whatever the credentials |
| A password on a connection whose `TlsSession` is `null` (`mqtt://`), without `--allow-plaintext-auth` | `CONNACK` 5, "not authorized"; the password is not checked |
| No user name | `CONNACK` 5, "not authorized" |
| A user name, with or without a password, that does not match an account (every one, with no account configured) | `CONNACK` 4, "bad user name or password" |
| A user name and password matching an account | `CONNACK` 0 |

After a `CONNACK` other than 0 the server closes the connection (MQTT 3.1.1 section 3.2.2.3).
4 and 5 are the two codes section 3.2.2.3 gives for a login; 5 is used where no password was
checked, 4 where one was, so neither says whether a name exists. MQTT is not a method in
`--auth`: its only login is a clear password, governed by `--allow-plaintext-auth`. The
connect-flag checks of ADR-0014 decision 3 run first and are unchanged. curl's exit code for
each `CONNACK` is measured by BL-115.

### 6. The contract (BL-109)

All in `Surl.Protocol.Abstractions.UnitLibrary`, namespace `Surl.Protocol.Abstractions`; every
type used is in the shared framework, so Abstractions still references nothing, and ADR-0002's
table is unchanged: no protocol server references `Surl.Authentication`. `Surl.Authentication`
implements the interfaces; `Surl.Console` passes its implementation to each server.

```csharp
public interface IAuthenticationPolicy
{
    // A login by user name and clear password: MQTT now; FTP, IMAP, POP3, SMTP, LDAP later.
    ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(
        PasswordLogin login, CancellationToken cancellationToken);

    // One per HTTP connection; holds that connection's NTLM and Negotiate handshakes.
    IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession);
}

public sealed record PasswordLogin(
    string Scheme,              // the listen URL's scheme, for the log
    string? UserName,           // null when the protocol sent none
    ReadOnlyMemory<byte>? Password,  // the bytes as sent; null when none was sent
    TlsSession? TlsSession);    // IConnection.TlsSession: null means unencrypted

public enum PasswordLoginVerdict
{
    Accepted,               // the credentials were checked and match an account
    RefusedCredentials,
    RefusedAnonymous,
    RefusedPlaintext,
    AcceptedUnchecked,      // --allow-anonymous: accepted, nothing checked (ADR-0038)
}

public interface IHttpAuthenticationSession
{
    ValueTask<HttpAuthenticationVerdict> JudgeAsync(
        HttpAuthenticationRequest request, CancellationToken cancellationToken);
}

public sealed record HttpAuthenticationRequest(
    string Method,
    string Target,              // the request target as received
    bool IsWrite,               // true for every method but GET and HEAD
    IReadOnlyList<KeyValuePair<string, string>> Fields);  // every header field, in order

public enum HttpAuthenticationOutcome { Proceed, Challenge, Forbidden }

public sealed record HttpAuthenticationVerdict(
    HttpAuthenticationOutcome Outcome,
    IReadOnlyList<string> WwwAuthenticateValues,  // written as one field each, in order
    string? AccountName,        // the account logged in, or null when anonymous or refused
    CheckedLogin? CheckedLogin = null);  // the login note to write; null when nothing was checked

// The credentials checked and the answer; the server writes Note to its exchange log (ADR-0038).
public sealed record CheckedLogin(string Method, string? User, bool IsAccepted)
{
    public const string BearerTokenUser = "bearer token";

    public string Note =>
        $"Login {(IsAccepted ? "accepted" : "refused")}: {Method}{(User is null ? string.Empty : " " + User)}";
}
```

- **The login note** (section 8) travels on the verdict, not through the policy:
  `CheckedLogin`, the fourth `HttpAuthenticationVerdict` parameter and
  `PasswordLoginVerdict.AcceptedUnchecked` were added by BL-125, and
  [ADR-0038](ADR-0038-checked-logins-carry-the-login-note-and-the-server-writes-it.md) records
  why, which logins are noted and how the method and user are named.
- **Encryption** is told by passing `IConnection.TlsSession` as it stands: `null` is
  unencrypted. A server that upgrades (FTP `AUTH TLS`, `STARTTLS`) passes the session it has
  at the moment of the login. HTTP starts its session after the engine's implicit handshake,
  so an `https` session always has one.
- **Connection-bound handshakes.** The HTTP server calls `StartHttpConnection` once per
  connection, before its first request, and `JudgeAsync` for every request on it; NTLM's and
  Negotiate's state lives in that session object and dies with it, so a new connection starts
  over (RFC 4559 section 6, and NTLM over HTTP, bind to the connection). The session is not
  disposable; it holds no unmanaged resource.
- **`Proceed`** may still carry `WwwAuthenticateValues` (Negotiate's final token, RFC 4559
  section 5); the server writes them on the response it then sends. **`Challenge`** is the
  `401` of section 4 with exactly those values; **`Forbidden`** the `403`. The server decides
  nothing about methods: a method added to `Surl.Authentication` later (Signature Version 4,
  Kerberos inside Negotiate) needs no change to the HTTP server or `Surl.Console`.
- **Body-bound checks** (Digest `auth-int`, a Signature Version 4 payload hash) do not get the
  body: `auth-int` is not offered, and BL-122 checks Signature Version 4's
  `x-amz-content-sha256` field as it decides.
- **Until BL-117 composes the real policy**, BL-114 and BL-115 keep each server's existing
  constructor as an overload that passes `AnonymousAuthenticationPolicy`, a sealed class BL-109
  adds to Abstractions: every HTTP request `Proceed`s with no values and every password login is
  `AcceptedUnchecked` (ADR-0038) - exactly today's behaviour and `--allow-anonymous`'s. BL-117 composes the servers
  with `Surl.Authentication`'s policy only and removes the overloads, so nothing in `surl` is
  left composed without a policy; the class stays as the test double protocol tests share.

### 7. `Surl.Authentication` may reference `Surl.Cryptography`

It needs MD4 for NTLM (BL-119) and SHA-512/256 for Digest (BL-112), which the BCL lacks, and
`Surl.Cryptography` is where hand-built primitives live (root `CLAUDE.md`, "Decisions").
`Surl.Cryptography` references nothing, so no cycle forms. The product overview's "Layers" row
for Services then reads that `Surl.Authentication` also depends on `Surl.Cryptography`;
BL-124 edits it.

### 8. Password checks

- Every comparison of a secret, or of a value computed from one (a Digest response, an NTLM
  response, a Signature Version 4 signature), is `CryptographicOperations.FixedTimeEquals`. A
  clear password is compared as the SHA-256 of its UTF-8 bytes with the SHA-256 of the account's,
  so the comparison's length does not depend on the password's.
- An unknown user name costs the same work as a wrong password: the check runs against a fixed
  dummy account's password, and the answer is the same verdict.
- Nothing a peer receives differs between "no such user", "wrong password" and "no accounts
  configured" (ADR-0006 section 3).
- **A refused credential is answered after a fixed 1-second delay**, waited on the injected
  `TimeProvider` inside `Surl.Authentication`, for every method and every protocol; missing
  credentials (a plain challenge) and a handshake's continuation step are not delayed. With
  `--max-connections-per-address` (100 by default) that holds one address to about 100 guesses a
  second without any per-account lockout, which would let anyone lock a user out. The delay is
  cancelled with the exchange's token.
- The verbose log notes `Login accepted: <method> <user>` and `Login refused: <method> <user>`
  (`<user>` as sent, escaped by ADR-0007 section 8, `bearer token` for Bearer), and never a
  password, a token, or an `Authorization` value in a note; the bytes-received lines still show
  the raw request, as they do today. Which logins are noted, and how the method and user are
  named, is [ADR-0038](ADR-0038-checked-logins-carry-the-login-note-and-the-server-writes-it.md).

### 9. Warnings

Each loosening option given writes one line to stderr on every start, after the command line is
read and before any `Listening on` line, in this order:

```
surl: warning: --allow-anonymous: every request and login is accepted without checking credentials
surl: warning: --allow-plaintext-auth: passwords and tokens are accepted over unencrypted connections
surl: warning: --auth: accepted methods are <methods>
surl: warning: --self-signed: serving a throwaway certificate; clients must skip verification (curl -k)
```

- `<methods>` is the accepted set in section 3's order, joined with `, `.
- `--auth` warns whenever it is given, even when it narrows the default: the line tells the
  operator what the server accepts, which is what a reader of the log needs.
- `--self-signed`'s line is written only when the throwaway certificate is made (section 10),
  since otherwise the option changes nothing.
- The level these lines are written at is the one ADR-0033 (BL-101) gives startup warnings; until
  ADR-0033 is Accepted and composed (BL-107), they are always written.

### 10. `--self-signed`

- **Without `--cert` and without `--self-signed`, a listen URL whose scheme
  `TlsSchemes.IsImplicitTls` names is refused** before any listener binds:
  `CertificateProblem` (58), `surl: (58) <listen url> needs a certificate: give --cert <file>,
  or --self-signed for a throwaway one`, `<listen url>` written as ADR-0007 section 7's status
  line writes it, for the first such URL in command-line order. 58 is ADR-0010's row for a
  server certificate surl cannot serve.
- **With `--self-signed`** and no `--cert`, the throwaway certificate is ADR-0010 section 3's,
  unchanged (key, subject, names, lifetime, made at most once, only when needed), and ADR-0010's
  `-v` note with its fingerprint stays. A start with `--self-signed` and no secure listen URL
  makes no certificate.
- **`--self-signed` with `--cert`** is refused (section 1).
- A protocol that upgrades mid-connection (FTP, SMTP, IMAP, POP3) with neither option is not
  refused at start: it refuses the upgrade command in its own words, and its clear-password
  login then stays refused without `--allow-plaintext-auth`.

### 11. Kerberos

Negotiate carries NTLM now (BL-121). Kerberos inside Negotiate is later work, built by hand as
the root `CLAUDE.md` requires, not a refusal; until it lands, a Kerberos token inside Negotiate
is refused like any other bad credential.

### Protocol servers not yet built

Each of these servers' own task carries these as acceptance criteria:

- **FTP** (`USER`/`PASS`), **IMAP** (`LOGIN`, `AUTHENTICATE`), **POP3** (`USER`/`PASS`,
  `APOP`, `AUTH`), **SMTP** (`AUTH`), **SSH** for SCP and SFTP (password and public-key user
  authentication), **SMB** (NTLM session setup) and **LDAP** (simple and SASL bind):
  1. every login goes through the section 6 contract: `CheckPasswordLoginAsync` for a clear
     password, and a new contract member, added to Abstractions by that server's task, for any
     other kind (SASL, SSH public key, SMB's NTLM), implemented in `Surl.Authentication`;
  2. with no account configured every login is refused, in the protocol's own words;
  3. a clear password before TLS (FTP before `AUTH TLS`, IMAP, POP3 and SMTP before `STARTTLS`
     or `STLS`, LDAP simple bind on `ldap://`) is refused without `--allow-plaintext-auth`,
     without being checked. SSH encrypts before it authenticates, so its password is not
     plain-text.
- **TFTP, Gopher, DICT and TELNET** stay as they are: no login, nothing added.

## Alternatives considered

- **An umbrella `--insecure`.** Rejected by Stewart: each loosening should be named and warned
  about on its own.
- **Anonymous reads even when accounts are configured.** Rejected: then `curl --digest`,
  `--ntlm` and `--negotiate`, which wait for a `401`, could never log in to a read, and an
  operator who configured accounts would still be serving everyone. `--allow-anonymous` gives
  that back when wanted.
- **A `401` for Basic over `http://`.** Rejected: re-challenging would invite the client to
  send the password in clear again; `403` ends it.
- **Hashed passwords in `--user-file`.** Rejected for now (section 2): Digest, NTLM and
  Signature Version 4 cannot be checked from a PBKDF2 hash.
- **Bearer tokens in their own option or file syntax.** Rejected: an empty user name says the
  same without a seventh option, and matches curl's own `-u :` for "no user name".
- **SHA-256 as the first Digest challenge.** Rejected: the Windows reference build then fails
  with exit 94 (measured).
- **Lock an account after failed logins.** Rejected: it lets anyone lock out any user; the
  fixed delay and the per-address connection limit bound guessing instead.
- **`Surl.Protocol.Http` referencing `Surl.Authentication`.** Rejected by ADR-0002; the contract
  in Abstractions is how a service reaches a protocol server.

## Consequences

- `surl http://127.0.0.1:0/` with no options still serves anonymous `GET` and `HEAD`, and
  answers `401` to any request carrying credentials.
- `surl https://127.0.0.1:0/` with no options no longer starts: it needs `--cert` or
  `--self-signed`. Conformance tests that serve a secure scheme pass `--self-signed` (BL-116).
- `surl mqtt://127.0.0.1:0/` refuses every `CONNECT` by default; a test that publishes and
  subscribes passes `--allow-anonymous`, or an account with `mqtts://` and `--self-signed`, or
  `--allow-plaintext-auth`.
- BL-108 parses section 1; BL-109 adds section 6's types; BL-110 builds sections 2, 3, 5 and 8's
  shared parts; BL-111, BL-113, BL-120, BL-121 and BL-122 the methods; BL-114 and BL-115 the
  servers; BL-116 section 10; BL-117 the composition and section 9; BL-118 proves it against
  pinned upstream curl; BL-124 the documentation and the "Layers" row.
- `SurlExitCode` gains no member: 2, 37 and 58 are reused.
