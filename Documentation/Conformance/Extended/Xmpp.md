# XMPP requirement matrix

The server-side requirements `Surl.Extended.Protocol.Xmpp` answers, for the `xmpp` and `xmpps`
schemes (ADR-0083 decision 9). Written from RFC 6120 (XMPP Core), RFC 6121 (XMPP IM), RFC 7622
(address format), XEP-0030 (service discovery), XEP-0199 (ping), XEP-0368 (direct TLS), and the
SASL mechanisms' RFC 4616, RFC 4505, RFC 5802 and RFC 7677, the specifications
[ADR-0114](../../Planning/Decisions/ADR-0114-how-the-xmpp-server-answers-xmpp-and-xmpps.md) decides
by, never from the Curl port's client matrix. Each row binds the server (the receiving entity);
"Covered by" names the task that proves it, until that task replaces its ID with the named test, or
says why the row needs no test.

IDs are `Xmpp:<source>-<section>-<n>` for a specification's requirement, and
`Xmpp:Surl-<Decision>-<n>` for a rule ADR-0114 adds where the specifications leave the server a
choice.

## Streams and XML (RFC 6120 sections 4 and 11)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:RFC6120-4.1-1 | MUST | The stream is read as XML however its bytes are split across reads, one first-level element at a time. | `XmppStreamReaderTests.ReadElementAsync_ElementSplitAcrossReads_IsDeliveredWhole` |
| Xmpp:RFC6120-4.2-1 | MUST | The server answers the client's stream header with its own response header. | `XmppServerTests.ServeAsync_Rfc6120OpeningHeader_IsAnsweredWithTheRfcsResponseHeaderShape` |
| Xmpp:RFC6120-4.7.1-1 | MUST | The response header's `from` is the stream's domain. | `XmppServerTests.ServeAsync_ToNamingADomainServed_OpensAStreamForIt` |
| Xmpp:RFC6120-4.7.3-1 | MUST | The response header carries a new, unpredictable `id` for every stream, each restart included. | `XmppServerTests.ServeAsync_EachStream_GetsANewId` |
| Xmpp:RFC6120-4.7.4-1 | SHOULD | The response header's `xml:lang` is the client's, else `en`. | `XmppServerTests.ServeAsync_ClientLanguageAndHigherMinorVersion_AreAnsweredWithItsLanguageAndVersion10` |
| Xmpp:RFC6120-4.7.5-1 | MUST | The response `version` is `1.0`, the lower of the two; a major other than 1, a missing version or a malformed one is `unsupported-version`. | `XmppServerTests.ServeAsync_ClientLanguageAndHigherMinorVersion_AreAnsweredWithItsLanguageAndVersion10`, `XmppServerTests.ServeAsync_VersionOtherThanMajor1_IsUnsupportedVersion` |
| Xmpp:RFC6120-4.8-1 | MUST | A stream namespace other than `http://etherx.jabber.org/streams` or a content namespace other than `jabber:client` is `invalid-namespace`. | `XmppServerTests.ServeAsync_RefusedXml_EndsTheStreamWithItsError`, `XmppStreamReaderTests.ReadHeaderAsync_RefusedHeader_ThrowsItsStreamError` |
| Xmpp:RFC6120-4.4-1 | MUST | `</stream:stream>` from the client is answered `</stream:stream>`, then the connection is closed. | `XmppServerTests.ServeAsync_ClientClosesTheStream_IsAnsweredWithTheServersClose` |
| Xmpp:RFC6120-4.6.1-1 | MUST | Whitespace between first-level elements is accepted as a keepalive. | `XmppStreamReaderTests.ReadElementAsync_WhitespaceKeepalivesThenEndTag_ReturnsNullAsAClientClose` |
| Xmpp:RFC6120-4.9.1.1-1 | MUST | A stream error before the client's header is complete is preceded by the server's response header. | `XmppServerTests.ServeAsync_RefusedXml_EndsTheStreamWithItsError` |
| Xmpp:RFC6120-4.9.1.2-1 | MUST | After a stream error the server sends `</stream:stream>` and closes. | `XmppServerTests.ServeAsync_ElementBeforeAuthentication_EndsTheStreamWithItsError` |
| Xmpp:RFC6120-4.9.3.13-1 | MUST | Bytes that are not well-formed XML, or a first element that is not a stream, are `not-well-formed`. | `XmppServerTests.ServeAsync_RefusedXml_EndsTheStreamWithItsError`, `XmppStreamReaderTests.ReadHeaderAsync_RefusedHeader_ThrowsItsStreamError` |
| Xmpp:RFC6120-11.1-1 | MUST | A DTD, comment, processing instruction (other than a header's XML declaration) or non-predefined entity reference is `restricted-xml`. | `XmppServerTests.ServeAsync_RefusedXml_EndsTheStreamWithItsError` |
| Xmpp:RFC6120-11.6-1 | MUST | An encoding other than UTF-8 is `unsupported-encoding`. | `XmppStreamReaderTests.ReadHeaderAsync_BytesThatAreNotUtf8_IsUnsupportedEncoding`, `XmppStreamReaderTests.ReadHeaderAsync_RefusedHeader_ThrowsItsStreamError` |
| Xmpp:RFC6120-4.9.3.24-1 | MUST | A first-level element in `jabber:client` that is not a stanza is `unsupported-stanza-type`. | `XmppServerTests.ServeAsync_ElementBeforeAuthentication_EndsTheStreamWithItsError` |
| Xmpp:Surl-Errors-1 | MUST NOT | No stream or stanza error carries `<text/>`, an account name, a path, an exception or a version. | `XmppServerTests.ServeAsync_ElementBeforeAuthentication_EndsTheStreamWithItsError` |

## The domain (RFC 6120 4.7.2, RFC 7622)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:RFC6120-4.9.3.6-1 | MUST | A `to` naming a domain not served is `host-unknown`. | `XmppServerTests.ServeAsync_WrongTo_IsHostUnknown` |
| Xmpp:Surl-Domain-1 | MUST | The listen URL's host is served; on a wildcard listen address, the connection's local address; on a loopback listen, both `localhost` and the loopback literal. | `XmppDomainsTests.Served_ListenHost_ServesTheDecisionsDomains` |
| Xmpp:Surl-Domain-2 | MUST | Every `--mail-domain` is served too. | `XmppDomainsTests.Served_MailDomains_FollowTheListenDomainsLowerCasedWithoutDuplicates` |
| Xmpp:Surl-Domain-3 | MUST | A header with no `to` opens a stream for the first domain served. | `XmppServerTests.ServeAsync_NoTo_OpensAStreamForTheFirstDomainServed` |

## STARTTLS and direct TLS (RFC 6120 section 5; XEP-0368)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:RFC6120-5.3.1-1 | MUST | On `xmpp` with a certificate, the first features are `<starttls><required/></starttls>` alone. | `ServeAsync_CertificateConfigured_OffersRequiredStartTlsAlone` |
| Xmpp:Surl-Tls-1 | MUST | Under `--allow-plaintext-auth`, STARTTLS is offered without `<required/>`, beside the plain-text mechanisms. | `ServeAsync_AllowPlaintextAuthentication_OffersVoluntaryStartTlsBesideThePlainTextMechanisms` |
| Xmpp:RFC6120-6.5.4-1 | MUST | `<auth/>` before TLS when TLS is required is `<failure><encryption-required/></failure>`, the stream kept. | `ServeAsync_SaslAuthBeforeTlsWhileRequired_IsEncryptionRequiredAndTheStreamKept` |
| Xmpp:RFC6120-5.3.1-2 | MUST | A stanza before TLS is `not-authorized`; any other element is `policy-violation`. | `ServeAsync_OtherElementBeforeTls_EndsTheStreamAsSection5Says` |
| Xmpp:RFC6120-5.4.2.3-1 | MUST | `<starttls/>` is answered `<proceed/>`, then the TLS handshake through the listener seam. | `ServeAsync_StartTls_IsAnsweredProceedThenTheUpgrade` |
| Xmpp:RFC6120-5.4.2.2-1 | MUST | When the upgrade is unavailable, or `<starttls/>` is repeated, the answer is the TLS `<failure/>`, then `</stream:stream>` and close. | `ServeAsync_StartTlsWithoutACertificate_IsTheTlsFailureAndClose`, `ServeAsync_StartTlsRepeatedAfterTls_IsTheTlsFailureAndClose` |
| Xmpp:RFC6120-5.4.3.2-1 | MUST | A failed handshake closes the connection with no bytes. | `ServeAsync_HandshakeFails_ClosesWithNoMoreBytes` |
| Xmpp:RFC6120-5.4.3.3-1 | MUST | After TLS the client's new stream header is answered with a new `id`, and STARTTLS is not offered again. | `ServeAsync_StreamRestartedAfterTls_HasANewIdAndOffersSaslAndNotStartTls` |
| Xmpp:XEP0368-1 | MUST | On `xmpps` the TLS handshake runs from the first byte, before any XML, and STARTTLS is never offered. | `XmppServerStartTlsTests.ServeAsync_ConnectionTlsFromItsFirstByte_OffersSaslAndRefusesStartTls`, `PinnedCurlPortClientTalksToSurlOverXmppTests.SendMessage_AfterScramLogin_Succeeds` (`xmpps` case) |
| Xmpp:Surl-Tls-2 | MUST | `xmpps` without a certificate, and `xmpp` without one and without `--allow-plaintext-auth` or `--allow-anonymous`, fail at start with `CertificateProblem` (58). | `CommandLineRunnerExtendedProtocolTests.RunAsync_XmppsSchemeWithoutACertificate_ExitsWithTheMissingCertificateCode` |

## SASL (RFC 6120 section 6; RFC 4616, RFC 4505, RFC 5802, RFC 7677)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:RFC6120-6.4.1-1 | MUST | `<mechanisms/>` lists `SCRAM-SHA-256`, `SCRAM-SHA-1`, `PLAIN`, then `ANONYMOUS` only under `--allow-anonymous`, as the policy offers them for the TLS state. | `XmppServerStartTlsTests.ServeAsync_StreamRestartedAfterTls_HasANewIdAndOffersSaslAndNotStartTls`, `XmppServerStartTlsTests.ServeAsync_AllowPlaintextAuthentication_OffersVoluntaryStartTlsBesideThePlainTextMechanisms` |
| Xmpp:RFC6120-6.4.2-1 | MUST | An initial response of `=` is an empty one. | `XmppSaslLoginTests.StartAsync_EqualsSign_IsAnEmptyInitialResponse` |
| Xmpp:RFC6120-6.4.3-1 | MUST | Challenges and responses carry base64; SCRAM runs to `<success/>` holding the server-final message. | `XmppSaslLoginTests.StartAsync_ScramWithTheRfcExample_ChallengesThenSucceedsWithTheServerFinalMessageAndRestartsTheStream` |
| Xmpp:RFC6120-6.4.6-1 | MUST | After `<success/>` the client's new stream header is answered with the bind feature. | `XmppSaslLoginTests.StartAsync_PlainOverTls_SucceedsEmptyAndOffersBindAfterTheRestart` |
| Xmpp:RFC6120-6.5-1 | MUST | Refused logins are `not-authorized` after the refusal delay; an unoffered mechanism `invalid-mechanism`; bad base64 `incorrect-encoding`; an `<auth/>` with no mechanism `malformed-request` (a mechanism message that does not parse is refused by the policy, so `not-authorized`); `<abort/>` `aborted`. | `XmppSaslLoginTests.StartAsync_PlainWithAWrongPassword_IsNotAuthorized`, `XmppSaslLoginTests.StartAsync_MechanismNotOffered_IsInvalidMechanismAndNoExchangeStarts`, `XmppSaslLoginTests.StartAsync_InitialResponseNotBase64_IsIncorrectEncoding`, `XmppSaslLoginTests.StartAsync_NoMechanism_IsMalformedRequest`, `XmppSaslLoginTests.AbortAsync_DuringAnExchange_IsAbortedAndNotCountedAsAFailure`, `XmppSaslLoginTests.StartAsync_PolicyRefusal_IsItsFailureCondition` |
| Xmpp:RFC6120-6.5.6-1 | MUST | An authzid other than empty or the account's own bare JID is `invalid-authzid`. | XmppResourceConflictTests.ServeAsync_PlainAuthzidOfAnotherAccount_IsInvalidAuthzid, XmppResourceConflictTests.ServeAsync_PlainAuthzidOfTheAccountsOwnBareJid_LogsInWithTheAuthzidDropped, XmppResourceConflictTests.ServeAsync_ScramAuthzid_IsInvalidAuthzidUnlessEmptyOrTheUser (BL-885) |
| Xmpp:RFC6120-6.4.5-1 | SHOULD | Three failed attempts on one stream end it with `policy-violation`. | `XmppSaslLoginTests.StartAsync_ThirdFailure_IsAnsweredThenEndsTheStreamWithPolicyViolation`, `XmppSaslLoginTests.StartAsync_AfterTwoFailures_ASuccessStillLogsIn` |
| Xmpp:RFC4616-2-1 | MUST | PLAIN's message is authzid NUL authcid NUL password, judged through `ISaslAuthenticationPolicy`. | `XmppSaslLoginTests.StartAsync_PlainOverTls_SucceedsEmptyAndOffersBindAfterTheRestart`, `XmppSaslLoginTests.StartAsync_PlainWithAWrongPassword_IsNotAuthorized` |
| Xmpp:RFC5802-5-1 | MUST | SCRAM-SHA-1 verifies the client proof and returns the server signature; gs2 header `p` is refused. | `XmppSaslLoginTests.StartAsync_ScramWithTheRfcExample_ChallengesThenSucceedsWithTheServerFinalMessageAndRestartsTheStream`, `XmppSaslLoginTests.ContinueAsync_ScramWithABadProof_IsNotAuthorizedAndTheStreamKept` |
| Xmpp:RFC7677-3-1 | MUST | SCRAM-SHA-256 is SCRAM over SHA-256. | `XmppSaslLoginTests.StartAsync_ScramWithTheRfcExample_ChallengesThenSucceedsWithTheServerFinalMessageAndRestartsTheStream` |
| Xmpp:RFC4505-2-1 | MUST | ANONYMOUS, under `--allow-anonymous` only, logs in as a new `anon-<16 hex>` localpart. | `XmppResourceBindingTests.AnswerAsync_AnonymousLogin_BindsAnAnonName` |
| Xmpp:Surl-Sasl-1 | MUST | An account `name` logs in as `name@<stream domain>`; an account `local@domain` only on that domain; a name invalid as a localpart is `not-authorized`. | `XmppSaslLoginTests.StartAsync_AccountUnusableAsAJidOnTheStreamDomain_IsNotAuthorized`, `XmppResourceBindingTests.AnswerAsync_MailboxAccountOnTheStreamDomain_BindsThatJid` |

## Resource binding (RFC 6120 section 7)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:RFC6120-7.6.1-1 | MUST | An empty `<bind/>` binds a generated `surl-<16 hex>` resource and answers the full JID. | `XmppResourceBindingTests.AnswerAsync_BindWithoutAResource_IsAnsweredWithAGeneratedOne` |
| Xmpp:RFC6120-7.7.1-1 | MUST | A client's valid resource is bound as given; an invalid one is `bad-request`. | `XmppResourceBindingTests.AnswerAsync_BindWithAResource_IsAnsweredWithTheFullJid`, `XmppResourceBindingTests.AnswerAsync_ResourceNotAResourcepart_IsBadRequestAndTheStreamKept` |
| Xmpp:RFC6120-7.7.2.2-1 | MUST | A resource already bound for the same bare JID is taken by the new stream; the older gets the stream error `conflict`. | XmppResourceConflictTests.ServeAsync_SecondStreamBindsTheSameResource_TakesItOverAndTheFirstGetsConflict (BL-885) |
| Xmpp:RFC6120-7.6.2.1-1 | MUST | An eleventh resource for one bare JID is `resource-constraint`. | XmppResourceConflictTests.ServeAsync_EleventhResourceOfOneBareJid_IsResourceConstraintOfTypeWait (BL-885) |
| Xmpp:RFC6120-7.6.2.2-1 | MUST | A second bind on one stream is `not-allowed`. | `XmppResourceBindingTests.AnswerAsync_SecondBind_IsNotAllowed` |
| Xmpp:RFC6120-4.9.3.12-1 | MUST | A stanza or bind before SASL success, or a stanza before binding, is `not-authorized`. | `XmppResourceBindingTests.AnswerAsync_StanzaBeforeBind_EndsTheStreamNotAuthorized`, `XmppSaslLoginTests.ServeAsync_OtherElementBeforeLogin_EndsTheStream` |
| Xmpp:RFC6121-E-1 | MAY | A legacy session IQ set is answered with an empty `result`; the session feature is not advertised. | `XmppResourceBindingTests.AnswerAsync_LegacySessionAfterBind_IsAnEmptyResult`, `XmppStanzaRouterTests.AnswerAsync_LegacySessionToTheServer_IsAnEmptyResult` |

## Stanzas (RFC 6120 sections 8 and 10; RFC 6121 sections 4, 5 and 8.5)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:RFC6120-8.1.2.1-1 | MUST | Every routed stanza carries `from` = the sender's full JID. | `XmppStanzaRouterTests.AnswerAsync_ChatToAnotherUsersFullJid_IsDeliveredToThatStreamWithFromStamped` |
| Xmpp:RFC6120-4.9.3.9-1 | MUST | A client `from` other than its full or bare JID is `invalid-from`. | `XmppStanzaRouterTests.AnswerAsync_FromThatIsNotTheStreamsJid_EndsTheStreamInvalidFrom`, `XmppStanzaRouterTests.AnswerAsync_FromThatIsTheStreamsBareJid_IsAcceptedAndStampedWithTheFullJid` |
| Xmpp:RFC6120-8.3.3.8-1 | MUST | A `to` that is not a valid JID is `jid-malformed`. | `XmppStanzaRouterTests.AnswerAsync_ToThatIsNotAJid_IsJidMalformed` |
| Xmpp:RFC6120-10.4.3-1 | MUST | A `to` on a domain not served is `remote-server-not-found`. | `XmppStanzaRouterTests.AnswerAsync_MessageToAnotherServersDomain_IsRemoteServerNotFound` |
| Xmpp:RFC6120-10.3.3-1 | MUST | A stanza with no `to` is handled as addressed to the sender's bare JID. | BL-1049 |
| Xmpp:RFC6121-8.5.3.1-1 | MUST | A message to a bound full JID is delivered to that stream. | `XmppStanzaRouterTests.AnswerAsync_ChatToAnotherUsersFullJid_IsDeliveredToThatStreamWithFromStamped`, `XmppStanzaRouterTests.AnswerAsync_ErrorMessageToABoundFullJid_IsDelivered` |
| Xmpp:RFC6121-8.5.2.1.1-1 | MUST | A `normal` or `chat` message to a bare JID goes to each available stream with non-negative priority. | `XmppStanzaRouterTests.AnswerAsync_ChatToABareJid_GoesToEveryAvailableStreamWithNonNegativePriority` |
| Xmpp:RFC6121-8.5.2.2-1 | MUST | With no available stream, `normal` and `chat` are `service-unavailable`, `headline` and `error` are dropped, `groupchat` is `service-unavailable`. | `XmppStanzaRouterTests.AnswerAsync_ChatToAUserWithNoStream_IsServiceUnavailableWhetherOrNotTheAccountExists`, `XmppStanzaRouterTests.AnswerAsync_HeadlineOrErrorToAUserWithNoStream_IsDropped`, `XmppStanzaRouterTests.AnswerAsync_GroupchatToAUser_IsServiceUnavailable` |
| Xmpp:RFC6121-5.2.2-1 | MUST | A message of unknown `type` is treated as `normal`. | BL-1049 |
| Xmpp:Surl-Stanza-1 | MUST | A bound stream is available at priority 0 until it sends presence; undirected presence sets its priority, `unavailable` removes it; directed presence is dropped. | `XmppStanzaRouterTests.AnswerAsync_PresenceWithNoPriority_IsAvailableAtPriorityZero`, `XmppStanzaRouterTests.AnswerAsync_PresenceAgainAfterUnavailable_MakesTheStreamAvailable`, `XmppStanzaRouterTests.AnswerAsync_DirectedOrSubscriptionPresence_IsDropped` |
| Xmpp:RFC6121-4.7.2.3-1 | MUST | A presence priority outside -128 to 127 is `bad-request`. | `XmppStanzaRouterTests.AnswerAsync_PresencePriorityOutOfRange_IsBadRequest` |
| Xmpp:RFC6120-8.2.3-1 | MUST | An IQ `get` or `set` without exactly one child is `bad-request`; each gets exactly one `result` or `error`. | `XmppStanzaRouterTests.AnswerAsync_IqGetWithoutExactlyOneChild_IsBadRequest`, `XmppStanzaRouterTests.AnswerAsync_IqNoOneAnswers_IsServiceUnavailable` |
| Xmpp:RFC6120-8.1.3-1 | MUST | An IQ with no `id` or an unknown `type` is the stream error `invalid-xml`. | `XmppResourceBindingTests.AnswerAsync_IqWithNoIdOrAnUnknownType_IsInvalidXml` |
| Xmpp:RFC6120-8.3.1-1 | MUST | A stanza error swaps `to` and `from`, keeps an IQ's child, carries a defined condition and no `<text/>`, and is never sent for a stanza of type `error`. | `XmppStanzaRouterTests.AnswerAsync_IqInAnUnknownNamespace_IsServiceUnavailableWithTheChildKept`, `XmppStanzaRouterTests.AnswerAsync_IqResultOrErrorFromTheClient_IsNeverAnswered` |
| Xmpp:RFC6120-8.4-1 | MUST | An IQ in a namespace the server does not serve is `service-unavailable`. | `XmppStanzaRouterTests.AnswerAsync_IqInAnUnknownNamespace_IsServiceUnavailableWithTheChildKept` |
| Xmpp:RFC6121-8.5.3.2.2-1 | MUST | An IQ to an unbound full JID, or to a bare JID with no bound stream, is `service-unavailable`. | `XmppStanzaRouterTests.AnswerAsync_IqNoOneAnswers_IsServiceUnavailable`, `XmppResourceBindingTests.AnswerAsync_PingToOwnAccountAfterBind_IsServiceUnavailable` |
| Xmpp:RFC6121-8.5.3.1-2 | MUST | An IQ to a bound full JID is delivered, and its `result` or `error` routed back. | `XmppStanzaRouterTests.AnswerAsync_IqToABoundFullJid_IsDeliveredAndItsResultRoutedBack` |
| Xmpp:RFC6121-2.1.3-1 | MUST | A roster get to one's own bare JID is answered with an empty roster. | `XmppStanzaRouterTests.AnswerAsync_RosterGetOfOnesOwn_IsAnEmptyRoster` |
| Xmpp:Surl-Stanza-2 | MUST NOT | Routing answers the same whether an account exists or has no stream. | `XmppStanzaRouterTests.AnswerAsync_ChatToAUserWithNoStream_IsServiceUnavailableWhetherOrNotTheAccountExists` |

## Service discovery and ping (XEP-0030; XEP-0199)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:XEP0030-3.1-1 | MUST | disco#info to the server answers identity `server`/`im` and features disco#info, disco#items and `urn:xmpp:ping`, with no `name`. | `XmppStanzaRouterTests.AnswerAsync_DiscoInfoToTheServer_IsAnsweredInXep0030Example2sShape` |
| Xmpp:XEP0030-3.1-2 | MUST | disco#info to a bare JID with a bound stream answers identity `account`/`registered` and features disco#info and disco#items. | `XmppStanzaRouterTests.AnswerAsync_DiscoInfoToABareJidWithABoundStream_IsARegisteredAccount`, `PinnedCurlPortClientTalksToSurlOverXmppTests.DiscoInfo_ToTheOwnBareJid_PrintsTheAccountIdentity` |
| Xmpp:XEP0030-4.1-1 | MUST | disco#items to the server or a bare JID answers an empty `query`. | `XmppStanzaRouterTests.AnswerAsync_DiscoItemsToTheServer_IsAnEmptyQuery`, `XmppStanzaRouterTests.AnswerAsync_DiscoItemsToABareJidWithABoundStream_IsAnEmptyQuery` |
| Xmpp:XEP0030-3.2-1 | MUST | A disco query with a `node` is `item-not-found`. | `XmppStanzaRouterTests.AnswerAsync_DiscoWithANode_IsItemNotFound` |
| Xmpp:XEP0199-4.2-1 | MUST | A ping to the server is answered with an empty `result`. | `XmppStanzaRouterTests.AnswerAsync_PingToTheServer_IsAnEmptyResult` |
| Xmpp:XEP0199-4.1-1 | MAY | The server pings clients. | n/a: the server never sends a ping; whitespace and `--idle-timeout` bound idle streams (ADR-0114 decision 7) |

## Limits (ADR-0006; RFC 6120 13.12)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Xmpp:RFC6120-13.12-1 | MUST | A stanza over `--max-message` bytes is `policy-violation`, then close. | `XmppServerTests.ServeAsync_ElementOverTheSizeLimit_IsPolicyViolation` |
| Xmpp:Surl-Limits-1 | MUST | Nesting past 32 levels, more than 64 attributes on an element, or a value over `--max-message` is `policy-violation`. | `XmppStreamReaderTests.ReadElementAsync_Nesting_IsRefusedPast32Levels`, `XmppStreamReaderTests.ReadElementAsync_Attributes_AreRefusedPast64`, `XmppStreamReaderTests.ReadElementAsync_ValueOverTheSizeBoundAcrossTheStartTag_IsPolicyViolation` |
| Xmpp:Surl-Limits-2 | MUST | A stream header not complete within `--head-timeout` is `connection-timeout`. | `XmppServerTests.ServeAsync_NoHeaderWithinTheHeadTimeout_IsConnectionTimeout` |
| Xmpp:Surl-Limits-3 | MUST | No bound resource within twice `--head-timeout` of accept is `policy-violation`. | XmppResourceConflictTests.ServeAsync_NoResourceBoundWithinTwiceTheHeadTimeout_IsPolicyViolation (BL-885) |
| Xmpp:RFC6120-4.9.3.4-1 | MUST | No byte for `--idle-timeout` is `connection-timeout`; the test proves the answer to a limit cancelling the exchange, which `--max-time` also is (ADR-0114 decision 7). | `XmppServerTests.ServeAsync_CancelledWhileOpen_SaysFarewellWithItsError` |
| Xmpp:RFC6120-4.9.3.17-1 | MUST | A connection over `--max-connections` or `--max-connections-per-address` gets a response header and `resource-constraint`. | `XmppServerTests.WriteRefusalAsync_Refusal_IsAHeaderAndResourceConstraint` |
| Xmpp:Surl-Limits-4 | MUST | A stream with 16 MiB of routed stanzas unsent ends with `policy-violation`. | `XmppStanzaRouterTests.ServeAsync_RecipientWithMoreThan16MiBRoutedUnsent_EndsWithPolicyViolationAndTheSenderGetsNoError`, `XmppStanzaRouterTests.ServeAsync_RecipientWhoseWritesNeverComplete_LeavesTheSenderFreeToSendAndBeAnswered` |
| Xmpp:RFC6120-4.9.3.20-1 | MUST | A server stopping ends each stream with `system-shutdown`. | `XmppServerTests.ServeAsync_CancelledWhileOpen_SaysFarewellWithItsError` |
