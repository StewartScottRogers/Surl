# Surl.Authentication.UnitLibrary

Phase 1.

The server side of the authentication schemes upstream curl sends, secure by default
(ADR-0032): the accounts, the policy that judges every login, and each HTTP method's
challenge (`WWW-Authenticate`) and check. Today it holds Basic, Bearer, Digest, NTLM,
Negotiate carrying NTLM and AWS Signature Version 4 for HTTP, the password check the
MQTT `CONNECT` asks for, and the mail servers' SASL mechanisms (`PLAIN`, `LOGIN`, `XOAUTH2`,
`OAUTHBEARER`, `CRAM-MD5`, `DIGEST-MD5`, `NTLM`, `GSSAPI`, which checks a Kerberos ticket with the
`--keytab` acceptor and is offered first (ADR-0057 decisions 9 and 10), and `EXTERNAL`, which logs in as the
verified TLS client certificate's subject simple name and is offered only on a connection that
has one) and POP3 `APOP` (ADR-0049), SSH password and public-key logins (ADR-0051), and
Kerberos inside Negotiate once `--keytab` is given (ADR-0057 decision 8, ADR-0064). Not here
yet: `Proxy-Authenticate`, the logins of servers not yet built (FTP, SMB). Anything time-dependent (the
refusal delay, Digest nonces, the Signature Version 4 window) takes an injected
`TimeProvider`.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and
`Surl.Cryptography.UnitLibrary` for MD4 and SHA-512/256 (ADR-0032 decision 7), and
`Surl.Kerberos.UnitLibrary` for the Kerberos acceptor (ADR-0057 decision 6), and
`Surl.Cryptography.Rc4.UnitLibrary` for NTLM sealing (ADR-0072 decision 4), and no
protocol server. `AuthenticationSettings.KerberosAcceptor` carries the acceptor `Surl.Console`
builds from `--keytab` (`null` without one, BL-240); SASL `GSSAPI` (`GssapiSaslExchange`) is offered and run only when it is set, and Negotiate carries Kerberos only when `Surl.Console` hands it to `NegotiateAuthenticationMethod`. Protocol servers receive what it provides through the contracts in
Abstractions (`IAuthenticationPolicy`, `IMailAuthenticationPolicy` - which extends the
protocol-neutral `ISaslAuthenticationPolicy` - and `ISshAuthenticationPolicy`); `Surl.Console`'s
`AuthenticationComposition` builds the policy from the command line. `GetSaslMechanisms` is the
mail offer's mechanisms for every scheme, with `GSS-SPNEGO` after `GSSAPI` for `ldap` and `ldaps`
when `--auth` accepts `negotiate` (ADR-0072 decision 4, BL-328); `GSS-SPNEGO` runs only when the
server starts it with `CanCarrySecurityLayer` (BL-329, below), and is `RefusedMechanism` otherwise.

## What is here now (BL-110)

- `Account`, `AccountBook`: the configured accounts. `AccountBook` keeps each password as
  the SHA-256 of its UTF-8 bytes and checks a password or Bearer token by comparing SHA-256
  hashes through `ISecretComparer` (`CryptographicSecretComparer`, i.e.
  `CryptographicOperations.FixedTimeEquals`); an unknown user is compared against a random
  dummy hash, so it costs the same and answers the same (ADR-0032 section 8). The empty
  user name is the Bearer token's account and never matches `CheckPassword`.
- `UserFileParser`: the `--user-file` text (bytes, never the disk) into accounts, after the
  `--user` ones, or the first refused line as a `UserFileLineFailure` whose `Describe()` is
  ADR-0032 section 2's text after the path.
- `AuthenticationMethod`, `AuthenticationMethods`: section 3's methods in its order, the
  default set, which send a plain-text secret, and the `Authorization` scheme of each.
- `AuthenticationPolicy` (with `AuthenticationSettings`) implements `IAuthenticationPolicy`:
  section 5 for password logins, section 4 for HTTP through `HttpAuthenticationSession`, and
  the 1-second refusal delay on the injected `TimeProvider`.
- `IHttpAuthenticationMethod` / `IHttpCredentialVerifier` / `HttpCredentialCheck`: the seam
  each HTTP method (BL-111, BL-113, BL-120, BL-121, BL-122) implements. The policy offers and
  checks only the methods it is given that `--auth` accepts; an `Authorization` of any other
  method is treated as missing.

## Basic and Bearer (BL-111)

- `BasicAuthenticationMethod` offers `Basic realm="surl", charset="UTF-8"` and checks the
  base64 `user-id:password`: split at the first `:`, the user-id read as UTF-8 and the
  password compared as the bytes sent (ADR-0035). Not base64, no `:`, or a user-id that is
  not UTF-8 is a refusal, never an exception.
- `BearerAuthenticationMethod` offers `Bearer realm="surl"` and checks the token, turned back
  into the bytes sent with Latin-1, against the empty-name account; an accepted token's
  `AccountName` is the empty string, and an empty token is refused.
- Both hold no per-connection state: `StartConnection` returns the method itself. The
  `Authorization` values they are tested with are recorded from pinned upstream curl in
  `Surl.Authentication.UnitTests/Fixtures` (see its README).

## Digest (BL-113)

- `DigestAuthenticationMethod` offers ADR-0032 section 4's three challenges (MD5, SHA-256,
  SHA-512-256, one nonce) and checks `qop=auth` answers under them or their `-sess` forms;
  `DigestAnswer` and `DigestParameterParser` read the answer, `DigestAlgorithmName` the
  `algorithm`, and `DigestCalculation` is RFC 7616 section 3.4's arithmetic (SHA-512/256 from
  `Surl.Cryptography`, which this library references for it, ADR-0032 section 7).
- `DigestNonceBook` (behind `IDigestNonceBook`) issues ADR-0036's signed nonces on the injected
  `TimeProvider`, expires them after five minutes, and remembers each used nonce's `nc` so a
  replayed answer is refused. A right answer on an expired nonce is a `Continue` carrying the
  challenges with `stale=true`, so the policy answers it undelayed.
- `AccountBook.FindDigestAccount` holds each named account's user hashes, computed at
  start-up, under its UTF-8 spelling and, for an all-ISO-8859-1 account, its ISO-8859-1 one;
  an unknown name gets a random dummy.

## Login notes (BL-125)

- Every checked HTTP login's verdict carries a `CheckedLogin` (Abstractions): the
  `Authorization` scheme (`AuthenticationMethods.AuthorizationSchemeOf`), the user as sent
  (`HttpCredentialCheck.UserAsSent`: Basic's user-id, Digest's `username`, `bearer token` for
  Bearer, `null` when none could be read) and whether it was accepted. The HTTP server writes
  its `Note` - `Login accepted: Basic alice` - to the verbose log (ADR-0032 section 8). No
  credentials, a plain-text secret refused unchecked, `--allow-anonymous` and a handshake's
  continuation step carry none.
- A password login under `--allow-anonymous` is `PasswordLoginVerdict.AcceptedUnchecked`, so
  a server notes only logins whose credentials were checked.

## NTLM (BL-120)

- `NtlmAuthenticationMethod` offers the bare `NTLM` challenge (only when `--auth` names `ntlm`,
  ADR-0032 section 3) and starts an `NtlmConnectionVerifier` per connection, which holds the
  handshake (ADR-0039): a `NEGOTIATE_MESSAGE` (`NtlmNegotiateMessage`) is a `Continue` carrying
  the `CHALLENGE_MESSAGE` `NtlmChallengeMessage` builds over a new server challenge; the next
  leg uses that challenge up, and an `AUTHENTICATE_MESSAGE` (`NtlmAuthenticateMessage`) is
  accepted only when its NTLMv2 `NTProofStr` matches, compared in fixed time. NTLMv1, a
  malformed message and an answer on an unchallenged connection are refused, never thrown.
- `NtlmV2Calculation` is [MS-NLMP] section 3.3.2's arithmetic (NT hash with `Surl.Cryptography`'s
  MD4, NTOWFv2, `NTProofStr`, session base key), tested against the specification's section
  4.2.4 example. `NtlmMessage` holds what the three messages share, `NtlmNegotiateFlags` the
  flag bits.
- `NtlmV1Calculation` (BL-291) is section 3.3.1's NTLMv1 without extended session security, as
  upstream curl's SMB session setup computes it: `LMOWFv1` (`ComputeLmHash`), the NT hash
  (`ComputeNtHashOfWidenedUtf8`) and `DESL` (`ComputeResponse`), over `Surl.Cryptography`'s
  `Des`, tested against section 4.2.2's example. It follows upstream curl's
  `lib/curl_ntlm_core.c`, not the specification, for a non-ASCII password: the UTF-8 bytes,
  ASCII-only upper-casing and a 14-byte cut for LM, each byte widened to 16 bits for NT. Its NT
  hash is also one of the four `NtlmPasswordHashes` keeps; the rest of it waits for the SMB
  login check (BL-295).
- The server challenge comes from `INtlmServerChallengeSource`: `RandomNtlmServerChallengeSource`
  in production, a fixed one in the tests, which replay the handshakes recorded from pinned
  upstream curl in `Surl.Authentication.UnitTests/Fixtures/ntlm*`.
- `AccountBook.FindNtlmAccount` holds each named account's four NT hashes (`NtlmPasswordHashes`,
  BL-321: one per way a pinned upstream curl build hashes a non-ASCII password, measured in
  `Fixtures/README.md`), computed at start-up; every hash is checked whichever matches; an
  unknown or empty name gets a dummy with four random hashes.
- An accepted NTLM login is remembered by the connection (ADR-0041, BL-133):
  `HttpAuthenticationSession` serves a later request on it without an `Authorization` as that
  account, with no login note, as upstream curl expects (`Fixtures/ntlm-two-urls`). Which
  methods do this is `AuthenticationMethods.AuthenticatesConnection` - NTLM and Negotiate
  (ADR-0044, BL-135). A new handshake replaces the login: none until it is accepted.
- The handshake itself is `NtlmHandshake` (answering decoded messages with an
  `NtlmHandshakeStep`), shared by NTLM and Negotiate; `NtlmConnectionVerifier` only decodes the
  base64 (`Base64Credentials`) and writes `NTLM <base64>`.
- SASL `NTLM` for the mail servers (ADR-0049 section 5, BL-196) is `NtlmSaslExchange`: it holds
  its own `NtlmHandshake` (from `SaslExchangeContext.StartNtlmHandshake`, over the policy's
  `INtlmServerChallengeSource`), so the handshake dies with the `AUTH` command. An empty
  challenge when no initial response was sent, one `CHALLENGE_MESSAGE` per exchange, and every
  other ending - wrong answer, malformed message, a second `NEGOTIATE_MESSAGE` - refused after
  the delay with the user the message named. `ntlm` in `--auth` accepts it and HTTP NTLM alike;
  it is not plain-text, so it is offered without TLS. The tests replay curl's measured type 1
  and type 3 (`NtlmSaslMechanismTests`).

## Negotiate, carrying NTLM (BL-121)

- `NegotiateAuthenticationMethod` offers the bare `Negotiate` challenge (only when `--auth` names
  `negotiate`) and starts a `NegotiateConnectionVerifier` per connection (ADR-0040). A bare NTLM
  token is answered bare, `Negotiate <base64 CHALLENGE_MESSAGE>`, with no final token. A SPNEGO
  `NegTokenInit` offering NTLMSSP is answered with a `negTokenResp` (`accept-incomplete`,
  `supportedMech` in the first reply only, the `CHALLENGE_MESSAGE` as `responseToken`), or with
  `supportedMech` alone when the client preferred another mechanism; the accepted answer is
  served with `Negotiate oQcwBaADCgEA` (`accept-completed`). No `mechListMIC` is checked or sent.
- `SpnegoToken` reads and writes the RFC 4178 tokens with `System.Formats.Asn1` (DER), into
  `SpnegoNegTokenInit`, `SpnegoNegTokenResp` and `SpnegoNegState`; malformed DER reads as `null`, never an exception.
- Without a Kerberos acceptor, a `NegTokenInit` offering no NTLM, a bare Kerberos token, a
  `negTokenResp` out of turn and malformed DER are refused.
- The pinned Windows reference build sent no Negotiate token on the lane machine
  (`SEC_E_NO_CREDENTIALS`, `Fixtures/negotiate-no-token`), so the tests wrap the NTLM messages
  recorded for BL-120 in SPNEGO (`SpnegoTestTokens`). `surl` composes Negotiate, and the
  end-to-end proof uses the unpatched 8.21.0 Windows build (ADR-0042, BL-134).

## Negotiate, carrying Kerberos (BL-241)

- Given `--keytab`'s `KerberosAcceptor` (the constructor
  `NegotiateAuthenticationMethod(accounts, kerberosAcceptor, allowAnonymous)`), the verifier hands
  every `InitialContextToken` that is not SPNEGO, and every `NegTokenInit` whose first supported
  mechanism is Kerberos (either OID), to `NegotiateKerberosLogin`: one leg, service `HTTP`
  (ADR-0057 decisions 2 and 8). Kerberos must be listed first with its AP-REQ as the optimistic
  token; otherwise the token is refused, with no NTLM fallback (ADR-0064). NTLM listed first runs
  as ADR-0040 decides. The same AP-REQ read on another connection is a replay.
- An accepted ticket is the account named exactly as the client principal's display form
  (`user@EXAMPLE.COM`), served with `negTokenResp { accept-completed, supportedMech <the client's
  OID>, responseToken <AP-REP> when mutual-required, mechListMIC <surl's> when the client sent
  one }`; a bare token gets the bare AP-REP token when mutual-required, else no final token. A
  client `mechListMIC` is checked over `SpnegoNegTokenInit.MechTypesDer` (key usage 25).
- `UserAsSent` is the principal once the ticket decrypted (no account, a bad MIC), and `null`
  before. Under `--allow-anonymous` the ticket must still decrypt; it and every other Negotiate
  token are `HttpCredentialOutcome.AcceptedUnchecked`, served with no login note
  (`HttpAuthenticationSession` reads a Negotiate `Authorization` under `--allow-anonymous` only
  when a Kerberos acceptor is set, ADR-0064).
- The tests (`NegotiateKerberosTests`) replay AP-REQs made by hand by `Surl.Kerberos.UnitTests`'
  `ApRequestBuilder` and `InitiatorTokens`, linked into the test project.

## AWS Signature Version 4 (BL-122)

- `AwsSigV4AuthenticationMethod` offers no challenge (curl's `--aws-sigv4` signs the first
  request unasked) and checks the signature against the account the access key ID names, with
  the account's password as the secret, at the injected `TimeProvider`'s time (ADR-0043): the
  signed `x-<provider>-date` must be within `RequestTimeWindow` (15 minutes) either way, and
  `host` and the date must be signed. It holds no per-connection state.
- `AwsSigV4Authorization` reads the `Credential`, `SignedHeaders` and `Signature` parameters;
  `AwsSigV4Provider` recognises any `<PROVIDER>4-HMAC-SHA256` scheme (`AuthenticationMethods`
  maps them all to `AwsSigV4`) and spells the algorithm and key prefix;
  `AwsSigV4CanonicalRequest` rebuilds the canonical request as measured from pinned upstream
  curl (the path encoded again except for `s3`, the query decoded, re-encoded and sorted by name
  then value, signed fields trimmed with inner spaces made one); `AwsSigV4Calculation` is the
  string to sign and the HMAC-SHA256 key derivation.
- `AccountBook.FindAwsSigV4Account` holds each named account's password as UTF-8 bytes; an
  unknown or empty key gets a random dummy, compared the same way.
- The body is bound to the signature (ADR-0045, BL-136). With an `x-<provider>-content-sha256`
  field (always sent for `s3`) the signature is checked on the head, and the body must then
  hash to the field; `UNSIGNED-PAYLOAD` binds no body. Without one, a request with no body is
  signed over the empty body's hash, and a request with a body is an `AwaitingBody` check whose
  `HttpCredentialCheck.CheckBody` finishes the signature over the body's SHA-256. A date
  outside the window is refused before the body is read.
- `HttpAuthenticationSession` turns `AwaitingBody` into a `Proceed` verdict carrying an
  `IHttpRequestBodyCheck` (Abstractions) and no login note; the HTTP server reads and hashes
  the body and asks it, and a body that does not match is refused after the refusal delay
  with the login note, as any refusal is.
- The tests replay the `aws-sigv4-*` requests in `Surl.Authentication.UnitTests/Fixtures`.

## SSH logins (BL-157)

- `AuthenticationPolicy` implements `ISshAuthenticationPolicy` (ADR-0051, sections 6 and 7).
  `none` is refused, undelayed and unnoted, except under `--allow-anonymous`. A `password`
  request or `keyboard-interactive` answer is checked against the named accounts through
  `AccountBook.CheckPassword`, never refused as plain-text (SSH encrypts first), so
  `--allow-plaintext-auth` plays no part. A `publickey` query is `KeyAcceptable` or `Refused`,
  undelayed and unnoted; a signed request is `Accepted` only when the server verified the
  signature and the key is authorized for the user. Every refused credential waits
  `RefusalDelay`, and every checked one carries a `CheckedLogin` whose method is the RFC 4252
  name as on the wire. Under `--allow-anonymous` everything is `AcceptedUnchecked` (a query
  `KeyAcceptable`), with no note.
- `AuthorizedKeysParser` reads an `--authorized-keys` file's bytes (OpenSSH's `authorized_keys`
  format) into `AuthorizedKey`s for one user, or the first refused line as an
  `AuthorizedKeysLineFailure` whose `Describe()` is ADR-0051 section 6's text after the path,
  the key type escaped as ADR-0006 section 3 escapes a peer's bytes. `SshPublicKeyBlob` knows
  the six key types and checks each blob's fields (RFC 4253 section 6.6, RFC 5656, RFC 8709).
  Key options before the type are refused, never ignored.
- `AuthorizedKeyBook` (on `AuthenticationSettings.AuthorizedKeys`, `AuthorizedKeyBook.Empty` by
  default) keeps each key as the SHA-256 of its blob and compares the SHA-256 of the blob sent
  against every key of the user with `ISecretComparer`; an unknown user is compared against a
  random dummy hash, so "no such user", "key not authorized" and "no keys" answer alike.

## LDAP's NTLM and GSS-SPNEGO binds and NTLM sealing (BL-329)

- A SASL exchange started with `SaslExchangeStart.CanCarrySecurityLayer` (LDAP) carries it on
  `SaslExchangeContext`. `NTLM` (which LDAP's Sicily binds map to) and `GSS-SPNEGO`
  (`SaslMechanism.GssSpnego`, accepted by `negotiate`, outside `InOfferOrder`) both run
  `NtlmSaslExchange` over a bare NTLM message, as `WinLDAP` sends it (ADR-0072 decision 4).
- Its `NtlmHandshake` (`grantsSecurityLayer`) answers with the LDAP `CHALLENGE_MESSAGE`:
  `NtlmChallengeMessage.ChooseFlags(clientFlags, true)` also grants sign, seal and key exchange
  when asked. On acceptance it exports the session key (`NtlmSessionKey`: NTLMv2's session base
  key, or the `EncryptedRandomSessionKey` RC4-decrypted under it with key exchange; a key that
  is not 16 bytes is refused). The mail and HTTP challenges are unchanged.
- The accepting step carries an `NtlmSecurityLayer` (`ISaslSecurityLayer`) when the
  `AUTHENTICATE_MESSAGE` asks for sealing or signing: [MS-NLMP] 3.4 with extended session
  security, per-direction keys and RC4 handles, sequence numbers from 0, the 16-byte signature
  then the sealed (or, signing only, clear) message. Asking for either without extended session
  security is refused (`NoExtendedSessionSecurityNote`). `ForInitiator` is the client's
  mirror, which the tests play.
- Never unchecked: under `--allow-anonymous` a user with no account is refused with
  `SecurityLayerNeedsPasswordNote`, and a known user is checked as usual.
- The tests replay `Fixtures/ldap-ntlm-sealed` and `Fixtures/ldap-negotiate-sealed`, unsealing
  `WinLDAP`'s first buffer to the base search, and check [MS-NLMP] section 4.2.4.4's example.

## SPNEGO-wrapped NTLM in LDAP binds (BL-330)

- Where `CanCarrySecurityLayer`, a first token that is an `InitialContextToken` runs
  `NtlmSaslExchange`'s handshake inside SPNEGO (ADR-0040 decision 3): a `NegTokenInit` naming
  NTLM (else refused), the challenge in an `accept-incomplete` `negTokenResp` (`supportedMech` in
  the first reply only; NTLM not first, or no optimistic token, gets `supportedMech` alone first),
  and the success's `accept-completed` `negTokenResp` as `SaslLoginStep.AdditionalSuccessData`.
  A bare message after SPNEGO started, or SPNEGO after a bare challenge, is refused. Mail `NTLM`
  never unwraps.
- A client `mechListMIC` (read into `SpnegoNegTokenResp`) is checked with
  `NtlmSecurityLayer.VerifyMechListMic` over `SpnegoNegTokenInit.MechTypesDer` and answered with
  `SignMechListMic`: NTLM signatures with sequence number 0, each RC4 handle keyed afresh after
  ([MS-SPNG] 3.1.5.1), so the first sealed message carries 1. A wrong one is refused
  (`WrongMechListMicNote`), none where NTLM was not first too (`MissingMechListMicNote`), and one
  without extended session security too (`NoExtendedSessionSecurityNote`).
- `WinLDAP` sent bare NTLM in every configuration measured, so `LdapSpnegoNtlmSaslMechanismTests`
  wraps its recorded messages in SPNEGO and plays the client's mechListMIC; the first search it
  seals is `WinLDAP`'s recorded ciphertext under a sequence-1 signature.

## LDAP's DIGEST-MD5 bind and its security layers (BL-326)

- `DigestMd5SaslExchange` started with `CanCarrySecurityLayer` sends ADR-0072 decision 4's
  challenge (`qop="auth,auth-int,auth-conf",cipher="3des,rc4",maxbuf=65536`), answers an empty
  initial response (as `WinLDAP` opens the bind) with it as it would none, and accepts a match at
  once: `rspauth=<hex>` is the accepting step's `SaslLoginStep.AdditionalSuccessData`
  (Abstractions), which the LDAP server sends as `serverSaslCreds`. The mail exchange, its
  challenge and its `rspauth` continuation are unchanged.
- `DigestMd5Response` reads `cipher`; `Answers(nonce, offersSecurityLayers)` takes `auth-int`, and
  `auth-conf` with `3des` or `rc4`, only where the LDAP challenge offered them.
  `DigestMd5Calculation` appends `:00000000000000000000000000000000` to `A2` for both and gives
  `H(A1)` (`ComputeSessionKey`).
- `DigestMd5SecurityLayer` (`ISaslSecurityLayer`) is RFC 2831 sections 2.3 and 2.4:
  per-direction `Ki` and `Kc`, the 10-byte HMAC-MD5 MAC, message type 1 and a big-endian sequence
  number from 0, the message in clear (`auth-int`) or encrypted with its MAC (`auth-conf`): RC4
  kept running, or two-key 3DES-CBC (the BCL's `TripleDES`, keyed K1 K2 K1) with its IV from the
  key and chained across messages and 1 to 8 padding bytes. `qop=auth` has no layer.
  `ForInitiator` is the client's mirror, which the tests play.
- Never unchecked: under `--allow-anonymous` a user with no account is refused with
  `NtlmSaslExchange.SecurityLayerNeedsPasswordNote`, and a known user is checked as usual.
- The tests replay `Fixtures/ldap-digest-md5` and compute RFC 2831's layout themselves.
